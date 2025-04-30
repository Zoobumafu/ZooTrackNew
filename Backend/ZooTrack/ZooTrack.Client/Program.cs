// ZooTrack.Client/Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ZooTrack.Client; // Your project's namespace

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- Configure HttpClient ---
// IMPORTANT: Use the base address of your ZooTrack.WebAPI
// Find this in the API's launchSettings.json (e.g., https://localhost:7250)
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7155;http://localhost:5119") // !!! REPLACE WITH YOUR API URL !!!
});

// Add logging (optional but helpful)
builder.Logging.SetMinimumLevel(LogLevel.Debug);


await builder.Build().RunAsync();