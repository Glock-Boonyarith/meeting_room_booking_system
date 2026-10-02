using System.Text;
using MeetingRoomBooking.Api.Models;
using MeetingRoomBooking.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = new JwtOptions
{
    Key = builder.Configuration["Jwt:Key"] ?? "local-development-key-change-me",
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "MeetingRoomBooking.Api",
    Audience = builder.Configuration["Jwt:Audience"] ?? "MeetingRoomBooking.Web",
    ExpiresHours = int.TryParse(builder.Configuration["Jwt:ExpiresHours"], out var hours) ? hours : 8
};

builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));
builder.Services.AddSingleton<JsonDatabase>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").GetChildren().Select(section => section.Value).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray();
    if (origins.Length == 0) origins = ["http://localhost:5173"];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "meeting-room-booking-api" }));
app.MapControllers();
app.Run();
