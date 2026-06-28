using Microsoft.Maui.Controls.Shapes;

namespace FIRE;

public partial class MainDashboardPage : ContentPage
{
    private readonly string _username;
    private readonly string _role;
    private bool _isRunning = true;

    private readonly GeminiAiService _geminiService = new GeminiAiService();
    private readonly FireFuzzyService _fuzzyService = new FireFuzzyService();
    private readonly FireNeuralNetworkService _neuralService = new FireNeuralNetworkService();

    private DateTime _lastSensorSavedAt = DateTime.MinValue;

    private int _currentSuhu;
    private int _currentAsap;
    private int _currentHum;
    private int _currentDanger;
    private string _currentStatus = "NORMAL";
    private string _activeMenu = "MONITORING";

    private string _lastOperatorMessage = "";
    private string _lastOperatorMessageTime = "";

    public MainDashboardPage(string username, string role)
    {
        InitializeComponent();

        _username = username;
        _role = role?.ToUpper() ?? "WARGA SIPIL";

        LabelWelcome.Text = $"Selamat datang, {username}. Sistem berhasil memverifikasi identitas Anda.";
        LabelUsername.Text = username;
        LabelUserRole.Text = _role;

        ConfigureRoleDashboard();
        LoadOperatorBroadcastMessage();
        StartSimulation();
    }

    private void ConfigureRoleDashboard()
    {
        bool isOperator = _role == "OPERATOR";

        OperatorPanel.IsVisible = isOperator;
        WargaSipilPanel.IsVisible = !isOperator;
        OperatorChatPanel.IsVisible = isOperator;

        if (isOperator)
        {
            LabelDashboardTitle.Text = "Dashboard Monitoring Kebakaran";
            LabelDashboardSubtitle.Text = "Sistem peringatan dini kebakaran berbasis parameter lingkungan.";
            LabelRoleBadge.Text = "OPERATOR";
        }
        else
        {
            LabelDashboardTitle.Text = "Informasi Status Lingkungan";
            LabelDashboardSubtitle.Text = "Pantau status keamanan lingkungan dan ikuti arahan sistem.";
            LabelRoleBadge.Text = "WARGA SIPIL";
        }

        SetSidebarMenuActive("MONITORING");
    }

    private async void StartSimulation()
    {
        Random random = new Random();

        while (_isRunning)
        {
            int suhu = random.Next(25, 90);
            int asap = random.Next(100, 900);
            int hum = random.Next(25, 90);

            int danger = HitungTingkatBahaya(suhu, asap, hum);

            _currentSuhu = suhu;
            _currentAsap = asap;
            _currentHum = hum;
            _currentDanger = danger;

            lblSuhu.Text = $"{suhu}°C";
            lblAsap.Text = $"{asap} ppm";
            lblHum.Text = $"{hum}%";
            lblDanger.Text = $"{danger}%";
            dangerBar.Progress = danger / 100.0;

            UpdateStatus(danger, suhu, asap, hum);
            UpdateAiAnalysisPanels(suhu, asap, hum);
            if (_activeMenu != "MONITORING")
            {
                RefreshActiveMenu();
            }
            if ((DateTime.Now - _lastSensorSavedAt).TotalSeconds >= 10)
            {
                _lastSensorSavedAt = DateTime.Now;

                await FireDatabase.Instance.SaveSensorReadingAsync(
                    new SensorReading
                    {
                        Suhu = suhu,
                        Asap = asap,
                        Kelembapan = hum,
                        DangerLevel = danger,
                        Status = _currentStatus,
                        CreatedAt = DateTime.Now
                    }
                );
            }
            if (_role == "WARGA SIPIL")
            {
                LoadOperatorBroadcastMessage();
            }

            await Task.Delay(2000);
        }
    }

    private int HitungTingkatBahaya(int suhu, int asap, int hum)
    {
        int skorSuhu = 0;
        int skorAsap = 0;
        int skorHum = 0;

        if (suhu >= 70)
            skorSuhu = 40;
        else if (suhu >= 50)
            skorSuhu = 25;
        else if (suhu >= 35)
            skorSuhu = 10;

        if (asap >= 700)
            skorAsap = 40;
        else if (asap >= 400)
            skorAsap = 25;
        else if (asap >= 250)
            skorAsap = 10;

        if (hum <= 35)
            skorHum = 20;
        else if (hum <= 45)
            skorHum = 10;

        int total = skorSuhu + skorAsap + skorHum;

        return Math.Min(total, 100);
    }

