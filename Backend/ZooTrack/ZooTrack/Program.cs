// ZooTrack.WebAPI/Program.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ZooTrack.Data;
using ZooTrack.Services;
using ZooTrack.Hubs; // Add Hubs namespace

var builder = WebApplication.CreateBuilder(args);

// --- Existing Services ---
builder.Services.AddControllers();
builder.Services.AddDbContext<ZootrackDbContext>(options =>
    options.UseSqlite("Data Source=zootrack.db"));

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZooTrack API", Version = "v1" });
});

// Example Notification/Detection services (keep them if used elsewhere)
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
        policy.WithOrigins("https://localhost:7155;http://localhost:5119") // !!! IMPORTANT: Replace with your Blazor app's actual ports (HTTPS and HTTP)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR with credentials
    });
});


var app = builder.Build();

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

app.UseHttpsRedirection();

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


app.Run();

// --- Make sure Models are included ---
// (Add using statements for Models if not implicit)
// using ZooTrack.Models;