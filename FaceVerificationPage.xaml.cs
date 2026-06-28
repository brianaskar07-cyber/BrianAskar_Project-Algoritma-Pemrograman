using System.IO;
using System.Threading;

#if WINDOWS
using OpenCvSharp;
using OpenCvSharp.Face;
#endif

namespace FIRE
{
    public partial class FaceVerificationPage : ContentPage
    {
        private readonly string _username;
        private readonly string _role;
        private readonly string _dbPhotoPath;

        private CancellationTokenSource? _scanCts;
        private bool _isNavigating;

        // Semakin kecil, semakin ketat.
        // 75 terlalu longgar, wajah orang lain bisa lolos.
        // Mulai dari 45 untuk lebih aman.
        private const double MatchThreshold = 45.0;
        private const int RequiredSuccessFrames = 5;

        public FaceVerificationPage(string username, string role)
        {
            InitializeComponent();

            _username = username;
            _role = role;

            _dbPhotoPath = Path.Combine(
                FileSystem.AppDataDirectory,
                $"{username}_face.png");

            LabelRole.Text = $"Pengguna: {username} • Hak akses: {role}";
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            StartLiveFaceScan();
        }

        protected override void OnDisappearing()
        {
            StopLiveFaceScan();

            base.OnDisappearing();
        }

        private void OnStartScanClicked(object sender, EventArgs e)
        {
            StartLiveFaceScan();
        }

        private void StartLiveFaceScan()
        {
            StopLiveFaceScan();

            _isNavigating = false;
            _scanCts = new CancellationTokenSource();

            ScanIndicator.IsVisible = true;
            ScanIndicator.IsRunning = true;

            LabelSimilarity.Text = "Status: membuka kamera laptop...";
            LabelStatus.Text = "Pemindaian berjalan otomatis. Tetap berada di depan kamera hingga proses selesai.";

#if WINDOWS
            _ = Task.Run(() => RunWindowsLiveScanAsync(_scanCts.Token));
#else
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ScanIndicator.IsRunning = false;
                ScanIndicator.IsVisible = false;

                LabelSimilarity.Text = "Live scan kamera laptop hanya aktif untuk Windows.";
                LabelStatus.Text = "Untuk pengujian Android, tekan tombol Lanjut ke Dashboard Android.";
            });
#endif
        }

        private void StopLiveFaceScan()
        {
            try
            {
                _scanCts?.Cancel();
                _scanCts?.Dispose();
                _scanCts = null;
            }
            catch
            {
                // Abaikan error saat menghentikan kamera.
            }
        }

