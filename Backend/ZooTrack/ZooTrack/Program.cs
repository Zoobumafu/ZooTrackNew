// ZooTrack.WebAPI/Program.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ZooTrack.Data;
using ZooTrack.Services;
using ZooTrack.Hubs;
using ZooTrackBackend.Services;
var builder = WebApplication.CreateBuilder(args);

// --- Existing Services ---
builder.Services.AddControllers();
builder.Services.AddDbContext<ZootrackDbContext>(options =>
   options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZooTrack API", Version = "v1" });
});

// Example Notification/Detection services (keep them if used elsewhere)
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<NotificationService>(); // Assuming scoped lifetime is correct
builder.Services.AddScoped<IDetectionService, DetectionService>(); // Use interface
builder.Services.AddScoped<DetectionMediaService>(); // Assuming scoped is correct

// --- New Services ---

// 1. Register CameraService as Singleton (important for hardware access)
builder.Services.AddSingleton<CameraService>();

// 2. Add SignalR
builder.Services.AddSignalR();

// 3. Register the Background Service
builder.Services.AddHostedService<CameraProcessingService>();

// 4. Add CORS (Cross-Origin Resource Sharing) - VERY IMPORTANT for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        // --- THIS LINE WAS CHANGED ---
        // List the frontend Blazor app's origins (found in its launchSettings.json)
        // as separate strings, not semicolon-separated.
        policy.WithOrigins("https://localhost:7155", "http://localhost:5119")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR with credentials
    });
});


var app = builder.Build();


// Get the CameraService instance from the application's service provider
var cameraService = app.Services.GetRequiredService<CameraService>();
// Define the list of target animals 
List<string> myDesiredTargetAnimals = new List<string>
{
    "person",
    "dog",
    "cow",
    "wolf",
    "tiger"
};
string myHighlightSavePath = "D:\\CODE\\Zootrack\\ZooTrackNew\\Backend\\ZooTrack\\ZooTrack\\Data\\TargetAnimals.txt";
cameraService.SetProcessingTargets(myDesiredTargetAnimals, myHighlightSavePath);


// --- Database Initialization ---
// Ensure database is created and seeded with required data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ZootrackDbContext>();
    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

    try
    {
        // Ensure database is created
        context.Database.EnsureCreated();

        // Check if system user exists, create if not
        if (!context.Users.Any(u => u.UserId == 1))
        {
            var systemUser = new ZooTrack.Models.User
            {
                UserId = 1,
                Name = "System",
                Email = "system@zootrack.local",
                Role = "System",
            };
            context.Users.Add(systemUser);
            await context.SaveChangesAsync();

            Console.WriteLine("System user created successfully.");
        }

        // Check if default device exists
        if (!context.Devices.Any(d => d.DeviceId == 1))
        {
            var defaultDevice = new ZooTrack.Models.Device
            {
                DeviceId = 1,
                Location = "Main Area",
                Status = "Active",
                LastActive = DateTime.Now
            };
            context.Devices.Add(defaultDevice);
            await context.SaveChangesAsync();

            Console.WriteLine("Default device created successfully.");
        }

        // Log successful initialization
        await logService.AddLogAsync(
            userId: 1,
            actionType: "SystemStartup",
            message: $"ZooTrack system started successfully at {DateTime.Now:G}",
            level: "Info"
        );

        Console.WriteLine("Database initialization completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
        // Log the error if possible
        try
        {
            await logService.AddLogAsync(
                userId: 1,
                actionType: "SystemStartupFailed",
                message: $"System startup failed: {ex.Message}",
                level: "Error"
            );
        }
        catch
        {
            // If logging fails, just continue
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZooTrack API v1");
        // Keep Swagger UI at root for easy testing if desired
        // c.RoutePrefix = string.Empty;
    });
    // Enable detailed errors in development
    app.UseDeveloperExceptionPage();
}

// If you only intend to run the backend on HTTPS, you might keep this.
// If you need HTTP access too (like for SignalR on http), comment this out.
// app.UseHttpsRedirection(); // Consider if needed based on http/https usage

// --- Use CORS Policy ---
// IMPORTANT: Place UseCors *before* UseAuthorization and endpoint mapping.
app.UseCors("AllowBlazorApp");

app.UseAuthorization();

app.MapControllers();

// Example minimal API (keep if needed)
app.MapGet("api/animals", async (ZootrackDbContext db) =>
    await db.Animals.ToListAsync());


// --- Map SignalR Hub ---
app.MapHub<CameraHub>("/cameraHub"); // Define the endpoint for the hub

// --- Add Health Check Endpoint for Debugging ---
app.MapGet("/api/health", async (ZootrackDbContext db, ILogService logService) =>
{
    try
    {
        // Test database connection
        var userCount = await db.Users.CountAsync();
        var deviceCount = await db.Devices.CountAsync();
        var detectionCount = await db.Detections.CountAsync();
        var logCount = await db.Logs.CountAsync();

        var healthInfo = new
        {
            Status = "Healthy",
            Timestamp = DateTime.Now,
            Database = new
            {
                Users = userCount,
                Devices = deviceCount,
                Detections = detectionCount,
                Logs = logCount
            }
        };

        await logService.AddLogAsync(
            userId: 1,
            actionType: "HealthCheck",
            message: $"Health check performed - Users: {userCount}, Devices: {deviceCount}, Detections: {detectionCount}, Logs: {logCount}",
            level: "Info"
        );

        return Results.Ok(healthInfo);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Health check failed: {ex.Message}");
    }
});

app.Run();

// --- Make sure Models are included ---
// (Add using statements for Models if not implicit)
// using ZooTrack.Models;