using System.Text.Json;
using Dapper;
using MeetingRoomBooking.Api.Models;
using Microsoft.Data.Sqlite;

namespace MeetingRoomBooking.Api.Services;

/// <summary>
/// Loads JSON files as the source-of-truth tables into an in-memory SQLite database.
/// Dapper provides the query/mutation layer; every write is persisted back to JSON.
/// </summary>
public sealed class JsonDatabase : IDisposable
{
    private readonly string _dataDirectory;
    private readonly string _usersFile;
    private readonly string _roomsFile;
    private readonly string _bookingsFile;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private readonly SqliteConnection _connection;

    public JsonDatabase(IHostEnvironment environment)
    {
        _dataDirectory = Path.Combine(environment.ContentRootPath, "Data", "seed");
        _usersFile = Path.Combine(_dataDirectory, "users.json");
        _roomsFile = Path.Combine(_dataDirectory, "rooms.json");
        _bookingsFile = Path.Combine(_dataDirectory, "bookings.json");

        Directory.CreateDirectory(_dataDirectory);
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        CreateSchema();
        LoadJsonTables();
    }

    public SqliteConnection Connection => _connection;

    private void CreateSchema()
    {
        _connection.Execute("""
            CREATE TABLE Users (
                Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Email TEXT NOT NULL UNIQUE,
                Role TEXT NOT NULL, PasswordHash TEXT NOT NULL, PasswordSalt TEXT NOT NULL, CreatedAt TEXT NOT NULL
            );
            CREATE TABLE Rooms (
                Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Floor INTEGER NOT NULL, Capacity INTEGER NOT NULL,
                AmenitiesJson TEXT NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL
            );
            CREATE TABLE Bookings (
                Id TEXT PRIMARY KEY, RoomId TEXT NOT NULL, UserId TEXT NOT NULL, Title TEXT NOT NULL,
                StartTime TEXT NOT NULL, EndTime TEXT NOT NULL, Status TEXT NOT NULL, Attendees INTEGER NOT NULL,
                Notes TEXT NOT NULL, CreatedAt TEXT NOT NULL
            );
            """);
    }

    private void LoadJsonTables()
    {
        var users = ReadJson(_usersFile, new List<UserRecord>());
        var rooms = ReadJson(_roomsFile, new List<RoomRecord>());
        var bookings = ReadJson(_bookingsFile, new List<BookingRecord>());

        foreach (var user in users)
        {
            _connection.Execute("INSERT INTO Users (Id, Name, Email, Role, PasswordHash, PasswordSalt, CreatedAt) VALUES (@Id, @Name, @Email, @Role, @PasswordHash, @PasswordSalt, @CreatedAt)", user);
        }

        foreach (var room in rooms)
        {
            _connection.Execute("INSERT INTO Rooms (Id, Name, Floor, Capacity, AmenitiesJson, Status, CreatedAt) VALUES (@Id, @Name, @Floor, @Capacity, @AmenitiesJson, @Status, @CreatedAt)", new
            {
                room.Id, room.Name, room.Floor, room.Capacity,
                AmenitiesJson = JsonSerializer.Serialize(room.Amenities, _jsonOptions), room.Status, room.CreatedAt
            });
        }

        foreach (var booking in bookings)
        {
            _connection.Execute("INSERT INTO Bookings (Id, RoomId, UserId, Title, StartTime, EndTime, Status, Attendees, Notes, CreatedAt) VALUES (@Id, @RoomId, @UserId, @Title, @StartTime, @EndTime, @Status, @Attendees, @Notes, @CreatedAt)", booking);
        }
    }

    public async Task PersistAsync()
    {
        await _persistLock.WaitAsync();
        try
        {
            var users = (await _connection.QueryAsync<UserRecord>("SELECT Id, Name, Email, Role, PasswordHash, PasswordSalt, CreatedAt FROM Users ORDER BY Name")).ToList();
            var roomRows = (await _connection.QueryAsync<RoomRow>("SELECT Id, Name, Floor, Capacity, AmenitiesJson, Status, CreatedAt FROM Rooms ORDER BY Name")).ToList();
            var bookings = (await _connection.QueryAsync<BookingRecord>("SELECT Id, RoomId, UserId, Title, StartTime, EndTime, Status, Attendees, Notes, CreatedAt FROM Bookings ORDER BY StartTime")).ToList();
            var rooms = roomRows.Select(MapRoom).ToList();

            await File.WriteAllTextAsync(_usersFile, JsonSerializer.Serialize(users, _jsonOptions));
            await File.WriteAllTextAsync(_roomsFile, JsonSerializer.Serialize(rooms, _jsonOptions));
            await File.WriteAllTextAsync(_bookingsFile, JsonSerializer.Serialize(bookings, _jsonOptions));
        }
        finally
        {
            _persistLock.Release();
        }
    }

    public static RoomResponse MapRoom(RoomRow row)
    {
        var amenities = JsonSerializer.Deserialize<List<string>>(row.AmenitiesJson) ?? [];
        return new RoomResponse(row.Id, row.Name, row.Floor, row.Capacity, amenities, row.Status, row.CreatedAt);
    }

    public static RoomRecord MapRoomRecord(RoomRow row) => new()
    {
        Id = row.Id, Name = row.Name, Floor = row.Floor, Capacity = row.Capacity,
        Amenities = JsonSerializer.Deserialize<List<string>>(row.AmenitiesJson) ?? [], Status = row.Status, CreatedAt = row.CreatedAt
    };

    private static T ReadJson<T>(string file, T fallback)
    {
        if (!File.Exists(file))
        {
            File.WriteAllText(file, JsonSerializer.Serialize(fallback, new JsonSerializerOptions { WriteIndented = true }));
            return fallback;
        }

        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? fallback;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _persistLock.Dispose();
    }
}
