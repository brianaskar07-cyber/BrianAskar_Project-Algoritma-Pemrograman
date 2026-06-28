using SQLite;

namespace FIRE;

public class SensorReading
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int Suhu { get; set; }

    public int Asap { get; set; }

    public int Kelembapan { get; set; }

    public int DangerLevel { get; set; }

    public string Status { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}