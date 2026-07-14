using GA.Application.Features.Auth;
using GA.Application.Features.Location;
using GA.Application.Features.WorkOrders;
using GA.Core.Interfaces;
using GA.Infrastructure.Background;
using GA.Infrastructure.Hubs;
using GA.Infrastructure.Persistence.Context;
using GA.Infrastructure.Persistence.Repositories;
using GA.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL + NetTopologySuite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseNetTopologySuite()));

// DI
builder.Services.AddOpenApi();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Periyodik iş emri otomasyonu
builder.Services.Configure<PeriodicWorkOrdersOptions>(
    builder.Configuration.GetSection(PeriodicWorkOrdersOptions.SectionName));
builder.Services.AddScoped<IPeriodicWorkOrderService, PeriodicWorkOrderService>();
builder.Services.AddHostedService<PeriodicWorkOrderHostedService>();

// SignalR — gerçek zamanlı konum yayını için
builder.Services.AddSignalR();

// JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };

    // SignalR WebSocket bağlantıları JWT'yi query string ile gönderir
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/location"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:5112")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SignalR hub endpoint
app.MapHub<LocationHub>("/hubs/location");

app.Run();
