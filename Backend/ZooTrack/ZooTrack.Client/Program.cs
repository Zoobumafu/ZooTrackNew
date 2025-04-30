// ZooTrack.Client/Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ZooTrack.Client; // Your project's namespace

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- Configure HttpClient ---
// IMPORTANT: Use the base address of your ZooTrack.WebAPI
// Found in the WebAPI's launchSettings.json (e.g., https://localhost:7019)
builder.Services.AddScoped(sp => new HttpClient
{
    // --- Ensure this line points to your BACKEND API URL ---
    BaseAddress = new Uri("https://localhost:7019")
});

// Add logging (optional but helpful)
// Note: Default Blazor logging works without adding specific console providers here
builder.Logging.SetMinimumLevel(LogLevel.Debug);


await builder.Build().RunAsync();