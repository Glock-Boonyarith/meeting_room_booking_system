using System.Globalization;
using System.Security.Claims;
using Dapper;
using MeetingRoomBooking.Api.Models;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CrudController : ControllerBase
{
    private readonly JsonDatabase _database;

    public CrudController(JsonDatabase database) => _database = database;

    [HttpGet("rooms")]
    public async Task<ActionResult<IReadOnlyList<RoomResponse>>> GetRooms()
    {
        var rows = await _database.Connection.QueryAsync<RoomRow>(
            "SELECT Id, Name, Floor, Capacity, AmenitiesJson, Status, CreatedAt FROM Rooms ORDER BY Floor, Name");
        return Ok(rows.Select(JsonDatabase.MapRoom).ToList());
    }

    [HttpGet("rooms/{id}")]
    public async Task<ActionResult<RoomResponse>> GetRoom(string id)
    {
        var row = await FindRoom(id);
        return row is null ? NotFound(new { message = "Room not found." }) : Ok(JsonDatabase.MapRoom(row));
    }

    [HttpPost("rooms")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomResponse>> CreateRoom(RoomRequest request)
    {
        var validation = ValidateRoom(request);
        if (validation is not null) return BadRequest(new { message = validation });

        var room = new RoomRecord
        {
            Id = Guid.NewGuid().ToString("N"), Name = request.Name.Trim(), Floor = request.Floor,
            Capacity = request.Capacity, Amenities = request.Amenities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList(),
            Status = NormalizeRoomStatus(request.Status), CreatedAt = UtcNow()
        };

        await _database.Connection.ExecuteAsync(
            "INSERT INTO Rooms (Id, Name, Floor, Capacity, AmenitiesJson, Status, CreatedAt) VALUES (@Id, @Name, @Floor, @Capacity, @AmenitiesJson, @Status, @CreatedAt)",
            new { room.Id, room.Name, room.Floor, room.Capacity, AmenitiesJson = System.Text.Json.JsonSerializer.Serialize(room.Amenities), room.Status, room.CreatedAt });
        await _database.PersistAsync();

        var response = new RoomResponse(room.Id, room.Name, room.Floor, room.Capacity, room.Amenities, room.Status, room.CreatedAt);
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, response);
    }

    [HttpPut("rooms/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RoomResponse>> UpdateRoom(string id, RoomRequest request)
    {
        var validation = ValidateRoom(request);
        if (validation is not null) return BadRequest(new { message = validation });

        var existing = await FindRoom(id);
        if (existing is null) return NotFound(new { message = "Room not found." });

        await _database.Connection.ExecuteAsync(
            "UPDATE Rooms SET Name = @Name, Floor = @Floor, Capacity = @Capacity, AmenitiesJson = @AmenitiesJson, Status = @Status WHERE Id = @Id",
            new
            {
                Id = id, Name = request.Name.Trim(), request.Floor, request.Capacity,
                AmenitiesJson = System.Text.Json.JsonSerializer.Serialize(request.Amenities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList()),
                Status = NormalizeRoomStatus(request.Status)
            });
        await _database.PersistAsync();
        var updated = await FindRoom(id);
        return Ok(JsonDatabase.MapRoom(updated!));
    }

    [HttpDelete("rooms/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRoom(string id)
    {
        var room = await FindRoom(id);
        if (room is null) return NotFound(new { message = "Room not found." });

        var bookingCount = await _database.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Bookings WHERE RoomId = @Id", new { Id = id });
        if (bookingCount > 0) return Conflict(new { message = "Room cannot be deleted while it has booking history. Mark it as Maintenance instead." });

        await _database.Connection.ExecuteAsync("DELETE FROM Rooms WHERE Id = @Id", new { Id = id });
        await _database.PersistAsync();
        return NoContent();
    }

    [HttpGet("bookings")]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> GetBookings(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] string? roomId = null,
        [FromQuery] string? status = null)
    {
        var sql = BookingSql("WHERE 1 = 1");
        var parameters = new DynamicParameters();
        AddBookingFilters(ref sql, parameters, from, to, roomId, status);
        sql += " ORDER BY b.StartTime";
        var rows = await _database.Connection.QueryAsync<BookingWithDetails>(sql, parameters);
        return Ok(rows.Select(MapBooking).ToList());
    }

    [HttpGet("bookings/{id}")]
    public async Task<ActionResult<BookingResponse>> GetBooking(string id)
    {
        var booking = await FindBooking(id);
        return booking is null ? NotFound(new { message = "Booking not found." }) : Ok(MapBooking(booking));
    }

    [HttpPost("bookings")]
    public async Task<ActionResult<BookingResponse>> CreateBooking(BookingRequest request)
    {
        var validation = await ValidateBooking(request.RoomId, request.Title, request.StartTime, request.EndTime, request.Attendees, null);
        if (validation is not null) return BadRequest(new { message = validation });

        var start = ParseUtc(request.StartTime);
        var end = ParseUtc(request.EndTime);
        var userId = CurrentUserId();
        var booking = new BookingRecord
        {
            Id = Guid.NewGuid().ToString("N"), RoomId = request.RoomId, UserId = userId,
            Title = request.Title.Trim(), StartTime = start.ToString("O"), EndTime = end.ToString("O"),
            Status = "Confirmed", Attendees = request.Attendees, Notes = request.Notes?.Trim() ?? string.Empty, CreatedAt = UtcNow()
        };

        await _database.Connection.ExecuteAsync("INSERT INTO Bookings (Id, RoomId, UserId, Title, StartTime, EndTime, Status, Attendees, Notes, CreatedAt) VALUES (@Id, @RoomId, @UserId, @Title, @StartTime, @EndTime, @Status, @Attendees, @Notes, @CreatedAt)", booking);
        await _database.PersistAsync();
        var created = await FindBooking(booking.Id);
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, MapBooking(created!));
    }

    [HttpPut("bookings/{id}")]
    public async Task<ActionResult<BookingResponse>> UpdateBooking(string id, BookingUpdateRequest request)
    {
        var existing = await FindBooking(id);
        if (existing is null) return NotFound(new { message = "Booking not found." });
        if (!CanManage(existing.UserId)) return Forbid();

        if (!TryParseUtc(request.StartTime ?? existing.StartTime, out var start) || !TryParseUtc(request.EndTime ?? existing.EndTime, out var end))
        {
            return BadRequest(new { message = "Start and end times must be valid dates." });
        }
        var title = string.IsNullOrWhiteSpace(request.Title) ? existing.Title : request.Title.Trim();
        var attendees = request.Attendees ?? existing.Attendees;
        var status = NormalizeBookingStatus(request.Status ?? existing.Status);
        var validation = await ValidateBooking(existing.RoomId, title, start.ToString("O"), end.ToString("O"), attendees, id);
        if (validation is not null) return BadRequest(new { message = validation });

        await _database.Connection.ExecuteAsync("UPDATE Bookings SET Title = @Title, StartTime = @StartTime, EndTime = @EndTime, Status = @Status, Attendees = @Attendees, Notes = @Notes WHERE Id = @Id", new
        {
            Id = id, Title = title, StartTime = start.ToString("O"), EndTime = end.ToString("O"), Status = status,
            Attendees = attendees, Notes = request.Notes?.Trim() ?? existing.Notes
        });
        await _database.PersistAsync();
        return Ok(MapBooking((await FindBooking(id))!));
    }

    [HttpDelete("bookings/{id}")]
    public async Task<IActionResult> CancelBooking(string id)
    {
        var existing = await FindBooking(id);
        if (existing is null) return NotFound(new { message = "Booking not found." });
        if (!CanManage(existing.UserId)) return Forbid();

        await _database.Connection.ExecuteAsync("UPDATE Bookings SET Status = 'Cancelled' WHERE Id = @Id", new { Id = id });
        await _database.PersistAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> Dashboard([FromQuery] string? date = null)
    {
        var day = ParseDate(date);
        var from = day.ToString("yyyy-MM-dd'T'00:00:00.0000000'Z'", CultureInfo.InvariantCulture);
        var to = day.AddDays(1).ToString("yyyy-MM-dd'T'00:00:00.0000000'Z'", CultureInfo.InvariantCulture);

        var rooms = (await _database.Connection.QueryAsync<RoomRow>("SELECT Id, Name, Floor, Capacity, AmenitiesJson, Status, CreatedAt FROM Rooms ORDER BY Floor, Name")).Select(JsonDatabase.MapRoom).ToList();
        var bookings = await _database.Connection.QueryAsync<BookingWithDetails>(BookingSql("WHERE b.StartTime < @To AND b.EndTime > @From"), new { From = from, To = to });
        var bookingResponses = bookings.OrderBy(b => b.StartTime).Select(MapBooking).ToList();
        var userId = CurrentUserId();
        var stats = new DashboardStats(
            rooms.Count,
            rooms.Count(r => string.Equals(r.Status, "Available", StringComparison.OrdinalIgnoreCase)),
            bookingResponses.Count(b => b.Status != "Cancelled"),
            bookingResponses.Count(b => b.UserId == userId && b.Status != "Cancelled"));

        return Ok(new DashboardResponse(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), rooms, bookingResponses, stats));
    }

    private async Task<RoomRow?> FindRoom(string id) => await _database.Connection.QuerySingleOrDefaultAsync<RoomRow>("SELECT Id, Name, Floor, Capacity, AmenitiesJson, Status, CreatedAt FROM Rooms WHERE Id = @Id", new { Id = id });

    private async Task<BookingWithDetails?> FindBooking(string id) => await _database.Connection.QuerySingleOrDefaultAsync<BookingWithDetails>(BookingSql("WHERE b.Id = @Id"), new { Id = id });

    private async Task<string?> ValidateBooking(string roomId, string title, string startText, string endText, int attendees, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(title)) return "Room and title are required.";
        if (!DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start) ||
            !DateTimeOffset.TryParse(endText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var end)) return "Start and end times must be valid dates.";
        if (end <= start) return "End time must be after start time.";
        if (attendees < 1) return "At least one attendee is required.";

        var room = await FindRoom(roomId);
        if (room is null) return "Room not found.";
        if (!string.Equals(room.Status, "Available", StringComparison.OrdinalIgnoreCase)) return "This room is not available for booking.";
        if (attendees > room.Capacity) return $"This room supports up to {room.Capacity} attendees.";

        var overlapSql = "SELECT COUNT(*) FROM Bookings WHERE RoomId = @RoomId AND Status <> 'Cancelled' AND StartTime < @EndTime AND EndTime > @StartTime";
        if (!string.IsNullOrEmpty(excludeId)) overlapSql += " AND Id <> @ExcludeId";
        var overlap = await _database.Connection.ExecuteScalarAsync<int>(overlapSql, new { RoomId = roomId, StartTime = start.ToString("O"), EndTime = end.ToString("O"), ExcludeId = excludeId });
        return overlap > 0 ? "This room is already booked during the selected time." : null;
    }

    private static string BookingSql(string where) => $"SELECT b.Id, b.RoomId, r.Name AS RoomName, b.UserId, u.Name AS UserName, b.Title, b.StartTime, b.EndTime, b.Status, b.Attendees, b.Notes, b.CreatedAt FROM Bookings b JOIN Rooms r ON r.Id = b.RoomId JOIN Users u ON u.Id = b.UserId {where}";

    private static void AddBookingFilters(ref string sql, DynamicParameters parameters, string? from, string? to, string? roomId, string? status)
    {
        if (!string.IsNullOrWhiteSpace(from)) { sql += " AND b.EndTime >= @From"; parameters.Add("From", from); }
        if (!string.IsNullOrWhiteSpace(to)) { sql += " AND b.StartTime <= @To"; parameters.Add("To", to); }
        if (!string.IsNullOrWhiteSpace(roomId)) { sql += " AND b.RoomId = @RoomId"; parameters.Add("RoomId", roomId); }
        if (!string.IsNullOrWhiteSpace(status)) { sql += " AND b.Status = @Status"; parameters.Add("Status", status); }
    }

    private bool CanManage(string ownerId) => User.IsInRole("Admin") || ownerId == CurrentUserId();
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private static string UtcNow() => DateTimeOffset.UtcNow.ToString("O");
    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    private static bool TryParseUtc(string? value, out DateTimeOffset result) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    private static DateTimeOffset ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result) ? new DateTimeOffset(result.Date, TimeSpan.Zero) : new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
    private static string NormalizeBookingStatus(string value) => value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ? "Cancelled" : "Confirmed";
    private static string NormalizeRoomStatus(string value) => value.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ? "Maintenance" : "Available";
    private static string? ValidateRoom(RoomRequest request) => string.IsNullOrWhiteSpace(request.Name) ? "Room name is required." : request.Floor < 0 ? "Floor cannot be negative." : request.Capacity < 1 ? "Capacity must be at least 1." : null;
    private static BookingResponse MapBooking(BookingWithDetails booking) => new(booking.Id, booking.RoomId, booking.RoomName, booking.UserId, booking.UserName, booking.Title, booking.StartTime, booking.EndTime, booking.Status, booking.Attendees, booking.Notes, booking.CreatedAt);
}
