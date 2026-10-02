using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using MeetingRoomBooking.Api.Models;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly JsonDatabase _database;
    private readonly PasswordService _passwords;
    private readonly JwtOptions _jwt;

    public AuthController(JsonDatabase database, PasswordService passwords, IOptions<JwtOptions> jwt)
    {
        _database = database;
        _passwords = passwords;
        _jwt = jwt.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var user = await _database.Connection.QuerySingleOrDefaultAsync<UserRecord>(
            "SELECT Id, Name, Email, Role, PasswordHash, PasswordSalt, CreatedAt FROM Users WHERE lower(Email) = lower(@Email)",
            new { request.Email });

        if (user is null || !_passwords.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var response = new LoginResponse(CreateToken(user), ToResponse(user));
        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _database.Connection.QuerySingleOrDefaultAsync<UserRecord>(
            "SELECT Id, Name, Email, Role, PasswordHash, PasswordSalt, CreatedAt FROM Users WHERE Id = @Id",
            new { Id = userId });

        return user is null ? NotFound() : Ok(ToResponse(user));
    }

    private string CreateToken(UserRecord user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpiresHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserResponse ToResponse(UserRecord user) => new(user.Id, user.Name, user.Email, user.Role);
}