#if WINDOWS
        private async Task RunWindowsLiveScanAsync(CancellationToken token)
        {
            if (!File.Exists(_dbPhotoPath))
            {
                await ShowMessageOnUiAsync(
                    "Error Database",
                    "Foto wajah untuk user ini tidak ditemukan. Silakan register ulang dan ambil foto wajah.");

                return;
            }

            string cascadePath = await EnsureCascadeFileAsync();

            using var faceCascade = new CascadeClassifier(cascadePath);

            if (faceCascade.Empty())
            {
                await ShowMessageOnUiAsync(
                    "Error",
                    "File Haar Cascade tidak bisa dibaca.");

                return;
            }

            using var referenceImage = Cv2.ImRead(_dbPhotoPath, ImreadModes.Color);

            if (referenceImage.Empty())
            {
                await ShowMessageOnUiAsync(
                    "Error",
                    "Foto wajah database tidak bisa dibaca.");

                return;
            }

            using var referenceFace = ExtractNormalizedFace(referenceImage, faceCascade);

            if (referenceFace.Empty())
            {
                await ShowMessageOnUiAsync(
                    "Wajah Tidak Terdeteksi",
                    "Foto registrasi tidak memiliki wajah yang jelas. Silakan register ulang dengan posisi wajah lurus dan pencahayaan cukup.");

                return;
            }

            using var recognizer = LBPHFaceRecognizer.Create(
                radius: 2,
                neighbors: 8,
                gridX: 8,
                gridY: 8
            );

            var augmentedFaces = BuildAugmentedFaces(referenceFace);

            recognizer.Train(
                augmentedFaces.ToArray(),
                augmentedFaces.Select(f => 1).ToArray()
            );

            using var capture = new VideoCapture(0);

            if (!capture.IsOpened())
            {
                await ShowMessageOnUiAsync(
                    "Kamera Tidak Terbuka",
                    "Kamera laptop tidak bisa dibuka. Pastikan kamera tidak sedang dipakai aplikasi lain dan izin kamera aktif.");

                return;
            }

            int successFrames = 0;

            using var frame = new Mat();
            using var gray = new Mat();

            while (!token.IsCancellationRequested && !_isNavigating)
            {
                capture.Read(frame);

                if (frame.Empty())
                {
                    await Task.Delay(80, token);
                    continue;
                }

                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                var faces = faceCascade.DetectMultiScale(
                    gray,
                    scaleFactor: 1.1,
                    minNeighbors: 5,
                    flags: HaarDetectionTypes.ScaleImage,
                    minSize: new OpenCvSharp.Size(90, 90));

                string statusText = "Wajah belum terdeteksi";

                if (faces.Length > 0)
                {
                    var faceRect = SelectBestFace(gray.Size(), faces);

                    if (faceRect.Width < 100 || faceRect.Height < 100)
                    {
                        successFrames = 0;

                        Cv2.Rectangle(frame, faceRect, Scalar.Orange, 2);

                        statusText = "Wajah terlalu jauh. Dekatkan wajah ke kamera.";

                        await UpdatePreviewOnUiAsync(frame, statusText);
                        await Task.Delay(60, token);
                        continue;
                    }

                    Cv2.Rectangle(frame, faceRect, Scalar.LimeGreen, 2);

                    using var liveFace = NormalizeFaceFromGray(gray, faceRect);

                    recognizer.Predict(liveFace, out int label, out double confidence);

                    bool isValidMatch =
                        label == 1 &&
                        confidence > 0 &&
                        confidence <= MatchThreshold;

                    if (isValidMatch)
                    {
                        successFrames++;

                        statusText =
                            $"Wajah cocok ({successFrames}/{RequiredSuccessFrames}) | Confidence: {confidence:F2}";
                    }
                    else
                    {
                        successFrames = 0;

                        statusText =
                            $"Wajah tidak cocok | Confidence: {confidence:F2}";
                    }

                    await UpdatePreviewOnUiAsync(frame, statusText);

                    if (successFrames >= RequiredSuccessFrames)
                    {
                        _isNavigating = true;

                        await NavigateToDashboardAsync();

                        break;
                    }
                }
                else
                {
                    successFrames = 0;

                    await UpdatePreviewOnUiAsync(frame, statusText);
                }

                await Task.Delay(60, token);
            }
        }

        private static Mat ExtractNormalizedFace(
            Mat image,
            CascadeClassifier faceCascade)
        {
            using var gray = new Mat();

            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);

            var faces = faceCascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,
                minNeighbors: 5,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(90, 90));

            if (faces.Length == 0)
            {
                return new Mat();
            }

            var faceRect = SelectBestFace(gray.Size(), faces);

            return NormalizeFaceFromGray(gray, faceRect);
        }

        private static Mat NormalizeFaceFromGray(
    Mat gray,
    OpenCvSharp.Rect faceRect)
{
    OpenCvSharp.Rect innerRect = GetInnerFaceRect(gray, faceRect);

    using var face = new Mat(gray, innerRect);
    var resized = new Mat();

    Cv2.Resize(face, resized, new OpenCvSharp.Size(160, 160));

    using var clahe = Cv2.CreateCLAHE(
        clipLimit: 2.0,
        tileGridSize: new OpenCvSharp.Size(8, 8)
    );

    clahe.Apply(resized, resized);

    var masked = new Mat(
        resized.Size(),
        MatType.CV_8UC1,
        Scalar.All(128)
    );

    using var mask = new Mat(
        resized.Size(),
        MatType.CV_8UC1,
        Scalar.All(0)
    );

    Cv2.Ellipse(
        mask,
        new OpenCvSharp.Point(80, 82),
        new OpenCvSharp.Size(62, 72),
        0,
        0,
        360,
        Scalar.All(255),
        -1
    );

    resized.CopyTo(masked, mask);

    return masked;
}

private static OpenCvSharp.Rect GetInnerFaceRect(
    Mat gray,
    OpenCvSharp.Rect faceRect)
{
    int x = faceRect.X + (int)(faceRect.Width * 0.12);
    int y = faceRect.Y + (int)(faceRect.Height * 0.10);
    int w = (int)(faceRect.Width * 0.76);
    int h = (int)(faceRect.Height * 0.82);

    x = Math.Clamp(x, 0, gray.Width - 1);
    y = Math.Clamp(y, 0, gray.Height - 1);

    if (x + w > gray.Width)
        w = gray.Width - x;

    if (y + h > gray.Height)
        h = gray.Height - y;

    if (w <= 0 || h <= 0)
        return faceRect;

    return new OpenCvSharp.Rect(x, y, w, h);
}

