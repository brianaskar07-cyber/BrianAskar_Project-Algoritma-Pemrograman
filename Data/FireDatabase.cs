using SQLite;
using System.Security.Cryptography;
using System.Text;

namespace FIRE;

public class FireDatabase
{
    private static SQLiteAsyncConnection? _database;

    private static readonly Lazy<FireDatabase> _instance =
        new Lazy<FireDatabase>(() => new FireDatabase());

    public static FireDatabase Instance => _instance.Value;

    private FireDatabase()
    {
    }

    private async Task InitAsync()
    {
        if (_database != null)
            return;

        string dbPath = System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            "fire_monitoring.db3"
        );

        _database = new SQLiteAsyncConnection(dbPath);

        await _database.CreateTableAsync<UserAccount>();
        await _database.CreateTableAsync<SensorReading>();
        await _database.CreateTableAsync<ChatLog>();
        await _database.CreateTableAsync<BroadcastMessage>();
    }

    public async Task<string> GetDatabasePathAsync()
    {
        await InitAsync();

        return System.IO.Path.Combine(
            FileSystem.AppDataDirectory,
            "fire_monitoring.db3"
        );
    }

    public async Task<int> GetUserCountAsync()
    {
        await InitAsync();

        return await _database!
            .Table<UserAccount>()
            .CountAsync();
    }

    public async Task<string> ExportDatabaseToDesktopAsync()
    {
        await InitAsync();

        string sourcePath = await GetDatabasePathAsync();

        try
        {
            await _database!.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE);");
        }
        catch
        {
            // Abaikan jika SQLite tidak memakai WAL.
        }

        await _database!.CloseAsync();
        _database = null;

        string desktopPath = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory
        );

        string exportPath = System.IO.Path.Combine(
            desktopPath,
            "fire_monitoring_export.db3"
        );

        File.Copy(sourcePath, exportPath, true);

        return exportPath;
    }

    public static string HashPassword(string password)
    {
        byte[] bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(password)
        );

        return Convert.ToHexString(bytes);
    }

    public static bool VerifyPassword(string inputPassword, string storedHash)
    {
        string inputHash = HashPassword(inputPassword);
        return inputHash == storedHash;
    }

    public async Task<bool> RegisterUserAsync(
        string username,
        string password,
        string role,
        string facePhotoPath)
    {
        await InitAsync();

        username = username.Trim();

        var existingUser = await _database!
            .Table<UserAccount>()
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();

        if (existingUser != null)
            return false;

        var user = new UserAccount
        {
            Username = username,
            PasswordHash = HashPassword(password),
            Role = role,
            FacePhotoPath = facePhotoPath,
            CreatedAt = DateTime.Now
        };

        int rows = await _database.InsertAsync(user);

        return rows > 0;
    }

    public async Task<UserAccount?> GetUserByUsernameAsync(string username)
    {
        await InitAsync();

        username = username.Trim();

        return await _database!
            .Table<UserAccount>()
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ValidateLoginAsync(string username, string password)
    {
        await InitAsync();

        var user = await GetUserByUsernameAsync(username);

        if (user == null)
            return false;

        return VerifyPassword(password, user.PasswordHash);
    }

    public async Task SaveSensorReadingAsync(SensorReading reading)
    {
        await InitAsync();

        reading.CreatedAt = DateTime.Now;

        await _database!.InsertAsync(reading);
    }

    public async Task<List<SensorReading>> GetLatestSensorReadingsAsync(int limit = 20)
    {
        await InitAsync();

        return await _database!
            .Table<SensorReading>()
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task SaveChatLogAsync(ChatLog log)
    {
        await InitAsync();

        log.CreatedAt = DateTime.Now;

        await _database!.InsertAsync(log);
    }

    public async Task<List<ChatLog>> GetLatestChatLogsAsync(int limit = 20)
    {
        await InitAsync();

        return await _database!
            .Table<ChatLog>()
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task SaveBroadcastMessageAsync(BroadcastMessage message)
    {
        await InitAsync();

        message.CreatedAt = DateTime.Now;

        await _database!.InsertAsync(message);
    }

    public async Task<BroadcastMessage?> GetLastBroadcastMessageAsync()
    {
        await InitAsync();

        return await _database!
            .Table<BroadcastMessage>()
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();
    }
}