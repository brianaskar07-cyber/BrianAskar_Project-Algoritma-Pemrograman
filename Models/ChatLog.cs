using SQLite;

namespace FIRE;

public class ChatLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string Role { get; set; } = "";

    public string Question { get; set; } = "";

    public string Answer { get; set; } = "";

    public string Source { get; set; } = "";
    // Contoh isi:
    // Gemini
    // Fallback Lokal
    // NLP Lokal

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}