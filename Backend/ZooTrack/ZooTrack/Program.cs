// Definitive Fix
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Text;
using System.Threading.Tasks;
using ZooTrack.Data;
using ZooTrack.Hubs;
using ZooTrack.Services;
using ZooTrackBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Service Registration ---
builder.Services.AddControllers();
builder.Services.AddDbContext<ZootrackDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(warnings =>
       warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
}
   );

builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IDetectionService, DetectionService>();
builder.Services.AddScoped<DetectionMediaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<CameraService>();
builder.Services.AddHostedService<CameraProcessingService>();
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();

// --- Authentication Service Configuration ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var tokenSecret = builder.Configuration.GetSection("AppSettings:Token").Value;
        if (string.IsNullOrEmpty(tokenSecret))
        {
            throw new ArgumentNullException(nameof(tokenSecret), "AppSettings:Token is not configured.");
        }
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        // This event allows SignalR to authenticate using the query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/cameraHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// --- Swagger Configuration ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ZooTrack API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { /* ... */ });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { /* ... */ });
});

// --- CORS Policy ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        policy.WithOrigins("https://localhost:7155", "http://localhost:5119")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- Database Initialization ---
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ZootrackDbContext>();
    context.Database.EnsureCreated();
}

// --- HTTP Request Pipeline Configuration ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZooTrack API v1"));
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowBlazorApp");
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<CameraHub>("/cameraHub");
});

app.Run();
