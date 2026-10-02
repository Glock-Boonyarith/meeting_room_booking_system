namespace MeetingRoomBooking.Api.Models;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record UserResponse(string Id, string Name, string Email, string Role);

public sealed record LoginResponse(string Token, UserResponse User);

public sealed class RoomRequest
{
    public string Name { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public List<string> Amenities { get; set; } = [];
    public string Status { get; set; } = "Available";
}

public sealed record RoomResponse(
    string Id,
    string Name,
    int Floor,
    int Capacity,
    List<string> Amenities,
    string Status,
    string CreatedAt);

public sealed class BookingRequest
{
    public string RoomId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int Attendees { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class BookingUpdateRequest
{
    public string? Title { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public int? Attendees { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
}

public sealed record BookingResponse(
    string Id,
    string RoomId,
    string RoomName,
    string UserId,
    string UserName,
    string Title,
    string StartTime,
    string EndTime,
    string Status,
    int Attendees,
    string Notes,
    string CreatedAt);

public sealed record DashboardResponse(
    string Date,
    IReadOnlyList<RoomResponse> Rooms,
    IReadOnlyList<BookingResponse> Bookings,
    DashboardStats Stats);

public sealed record DashboardStats(
    int TotalRooms,
    int AvailableRooms,
    int BookingsToday,
    int MyBookingsToday);

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiresHours { get; set; } = 8;
}
