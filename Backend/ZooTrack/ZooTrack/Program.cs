using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ZooTrack.Data;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllers();


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ZootrackDbContext>(options =>
    options.UseSqlite("Data Source=zootrack.db"));

builder.Services.AddEndpointsApiExplorer(); // Adds API explorer capabilities needed for Swagger
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZooTrack API", Version = "v1" });
});

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("api/animals", async (ZootrackDbContext db) =>
    await db.Animals.ToListAsync());

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZooTrack API v1");
    c.RoutePrefix = string.Empty; // This makes Swagger UI available at the root URL
});

app.UseSwagger();
app.UseSwaggerUI();

app.Run();