private static OpenCvSharp.Rect SelectBestFace(
    OpenCvSharp.Size imageSize,
    OpenCvSharp.Rect[] faces)
{
    double centerX = imageSize.Width / 2.0;
    double centerY = imageSize.Height / 2.0;

    return faces
        .OrderByDescending(face =>
        {
            double faceCenterX = face.X + face.Width / 2.0;
            double faceCenterY = face.Y + face.Height / 2.0;

            double distanceFromCenter =
                Math.Sqrt(
                    Math.Pow(faceCenterX - centerX, 2) +
                    Math.Pow(faceCenterY - centerY, 2)
                );

            double areaScore = face.Width * face.Height;
            double centerScore = 1.0 / (1.0 + distanceFromCenter);

            return areaScore + centerScore * 50000;
        })
        .First();
}

private static List<Mat> BuildAugmentedFaces(Mat referenceFace)
{
    var faces = new List<Mat>();

    faces.Add(referenceFace.Clone());

    var flipped = new Mat();
    Cv2.Flip(referenceFace, flipped, FlipMode.Y);
    faces.Add(flipped);

    faces.Add(AdjustBrightnessContrast(referenceFace, 1.10, 10));
    faces.Add(AdjustBrightnessContrast(referenceFace, 0.90, -10));
    faces.Add(AdjustBrightnessContrast(referenceFace, 1.20, 0));
    faces.Add(AdjustBrightnessContrast(referenceFace, 0.80, 0));

    return faces;
}

private static Mat AdjustBrightnessContrast(
    Mat source,
    double alpha,
    double beta)
{
    var result = new Mat();

    source.ConvertTo(
        result,
        MatType.CV_8UC1,
        alpha,
        beta
    );

    return result;
}

        private static async Task<string> EnsureCascadeFileAsync()
        {
            string targetPath = Path.Combine(
                FileSystem.AppDataDirectory,
                "haarcascade_frontalface_default.xml");

            if (File.Exists(targetPath))
            {
                return targetPath;
            }

            await using var input =
                await FileSystem.OpenAppPackageFileAsync(
                    "haarcascade_frontalface_default.xml");

            await using var output = File.Create(targetPath);

            await input.CopyToAsync(output);

            return targetPath;
        }

        private Task UpdatePreviewOnUiAsync(
            Mat frame,
            string statusText)
        {
            Cv2.ImEncode(".jpg", frame, out var imageBytes);

            return MainThread.InvokeOnMainThreadAsync(() =>
            {
                LabelSimilarity.Text = statusText;

                CameraPreview.Source = ImageSource.FromStream(() =>
                    new MemoryStream(imageBytes));
            });
        }
#endif

        private Task ShowMessageOnUiAsync(
            string title,
            string message)
        {
            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                ScanIndicator.IsRunning = false;
                ScanIndicator.IsVisible = false;

                LabelSimilarity.Text = message;

                await DisplayAlert(title, message, "OK");
            });
        }

        private Task NavigateToDashboardAsync()
        {
            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StopLiveFaceScan();

                ScanIndicator.IsRunning = false;
                ScanIndicator.IsVisible = false;

                LabelSimilarity.Text = "Verifikasi berhasil. Mengalihkan ke dashboard...";

                await DisplayAlert(
                    "AKSES DITERIMA",
                    "Wajah cocok. Anda akan masuk ke dashboard.",
                    "OK");

                Application.Current!.MainPage = new MainDashboardPage(_username, _role);
            });
        }

        private async void OnSkipAndroidVerificationClicked(object sender, EventArgs e)
        {
            StopLiveFaceScan();

            await DisplayAlert(
                "Mode Android",
                "Live face verification OpenCV saat ini hanya aktif untuk Windows. Untuk pengujian Android, sistem akan melanjutkan ke dashboard.",
                "OK"
            );

            Application.Current!.MainPage = new MainDashboardPage(_username, _role);
        }
        private void OnCancelClicked(object sender, EventArgs e)
        {
            StopLiveFaceScan();

            Application.Current!.MainPage = new MainPage();
        }
    }
}