    private void UpdateStatus(int danger, int suhu, int asap, int hum)
    {
        if (danger >= 75)
        {
            _currentStatus = "BAHAYA";
            lblStatus.Text = "BAHAYA";
            lblStatus.TextColor = Color.FromArgb("#B91C1C");
            lblDanger.TextColor = Color.FromArgb("#B91C1C");

            lblPublicStatus.Text = "BAHAYA";
            lblPublicStatus.TextColor = Color.FromArgb("#B91C1C");

            lblPublicMessage.Text = "Terdeteksi potensi kebakaran tinggi di area lingkungan.";
            lblEvacuationInstruction.Text = "Segera menjauh dari sumber bahaya, bantu warga sekitar, dan ikuti jalur evakuasi terdekat.";
        }
        else if (danger >= 45)
        {
            _currentStatus = "WASPADA";
            lblStatus.Text = "WASPADA";
            lblStatus.TextColor = Color.FromArgb("#E8752F");
            lblDanger.TextColor = Color.FromArgb("#E8752F");

            lblPublicStatus.Text = "WASPADA";
            lblPublicStatus.TextColor = Color.FromArgb("#E8752F");

            lblPublicMessage.Text = "Parameter lingkungan menunjukkan peningkatan risiko. Tetap tenang dan perhatikan kondisi sekitar.";
            lblEvacuationInstruction.Text = "Siapkan diri untuk evakuasi jika kondisi memburuk. Hindari area berasap dan sumber panas.";
        }
        else
        {
            _currentStatus = "NORMAL";
            lblStatus.Text = "NORMAL";
            lblStatus.TextColor = Color.FromArgb("#15803D");
            lblDanger.TextColor = Color.FromArgb("#1E3A5F");

            lblPublicStatus.Text = "AMAN";
            lblPublicStatus.TextColor = Color.FromArgb("#15803D");

            lblPublicMessage.Text = "Kondisi lingkungan saat ini berada dalam batas aman.";
            lblEvacuationInstruction.Text = "Tetap waspada dan pantau informasi dari sistem secara berkala.";
        }

        lblPublicDetail.Text = $"Suhu {suhu}°C • Asap {asap} ppm • Kelembapan {hum}%";
    }

    private void UpdateAiAnalysisPanels(int suhu, int asap, int hum)
    {
        FuzzyFireResult fuzzyResult =
            _fuzzyService.Analyze(suhu, asap, hum);

        NeuralFireResult neuralResult =
            _neuralService.Predict(suhu, asap, hum);

        Color fuzzyColor = GetStatusColor(fuzzyResult.Status);
        Color neuralColor = GetStatusColor(neuralResult.Status);

        // Tampilan OPERATOR
        lblOperatorFuzzyStatus.Text = $"Status: {fuzzyResult.Status}";
        lblOperatorFuzzyStatus.TextColor = fuzzyColor;

        lblOperatorFuzzyScore.Text = $"Risiko fuzzy: {fuzzyResult.RiskScore:F1}%";

        lblOperatorFuzzyDetail.Text =
            $"Aman: {fuzzyResult.AmanDegree:F2} • " +
            $"Waspada: {fuzzyResult.WaspadaDegree:F2} • " +
            $"Bahaya: {fuzzyResult.BahayaDegree:F2}";

        lblOperatorNnStatus.Text = $"Status: {neuralResult.Status}";
        lblOperatorNnStatus.TextColor = neuralColor;

        lblOperatorNnScore.Text = $"Risiko NN: {neuralResult.RiskScore:F1}%";

        lblOperatorNnDetail.Text = neuralResult.Explanation;

        // Tampilan WARGA SIPIL
        lblPublicFuzzyStatus.Text =
            $"{fuzzyResult.Status} ({fuzzyResult.RiskScore:F0}%)";
        lblPublicFuzzyStatus.TextColor = fuzzyColor;

        lblPublicNnStatus.Text =
            $"{neuralResult.Status} ({neuralResult.RiskScore:F0}%)";
        lblPublicNnStatus.TextColor = neuralColor;

        lblPublicAiSummary.Text = BuildPublicAiSummary(
            fuzzyResult,
            neuralResult
        );
    }

