namespace MeetingRoomBooking.Api.Models;

public sealed class UserRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class RoomRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public List<string> Amenities { get; set; } = [];
    public string Status { get; set; } = "Available";
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class RoomRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public string AmenitiesJson { get; set; } = "[]";
    public string Status { get; set; } = "Available";
    public string CreatedAt { get; set; } = string.Empty;
}

public class BookingRecord
{
    public string Id { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Status { get; set; } = "Confirmed";
    public int Attendees { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class BookingWithDetails : BookingRecord
{
    public string RoomName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
