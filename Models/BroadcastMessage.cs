using SQLite;

namespace FIRE;

public class BroadcastMessage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SenderUsername { get; set; } = "";

    public string Message { get; set; } = "";

    public string TimeText { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}