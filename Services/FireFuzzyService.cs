namespace FIRE;

public class FuzzyFireResult
{
    public double AmanDegree { get; set; }
    public double WaspadaDegree { get; set; }
    public double BahayaDegree { get; set; }
    public double RiskScore { get; set; }
    public string Status { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public class FireFuzzyService
{
    public FuzzyFireResult Analyze(int suhu, int asap, int kelembapan)
    {
        // Fuzzifikasi suhu
        double suhuNormal = GradeDown(suhu, 30, 45);
        double suhuPanas = Triangle(suhu, 35, 55, 75);
        double suhuSangatPanas = GradeUp(suhu, 65, 85);

        // Fuzzifikasi asap
        double asapRendah = GradeDown(asap, 200, 400);
        double asapSedang = Triangle(asap, 250, 500, 750);
        double asapTinggi = GradeUp(asap, 650, 850);

        // Fuzzifikasi kelembapan
        double humKering = GradeDown(kelembapan, 35, 50);
        double humNormal = Triangle(kelembapan, 40, 60, 80);
        double humTinggi = GradeUp(kelembapan, 70, 90);

        // Aturan fuzzy:
        // Aman jika suhu normal, asap rendah, dan kelembapan normal/tinggi.
        double aman = Min3(
            suhuNormal,
            asapRendah,
            Math.Max(humNormal, humTinggi)
        );

        // Waspada jika salah satu parameter mulai meningkat.
        double waspada = MaxMany(
            suhuPanas,
            asapSedang,
            Min2(suhuPanas, humKering),
            Min2(asapSedang, humKering),
            Min2(suhuNormal, asapSedang)
        );

        // Bahaya jika suhu/asap tinggi atau lingkungan sangat kering.
        double bahaya = MaxMany(
            Min2(suhuSangatPanas, asapTinggi),
            Min2(suhuSangatPanas, humKering),
            Min2(asapTinggi, humKering),
            Min2(suhuPanas, asapTinggi)
        );

        // Defuzzifikasi sederhana memakai weighted average.
        // Aman = 20, Waspada = 55, Bahaya = 90.
        double totalDegree = aman + waspada + bahaya;

        double riskScore;

        if (totalDegree <= 0)
        {
            riskScore = 0;
        }
        else
        {
            riskScore =
                ((aman * 20) + (waspada * 55) + (bahaya * 90)) /
                totalDegree;
        }

        string status;

        if (riskScore >= 75)
            status = "BAHAYA";
        else if (riskScore >= 45)
            status = "WASPADA";
        else
            status = "AMAN";

        string explanation = BuildExplanation(
            suhu,
            asap,
            kelembapan,
            riskScore,
            status
        );

        return new FuzzyFireResult
        {
            AmanDegree = aman,
            WaspadaDegree = waspada,
            BahayaDegree = bahaya,
            RiskScore = riskScore,
            Status = status,
            Explanation = explanation
        };
    }

    public string AnalyzeToText(int suhu, int asap, int kelembapan)
    {
        FuzzyFireResult result = Analyze(suhu, asap, kelembapan);

        return
            $"Analisis Fuzzy Logic:\n\n" +
            $"- Suhu: {suhu}°C\n" +
            $"- Asap: {asap} ppm\n" +
            $"- Kelembapan: {kelembapan}%\n\n" +
            $"Derajat keanggotaan:\n" +
            $"- Aman: {result.AmanDegree:F2}\n" +
            $"- Waspada: {result.WaspadaDegree:F2}\n" +
            $"- Bahaya: {result.BahayaDegree:F2}\n\n" +
            $"Hasil defuzzifikasi: {result.RiskScore:F1}%\n" +
            $"Status fuzzy: {result.Status}\n\n" +
            $"{result.Explanation}";
    }

    private static string BuildExplanation(
        int suhu,
        int asap,
        int kelembapan,
        double riskScore,
        string status)
    {
        if (status == "BAHAYA")
        {
            return
                "Penjelasan awam: Sistem menilai kondisi berbahaya karena kombinasi suhu tinggi, asap meningkat, atau kelembapan rendah. " +
                "Operator disarankan segera memverifikasi lokasi, memberi peringatan, dan menyiapkan evakuasi.";
        }

        if (status == "WASPADA")
        {
            return
                "Penjelasan awam: Kondisi belum masuk bahaya tinggi, tetapi ada tanda peningkatan risiko. " +
                "Operator perlu terus memantau suhu, asap, dan kelembapan secara berkala.";
        }

        return
            "Penjelasan awam: Kondisi lingkungan masih relatif aman. " +
            "Suhu, asap, dan kelembapan belum menunjukkan risiko kebakaran yang tinggi.";
    }

    private static double GradeDown(double x, double start, double end)
    {
        if (x <= start)
            return 1.0;

        if (x >= end)
            return 0.0;

        return (end - x) / (end - start);
    }

    private static double GradeUp(double x, double start, double end)
    {
        if (x <= start)
            return 0.0;

        if (x >= end)
            return 1.0;

        return (x - start) / (end - start);
    }

    private static double Triangle(double x, double left, double center, double right)
    {
        if (x <= left || x >= right)
            return 0.0;

        if (x == center)
            return 1.0;

        if (x < center)
            return (x - left) / (center - left);

        return (right - x) / (right - center);
    }

    private static double Min2(double a, double b)
    {
        return Math.Min(a, b);
    }

    private static double Min3(double a, double b, double c)
    {
        return Math.Min(a, Math.Min(b, c));
    }

    private static double MaxMany(params double[] values)
    {
        double max = 0;

        foreach (double value in values)
        {
            if (value > max)
                max = value;
        }

        return max;
    }
}