    private Color GetStatusColor(string status)
    {
        if (status == "BAHAYA")
            return Color.FromArgb("#B91C1C");

        if (status == "WASPADA")
            return Color.FromArgb("#E8752F");

        return Color.FromArgb("#15803D");
    }

    private string BuildPublicAiSummary(
    FuzzyFireResult fuzzyResult,
    NeuralFireResult neuralResult)
    {
        double averageRisk =
            (fuzzyResult.RiskScore + neuralResult.RiskScore) / 2.0;

        if (averageRisk >= 75)
        {
            return
                $"AI menilai kondisi saat ini berisiko tinggi. " +
                $"Tetap tenang, hindari area berasap atau panas, dan ikuti arahan operator.";
        }

        if (averageRisk >= 45)
        {
            return
                $"AI menilai kondisi saat ini perlu diwaspadai. " +
                $"Pantau informasi dari sistem dan bersiap mengikuti instruksi evakuasi jika diperlukan.";
        }

        return
            $"AI menilai kondisi saat ini masih relatif aman. " +
            $"Tetap waspada dan ikuti informasi terbaru dari sistem.";
    }

    protected override void OnDisappearing()
    {
        _isRunning = false;
        base.OnDisappearing();
    }

    private void OnChatEntryCompleted(object sender, EventArgs e)
    {
        ProcessChatMessage();
    }

    private void OnSendChatClicked(object sender, EventArgs e)
    {
        ProcessChatMessage();
    }

    // Tambahkan kata "async" di sini karena kita akan memanggil server AI
    private async void ProcessChatMessage()
    {
        // Ambil teks dari kolom chat sesuai komponen asli Anda
        string question = EntryChat.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(question))
            return;

        if (question.ToLower().Contains("database") ||
             question.ToLower().Contains("cek database") ||
            question.ToLower().Contains("db path"))
        {
            AddChatBubble(question, true);
            EntryChat.Text = "";

            string dbReport = await GetDatabaseTestReportAsync();

            AddChatBubble(dbReport, false);
            return;
        }

        if (IsIdentityQuestion(question))
        {
            AddChatBubble(question, true);
            EntryChat.Text = "";

            if (IsLocalAiAnalysisCommand(question))
            {
                string localAiAnswer = GenerateNlpResponse(question);

                await FireDatabase.Instance.SaveChatLogAsync(
                    new ChatLog
                    {
                        Username = _username,
                        Role = _role,
                        Question = question,
                        Answer = localAiAnswer,
                        Source = "Fuzzy/Neural Network Lokal",
                        CreatedAt = DateTime.Now
                    }
                );

                AddChatBubble(localAiAnswer, false);
                return;
            }

            AddChatBubble(GetIdentityResponse(), false);
            return;
        }

        // 1. Munculkan teks ketikan operator ke layar
        AddChatBubble(question, true);

        // 2. Kosongkan kolom input
        EntryChat.Text = "";

        if (IsLocalAiAnalysisCommand(question))
        {
            string localAiAnswer = GenerateNlpResponse(question);

            await FireDatabase.Instance.SaveChatLogAsync(
                new ChatLog
                {
                    Username = _username,
                    Role = _role,
                    Question = question,
                    Answer = localAiAnswer,
                    Source = "Fuzzy/Neural Network Lokal",
                    CreatedAt = DateTime.Now
                }
            );

            AddChatBubble(localAiAnswer, false);
            return;
        }

