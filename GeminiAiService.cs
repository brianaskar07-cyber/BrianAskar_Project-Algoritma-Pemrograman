using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FIRE
{
    public class GeminiAiService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Gunakan model yang aktif di Google AI Studio kamu.
        // Dokumentasi Gemini saat ini mencontohkan gemini-3.5-flash untuk text generation.
        // Kalau akun kamu masih memakai gemini-2.5-flash, ganti nilainya di sini.
        private const string ModelName = "gemini-2.5-flash-lite";

        // Untuk prototype boleh isi langsung, tetapi untuk versi aman gunakan Environment Variable.
        private readonly string _apiKey;

        public GeminiAiService()
        {
            _apiKey = "AQ.Ab8RN6KJOb-RzyS1LjgIxit7_zbwvCYkK_8DW4vf-cPKBXkDCQ";
        }

        public async Task<string> TanyaGeminiAsync(
            string pesanUser,
            int suhu,
            int asap,
            int kelembapan,
            string statusTriase,
            int tingkatBahaya,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return "API key Gemini belum diatur. Masukkan API key terlebih dahulu.";
            }

            try
            {
                string url =
                    $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent";

                string systemInstruction =
                    "Kamu adalah FIRE Assistant, chatbot AI pada dashboard monitoring kebakaran. " +
                    "Jika ditanya identitas seperti 'kamu siapa', jawab bahwa kamu adalah FIRE Assistant. " +
                    "Tugasmu membantu operator membaca data suhu, asap, kelembapan, status bahaya, dan rekomendasi keselamatan. " +
                    "Jawab sesuai pertanyaan operator, jangan selalu memaksa menjawab laporan sensor jika pertanyaannya bukan tentang sensor. " +
                    "Gunakan Bahasa Indonesia yang singkat, jelas, dan solutif.";

                string prompt =
                    $"Sensor: suhu {suhu}C, asap {asap}ppm, kelembapan {kelembapan}%, " +
                    $"status {statusTriase}, bahaya {tingkatBahaya}%.\n" +
                    $"Pertanyaan operator: {pesanUser}";

                var payload = new
                {
                    system_instruction = new
                    {
                        parts = new[]
                        {
                            new { text = systemInstruction }
                        }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.3,
                        maxOutputTokens = 350
                    }
                };

                string jsonPayload = JsonSerializer.Serialize(payload);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using HttpResponseMessage response =
                    await _httpClient.SendAsync(request, cancellationToken);

                string responseBody =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return $"Gemini gagal merespons. Status: {(int)response.StatusCode} {response.StatusCode}\n\nDetail server:\n{responseBody}";
                }

                using JsonDocument doc = JsonDocument.Parse(responseBody);

                if (doc.RootElement.TryGetProperty("candidates", out JsonElement candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    JsonElement candidate = candidates[0];

                    string? finishReason = "";

                    if (candidate.TryGetProperty("finishReason", out JsonElement finishElement))
                    {
                        finishReason = finishElement.GetString();
                    }

                    if (candidate.TryGetProperty("content", out JsonElement content) &&
                        content.TryGetProperty("parts", out JsonElement parts) &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out JsonElement textElement))
                    {
                        string? jawabanAI = textElement.GetString();

                        if (string.IsNullOrWhiteSpace(jawabanAI))
                        {
                            return "Gemini tidak memberikan jawaban teks.";
                        }

                        if (finishReason == "MAX_TOKENS")
                        {
                            return jawabanAI.Trim() +
                                   "\n\n[Catatan: Jawaban AI terpotong karena batas token. Naikkan nilai maxOutputTokens di GeminiAiService.cs.]";
                        }

                        return jawabanAI.Trim();
                    }

                    return $"Gemini tidak mengembalikan teks jawaban. Finish reason: {finishReason}";
                }

                return "Gemini tidak mengembalikan kandidat jawaban.";
            }
            catch (TaskCanceledException)
            {
                return "Koneksi ke Gemini terlalu lama. Periksa internet atau coba lagi.";
            }
            catch (Exception ex)
            {
                return $"Terjadi error saat menghubungi Gemini: {ex.Message}";
            }
        }
    }
}