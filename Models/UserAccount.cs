using SQLite;

namespace FIRE;

public class UserAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string Username { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string Role { get; set; } = "";

    public string FacePhotoPath { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}