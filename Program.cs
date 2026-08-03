using GA.Application.Features.Auth;
using GA.Application.Features.Users;
using GA.Application.Features.Chat;
using GA.Application.Features.Location;
using GA.Application.Features.Notifications;
using GA.Application.Features.Translation;
using GA.Application.Features.OfficeChat;
using GA.Application.Features.WorkOrders;
using GA.Core.Interfaces;
using GA.Infrastructure.Background;
using GA.Infrastructure.Hubs;
using GA.Infrastructure.Persistence.Context;
using GA.Infrastructure.Persistence.Repositories;
using GA.Infrastructure.Persistence.Seed;
using GA.Infrastructure.Services;
using GA.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Yetki belgesi PDF yükleme (max ~27 MB + pay)
const long MaxUploadBytes = 30L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxUploadBytes;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxUploadBytes;
});

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
builder.Services.AddScoped<IUserAccessService, UserAccessService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPushNotificationService, ExpoPushNotificationService>();
builder.Services.AddHttpClient("expo-push", client =>
{
    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IOfficeChatService, OfficeChatService>();

builder.Services.Configure<TranslationOptions>(
    builder.Configuration.GetSection(TranslationOptions.SectionName));
builder.Services.AddHttpClient("translation", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<ITranslationService, TranslationService>();

builder.Services.AddMemoryCache();

// Periyodik iş emri otomasyonu
builder.Services.Configure<PeriodicWorkOrdersOptions>(
    builder.Configuration.GetSection(PeriodicWorkOrdersOptions.SectionName));
builder.Services.AddScoped<IPeriodicScheduleService, PeriodicScheduleService>();
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
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs/location") || path.StartsWithSegments("/hubs/chat")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Login yanıtındaki token'ı yapıştırın. 'Bearer ' öneki yazmayın.",
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:5112",
                "https://204.168.249.86:8443",
                "http://204.168.249.86:8443",
                "http://204.168.249.86:8080",
                "http://204.168.249.86:8081")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await RoleSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseMiddleware<TenantDemoAccessMiddleware>();
app.UseAuthorization();
app.MapControllers();

// SignalR hub endpoint
app.MapHub<LocationHub>("/hubs/location");
app.MapHub<ChatHub>("/hubs/chat");

if (args.Contains("--backfill-periods", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var schedule = scope.ServiceProvider.GetRequiredService<IPeriodicScheduleService>();
    var result = await schedule.BackfillAllTemplatesAsync();
    Console.WriteLine(
        $"Backfill OK: templates={result.TemplatesProcessed}, periods={result.PeriodsCreated}, labels={result.PeriodLabelsUpdated}");
    return;
}

app.Run();
