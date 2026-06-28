using Microsoft.Maui.Media;
using SkiaSharp;
using System.IO;

namespace FIRE
{
    public partial class RegisterPage : ContentPage
    {
        // PERBAIKAN WARNING: Menambahkan tanda ? agar field ini boleh bernilai null sebelum foto diambil
        private byte[]? _faceData;
        private string _selectedRole = "";

        public RegisterPage()
        {
            InitializeComponent();

            _selectedRole = "";
            LabelRoleValue.Text = "Pilih hak akses";
            LabelRoleValue.TextColor = Color.FromArgb("#6B7280");
        }

        private async void OnSelectRoleTapped(object sender, TappedEventArgs e)
        {
            string pilihan = await DisplayActionSheet(
                "Pilih Hak Akses",
                "Batal",
                null,
                "OPERATOR",
                "WARGA SIPIL"
            );

            if (pilihan == "OPERATOR" || pilihan == "WARGA SIPIL")
            {
                _selectedRole = pilihan;
                LabelRoleValue.Text = pilihan;
                LabelRoleValue.TextColor = Color.FromArgb("#1F2933");
            }
        }

        private async void OnCaptureFaceClicked(object sender, EventArgs e)
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                using var stream = await photo.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _faceData = memoryStream.ToArray();

                CameraPreview.Source = ImageSource.FromStream(() => new MemoryStream(_faceData));
            }
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            string user = EntryUsername.Text?.Trim() ?? "";
            string pass = EntryPassword.Text ?? "";

            if (string.IsNullOrEmpty(user) ||
     string.IsNullOrEmpty(pass) ||
     string.IsNullOrEmpty(_selectedRole) ||
     _faceData == null)
            {
                await DisplayAlert(
                    "Gagal",
                    "Semua data termasuk hak akses dan foto wajah wajib diisi!",
                    "OK"
                );

                return;
            }

            string selectedRole = _selectedRole;

            string imagePath = System.IO.Path.Combine(
                FileSystem.AppDataDirectory,
                $"{user}_face.png"
            );

            File.WriteAllBytes(imagePath, _faceData);

            bool registerSuccess =
                await FireDatabase.Instance.RegisterUserAsync(
                    user,
                    pass,
                    selectedRole,
                    imagePath
                );

            if (!registerSuccess)
            {
                await DisplayAlert(
                    "Gagal",
                    "Username sudah terdaftar. Gunakan username lain.",
                    "OK"
                );

                return;
            }

            UserAccount? checkUser =
    await FireDatabase.Instance.GetUserByUsernameAsync(user);

            int userCount =
                await FireDatabase.Instance.GetUserCountAsync();

            string databasePath =
                await FireDatabase.Instance.GetDatabasePathAsync();

            string exportInfo = "";

#if WINDOWS
string exportPath =
    await FireDatabase.Instance.ExportDatabaseToDesktopAsync();

exportInfo = $"\n\nDatabase export:\n{exportPath}";
#endif

            await DisplayAlert(
                "Sukses",
                $"User '{user}' berhasil didaftarkan sebagai {selectedRole}!\n\n" +
                $"User terbaca ulang: {(checkUser != null ? "YA" : "TIDAK")}\n" +
                $"Jumlah user di database: {userCount}\n\n" +
                $"Database asli:\n{databasePath}" +
                exportInfo,
                "OK"
            );

            Application.Current!.MainPage = new MainPage();
        }
        private void OnBackToLoginClicked(object sender, EventArgs e)
        {
            // Arahkan kembali ke halaman login (MainPage)
            if (Application.Current != null)
            {
                Application.Current.MainPage = new MainPage();
            }
        }
    }
}