        if (IsBroadcastCommand(question))
        {
            // Jika ya, gunakan sistem broadcast bawaan Anda
            string response = ProcessBroadcastCommand(question);
            AddChatBubble(response, false);
        }
        else
        {
            // Jika tidak, berarti operator sedang bertanya. Kita panggil Gemini!

            // Munculkan chat sementara agar operator tahu AI sedang mikir
            AddChatBubble("Sedang menganalisis kondisi lingkungan...", false);

            // Panggil Gemini AI dengan data sensor real-time
            string jawabanGemini = await _geminiService.TanyaGeminiAsync(
    question,
    _currentSuhu,
    _currentAsap,
    _currentHum,
    _currentStatus,
    _currentDanger
);

            if (jawabanGemini.StartsWith("Gemini gagal") ||
                jawabanGemini.StartsWith("Terjadi error") ||
                jawabanGemini.StartsWith("Koneksi") ||
                jawabanGemini.StartsWith("API key"))
            {
                jawabanGemini =
                    "Mode fallback lokal aktif karena Gemini tidak dapat dihubungi.\n\n" +
                    GenerateNlpResponse(question);
            }

            // Hapus chat sementara "Sedang menganalisis..." tadi
            ChatConversation.Children.RemoveAt(ChatConversation.Children.Count - 1);

            string source = jawabanGemini.StartsWith("Mode fallback lokal aktif")
    ? "Fallback Lokal"
    : "Gemini";

            await FireDatabase.Instance.SaveChatLogAsync(
                new ChatLog
                {
                    Username = _username,
                    Role = _role,
                    Question = question,
                    Answer = jawabanGemini,
                    Source = source,
                    CreatedAt = DateTime.Now
                }
            );

            AddChatBubble(jawabanGemini, false);
        }
    }

    private async Task<string> GetDatabaseTestReportAsync()
    {
        string dbPath = await FireDatabase.Instance.GetDatabasePathAsync();

        var sensorLogs = await FireDatabase.Instance.GetLatestSensorReadingsAsync(5);
        var chatLogs = await FireDatabase.Instance.GetLatestChatLogsAsync(5);
        var lastBroadcast = await FireDatabase.Instance.GetLastBroadcastMessageAsync();

        string broadcastInfo = lastBroadcast == null
            ? "Belum ada broadcast tersimpan."
            : $"Broadcast terakhir: {lastBroadcast.Message} ({lastBroadcast.TimeText})";

        return
            $"Database SQLite aktif.\n\n" +
            $"Lokasi database:\n{dbPath}\n\n" +
            $"Jumlah data sensor terbaru yang terbaca: {sensorLogs.Count}\n" +
            $"Jumlah chat log terbaru yang terbaca: {chatLogs.Count}\n" +
            $"{broadcastInfo}";
    }

    private bool IsIdentityQuestion(string input)
    {
        string text = input.ToLower().Trim();

        return text.Contains("kamu siapa") ||
               text.Contains("siapa kamu") ||
               text.Contains("anda siapa") ||
               text.Contains("siapa anda") ||
               text.Contains("nama kamu") ||
               text.Contains("namamu siapa") ||
               text.Contains("ini ai apa") ||
               text.Contains("asisten apa") ||
               text.Contains("who are you");
    }

    private string GetIdentityResponse()
    {
        return
            "Saya FIRE Assistant, chatbot AI pada dashboard monitoring kebakaran. " +
            "Tugas saya membantu operator membaca data suhu, kadar asap, kelembapan, status bahaya, " +
            "serta memberikan rekomendasi tindakan keselamatan berdasarkan kondisi sensor.";
    }

    private void AddChatBubble(string message, bool isUser)
    {
        var bubble = new Border
        {
            BackgroundColor = isUser
                ? Color.FromArgb("#1E3A5F")
                : Color.FromArgb("#F6F7F9"),
            StrokeThickness = 0,
            Padding = new Thickness(14),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(14)
            },
            HorizontalOptions = isUser
                ? LayoutOptions.End
                : LayoutOptions.Start,
            MaximumWidthRequest = 420,
            Content = new Label
            {
                Text = message,
                FontSize = 13,
                TextColor = isUser
                    ? Colors.White
                    : Color.FromArgb("#1F2933"),
                LineHeight = 1.25,
                LineBreakMode = LineBreakMode.WordWrap
            }
        };

        ChatConversation.Children.Add(bubble);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);

            if (ChatScrollView != null)
            {
                await ChatScrollView.ScrollToAsync(
                    bubble,
                    ScrollToPosition.End,
                    true
                );
            }
        });
    }

    private bool IsBroadcastCommand(string input)
    {
        string text = input.ToLower();

        return text.StartsWith("kirim warga:") ||
               text.StartsWith("kirim ke warga:") ||
               text.StartsWith("umumkan:") ||
               text.StartsWith("broadcast:");
    }

    private bool IsLocalAiAnalysisCommand(string input)
    {
        string text = input.ToLower();

        return text.Contains("fuzzy") ||
               text.Contains("neural") ||
               text.Contains("network") ||
               text.Contains("nn") ||
               text.Contains("jaringan saraf") ||
               text.Contains("jaringan syaraf") ||
               text.Contains("prediksi risiko") ||
               text.Contains("prediksi kebakaran") ||
               text.Contains("analisis ai");
    }

    private string ProcessBroadcastCommand(string input)
    {
        string message = input;

        message = message.Replace("kirim warga:", "", StringComparison.OrdinalIgnoreCase);
        message = message.Replace("kirim ke warga:", "", StringComparison.OrdinalIgnoreCase);
        message = message.Replace("umumkan:", "", StringComparison.OrdinalIgnoreCase);
        message = message.Replace("broadcast:", "", StringComparison.OrdinalIgnoreCase);

        message = message.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return "Format pesan belum lengkap. Contoh: kirim warga: Mohon tetap tenang dan ikuti arahan petugas.";
        }

        string time = DateTime.Now.ToString("HH:mm");

        Preferences.Set("operator_broadcast_message", message);
        Preferences.Set("operator_broadcast_time", time);

        _ = FireDatabase.Instance.SaveBroadcastMessageAsync(
    new BroadcastMessage
    {
        SenderUsername = _username,
        Message = message,
        TimeText = time,
        CreatedAt = DateTime.Now
    }
);

        _lastOperatorMessage = message;
        _lastOperatorMessageTime = time;

        return
            $"Pesan berhasil dikirim ke dashboard WARGA SIPIL.\n\n" +
            $"Isi pesan:\n\"{message}\"\n\n" +
            $"Waktu pengiriman: {time}";
    }
    private string GenerateNlpResponse(string input)
    {
        string text = input.ToLower();
        if (text.Contains("fuzzy"))
        {
            return _fuzzyService.AnalyzeToText(
                _currentSuhu,
                _currentAsap,
                _currentHum
            );
        }

        if (text.Contains("neural") ||
            text.Contains("network") ||
            text.Contains("nn") ||
            text.Contains("jaringan saraf") ||
            text.Contains("jaringan syaraf"))
        {
            return _neuralService.PredictToText(
                _currentSuhu,
                _currentAsap,
                _currentHum
            );
        }

        if (text.Contains("prediksi risiko") ||
            text.Contains("prediksi kebakaran") ||
            text.Contains("analisis ai"))
        {
            string fuzzyText = _fuzzyService.AnalyzeToText(
                _currentSuhu,
                _currentAsap,
                _currentHum
            );

            string neuralText = _neuralService.PredictToText(
                _currentSuhu,
                _currentAsap,
                _currentHum
            );

            return
                $"{fuzzyText}\n\n" +
                "-------------------------\n\n" +
                $"{neuralText}";
        }
        if (text.Contains("database") || text.Contains("db path"))
        {
            string dbPath = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            "fire_monitoring.db3"
            );

            return $"Database SQLite aktif.\nLokasi file database:\n{dbPath}";
        }

        bool tanyaIdentitas =
    text.Contains("kamu siapa") ||
    text.Contains("siapa kamu") ||
    text.Contains("anda siapa") ||
    text.Contains("siapa anda") ||
    text.Contains("nama kamu") ||
    text.Contains("namamu siapa") ||
    text.Contains("ini ai apa") ||
    text.Contains("asisten apa") ||
    text.Contains("who are you");

        bool tanyaStatus =
            text.Contains("status") ||
            text.Contains("kondisi") ||
            text.Contains("keadaan") ||
            text.Contains("aman") ||
            text.Contains("bahaya");

        bool tanyaSuhu =
            text.Contains("suhu") ||
            text.Contains("panas") ||
            text.Contains("temperatur");

        bool tanyaAsap =
            text.Contains("asap") ||
            text.Contains("ppm") ||
            text.Contains("gas");

        bool tanyaKelembapan =
            text.Contains("kelembapan") ||
            text.Contains("lembap") ||
            text.Contains("humidity");

        bool tanyaRekomendasi =
            text.Contains("rekomendasi") ||
            text.Contains("saran") ||
            text.Contains("tindakan") ||
            text.Contains("apa yang harus") ||
            text.Contains("evakuasi");

        bool mintaLaporan =
            text.Contains("laporan") ||
            text.Contains("ringkasan") ||
            text.Contains("rekap");

        bool tanyaMetode =
            text.Contains("metode") ||
            text.Contains("nlp") ||
            text.Contains("cara kerja") ||
            text.Contains("analisis");

        if (tanyaIdentitas)
        {
            return GetIdentityResponse();
        }

        if (mintaLaporan)
        {
            return
                $"Laporan singkat sistem:\n\n" +
                $"- Status: {_currentStatus}\n" +
                $"- Suhu: {_currentSuhu}°C\n" +
                $"- Kadar asap: {_currentAsap} ppm\n" +
                $"- Kelembapan: {_currentHum}%\n" +
                $"- Tingkat bahaya: {_currentDanger}%\n\n" +
                $"{GetRecommendation()}";
        }

        if (tanyaRekomendasi)
        {
            return GetRecommendation();
        }

        if (tanyaSuhu)
        {
            return
                $"Suhu lingkungan saat ini adalah {_currentSuhu}°C. " +
                GetSuhuAnalysis();
        }

        if (tanyaAsap)
        {
            return
                $"Kadar asap saat ini adalah {_currentAsap} ppm. " +
                GetAsapAnalysis();
        }

        if (tanyaKelembapan)
        {
            return
                $"Kelembapan saat ini adalah {_currentHum}%. " +
                GetHumidityAnalysis();
        }

        if (tanyaStatus)
        {
            return
                $"Status sistem saat ini: {_currentStatus}. " +
                $"Tingkat bahaya berada pada nilai {_currentDanger}%. " +
                $"{GetRecommendation()}";
        }

        if (tanyaMetode)
        {
            return
                "Chatbot ini menggunakan NLP sederhana berbasis intent dan kata kunci. " +
                "Pertanyaan operator dianalisis untuk mengenali maksud seperti status, suhu, asap, kelembapan, rekomendasi, dan laporan. " +
                "Jawaban kemudian dibuat berdasarkan data sensor terbaru pada dashboard.";
        }

        return
    "Saya belum memahami pertanyaan tersebut. " +
    "Coba gunakan pertanyaan seperti: 'status sekarang', 'berapa suhu', 'berapa asap', 'rekomendasi tindakan', atau 'buat laporan singkat'.\n\n" +
    "Untuk mengirim pesan ke warga sipil, gunakan format:\n" +
    "kirim warga: isi pesan operator";
    }

    private string GetRecommendation()
    {
        if (_currentDanger >= 75)
        {
            return
                "Rekomendasi: status BAHAYA. Operator disarankan segera melakukan verifikasi lokasi, mengaktifkan prosedur evakuasi, menghubungi petugas terkait, dan memberi peringatan kepada warga sipil.";
        }

        if (_currentDanger >= 45)
        {
            return
                "Rekomendasi: status WASPADA. Operator perlu memantau peningkatan suhu dan asap, memastikan jalur evakuasi siap, serta memberi informasi awal kepada warga jika parameter terus meningkat.";
        }

        return
            "Rekomendasi: status masih NORMAL. Sistem tetap perlu dipantau secara berkala dan operator memastikan sensor tetap aktif.";
    }

    private string GetSuhuAnalysis()
    {
        if (_currentSuhu >= 70)
            return "Nilai ini tergolong sangat tinggi dan dapat menjadi indikator kuat potensi kebakaran.";

        if (_currentSuhu >= 50)
            return "Nilai ini mulai tinggi dan perlu dipantau bersama parameter asap.";

        if (_currentSuhu >= 35)
            return "Nilai ini mulai meningkat, tetapi belum tentu menunjukkan kebakaran jika asap masih rendah.";

        return "Nilai ini masih berada pada kondisi relatif normal.";
    }

    private string GetAsapAnalysis()
    {
        if (_currentAsap >= 700)
            return "Kadar asap sangat tinggi dan perlu segera ditindaklanjuti.";

        if (_currentAsap >= 400)
            return "Kadar asap meningkat dan perlu dipantau dengan serius.";

        if (_currentAsap >= 250)
            return "Terdapat indikasi peningkatan asap ringan.";

        return "Kadar asap masih relatif rendah.";
    }

    private string GetHumidityAnalysis()
    {
        if (_currentHum <= 35)
            return "Kelembapan rendah dapat meningkatkan risiko penyebaran api.";

        if (_currentHum <= 45)
            return "Kelembapan mulai rendah, sehingga perlu dipantau bersama suhu.";

        return "Kelembapan masih cukup stabil.";
    }
    private void LoadOperatorBroadcastMessage()
    {
        _lastOperatorMessage = Preferences.Get("operator_broadcast_message", "");
        _lastOperatorMessageTime = Preferences.Get("operator_broadcast_time", "");

        if (string.IsNullOrWhiteSpace(_lastOperatorMessage))
        {
            lblOperatorMessage.Text = "Belum ada pesan dari operator.";
            lblOperatorMessageTime.Text = "-";
        }
        else
        {
            lblOperatorMessage.Text = _lastOperatorMessage;
            lblOperatorMessageTime.Text = _lastOperatorMessageTime;
        }
    }

    private void OnMenuMonitoringClicked(object sender, EventArgs e)
    {
        ShowMonitoringMenu();
    }

    private void OnMenuParameterSensorClicked(object sender, EventArgs e)
    {
        ShowParameterSensorMenu();
    }

    private void OnMenuStatusBahayaClicked(object sender, EventArgs e)
    {
        ShowStatusBahayaMenu();
    }

    private void OnMenuDataPenggunaClicked(object sender, EventArgs e)
    {
        ShowDataPenggunaMenu();
    }

    private void ShowMonitoringMenu()
    {
        _activeMenu = "MONITORING";

        DynamicMenuPanel.IsVisible = false;

        OperatorPanel.IsVisible = _role == "OPERATOR";
        WargaSipilPanel.IsVisible = _role != "OPERATOR";

        SetSidebarMenuActive("MONITORING");
    }

    private void ShowParameterSensorMenu()
    {
        _activeMenu = "PARAMETER";

        OperatorPanel.IsVisible = false;
        WargaSipilPanel.IsVisible = false;
        DynamicMenuPanel.IsVisible = true;

        SetSidebarMenuActive("PARAMETER");

        DynamicMenuContent.Children.Clear();

        DynamicMenuContent.Children.Add(CreateTitle(
            "Parameter Sensor",
            "Menu ini menampilkan nilai parameter lingkungan yang digunakan untuk mendeteksi potensi risiko kebakaran."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Suhu Lingkungan",
            $"{_currentSuhu}°C",
            "Suhu digunakan untuk membaca peningkatan panas pada area monitoring. Semakin tinggi suhu, semakin besar potensi risiko kebakaran."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Kadar Asap",
            $"{_currentAsap} ppm",
            "Kadar asap digunakan sebagai indikator adanya pembakaran atau asap di lingkungan. Nilai asap tinggi dapat meningkatkan status bahaya."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Kelembapan",
            $"{_currentHum}%",
            "Kelembapan digunakan untuk melihat kondisi udara. Kelembapan rendah dapat meningkatkan potensi penyebaran api."
        ));
    }

    private void ShowStatusBahayaMenu()
    {
        _activeMenu = "STATUS";

        OperatorPanel.IsVisible = false;
        WargaSipilPanel.IsVisible = false;
        DynamicMenuPanel.IsVisible = true;

        SetSidebarMenuActive("STATUS");

        DynamicMenuContent.Children.Clear();

        DynamicMenuContent.Children.Add(CreateTitle(
            "Status Bahaya",
            "Menu ini menampilkan hasil klasifikasi risiko kebakaran berdasarkan suhu, asap, kelembapan, Fuzzy Logic, dan Neural Network."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Status Saat Ini",
            _currentStatus,
            $"Sistem membaca suhu {_currentSuhu}°C, asap {_currentAsap} ppm, dan kelembapan {_currentHum}%. Hasil klasifikasi saat ini adalah {_currentStatus}."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Tingkat Bahaya",
            $"{_currentDanger}%",
            "Tingkat bahaya dihitung dari kombinasi skor suhu, kadar asap, dan kelembapan. Nilai yang semakin tinggi menunjukkan potensi kebakaran yang lebih besar."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Aturan Status",
            "NORMAL / WASPADA / BAHAYA",
            "Jika tingkat bahaya kurang dari 45%, status NORMAL. Jika 45% sampai 74%, status WASPADA. Jika 75% atau lebih, status BAHAYA."
        ));
    }

    private void ShowDataPenggunaMenu()
    {
        _activeMenu = "DATA";

        OperatorPanel.IsVisible = false;
        WargaSipilPanel.IsVisible = false;
        DynamicMenuPanel.IsVisible = true;

        SetSidebarMenuActive("DATA");

        DynamicMenuContent.Children.Clear();

        DynamicMenuContent.Children.Add(CreateTitle(
            "Data Pengguna",
            "Menu ini menampilkan informasi pengguna yang sedang login ke dalam sistem."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Username",
            _username,
            "Username merupakan identitas akun yang digunakan untuk masuk ke aplikasi."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Role Pengguna",
            _role,
            "Role menentukan hak akses pengguna. OPERATOR memiliki akses monitoring lengkap dan broadcast, sedangkan WARGA SIPIL menerima informasi keselamatan."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Metode Verifikasi",
            "Face Recognition",
            "Sistem menggunakan verifikasi wajah sebagai keamanan tambahan setelah login username dan password."
        ));

        DynamicMenuContent.Children.Add(CreateInfoCard(
            "Database",
            "SQLite Local Database",
            "Data akun, sensor, chatbot, dan broadcast disimpan menggunakan database lokal SQLite."
        ));
    }

    private void RefreshActiveMenu()
    {
        if (_activeMenu == "PARAMETER")
        {
            ShowParameterSensorMenu();
        }
        else if (_activeMenu == "STATUS")
        {
            ShowStatusBahayaMenu();
        }
        else if (_activeMenu == "DATA")
        {
            ShowDataPenggunaMenu();
        }
    }

    private void SetSidebarMenuActive(string menu)
    {
        SetMenuStyle(
            MenuMonitoring,
            LabelMenuMonitoring,
            DotMenuMonitoring,
            menu == "MONITORING"
        );

        SetMenuStyle(
            MenuParameterSensor,
            LabelMenuParameterSensor,
            DotMenuParameterSensor,
            menu == "PARAMETER"
        );

        SetMenuStyle(
            MenuStatusBahaya,
            LabelMenuStatusBahaya,
            DotMenuStatusBahaya,
            menu == "STATUS"
        );

        SetMenuStyle(
            MenuDataPengguna,
            LabelMenuDataPengguna,
            DotMenuDataPengguna,
            menu == "DATA"
        );
    }

    private void SetMenuStyle(Border menuBorder, Label menuLabel, BoxView menuDot, bool isActive)
    {
        if (isActive)
        {
            menuBorder.BackgroundColor = Color.FromArgb("#EAF0F6");
            menuLabel.TextColor = Color.FromArgb("#1E3A5F");
            menuLabel.FontAttributes = FontAttributes.Bold;
            menuDot.Color = Color.FromArgb("#1E3A5F");
        }
        else
        {
            menuBorder.BackgroundColor = Colors.Transparent;
            menuLabel.TextColor = Color.FromArgb("#667085");
            menuLabel.FontAttributes = FontAttributes.None;
            menuDot.Color = Color.FromArgb("#D9DEE7");
        }
    }

    private View CreateTitle(string title, string subtitle)
    {
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
        {
            new Label
            {
                Text = title,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1F2937")
            },
            new Label
            {
                Text = subtitle,
                FontSize = 14,
                TextColor = Color.FromArgb("#667085"),
                LineBreakMode = LineBreakMode.WordWrap
            },
            new BoxView
            {
                HeightRequest = 1,
                Color = Color.FromArgb("#E5E7EB"),
                Margin = new Thickness(0, 12, 0, 4)
            }
        }
        };
    }

    private View CreateInfoCard(string title, string value, string description)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb("#F6F7F9"),
            StrokeThickness = 0,
            Padding = new Thickness(18),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(14)
            },
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#667085")
                },
                new Label
                {
                    Text = value,
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#1E3A5F")
                },
                new Label
                {
                    Text = description,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#667085"),
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
            }
        };
    }

    private void OnMenuMonitoringTapped(object sender, TappedEventArgs e)
    {
        ShowMonitoringMenu();
    }

    private void OnMenuParameterSensorTapped(object sender, TappedEventArgs e)
    {
        ShowParameterSensorMenu();
    }

    private void OnMenuStatusBahayaTapped(object sender, TappedEventArgs e)
    {
        ShowStatusBahayaMenu();
    }

    private void OnMenuDataPenggunaTapped(object sender, TappedEventArgs e)
    {
        ShowDataPenggunaMenu();
    }
    private void OnLogoutClicked(object sender, EventArgs e)
    {
        _isRunning = false;

        Preferences.Remove("session_username");
        Preferences.Remove("session_role");

        Application.Current!.MainPage = new MainPage();
    }
}