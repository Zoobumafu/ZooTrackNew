using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ZooTrack.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddDbContext<ZootrackDbContext>(options =>
    options.UseSqlite("Data Source=zootrack.db"));

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZooTrack API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZooTrack API v1");
        c.RoutePrefix = string.Empty; // Makes Swagger UI available at root URL
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

// Example minimal API (optional — move to a controller if it grows)
app.MapGet("api/animals", async (ZootrackDbContext db) =>
    await db.Animals.ToListAsync());

app.Run();
