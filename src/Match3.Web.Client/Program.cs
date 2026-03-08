using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Match3.Web.Client;
using Match3.Web.Client.Services;
using Match3.Core.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register game service factory
builder.Services.AddSingleton<IGameServiceFactory>(_ => new GameServiceBuilder().Build());

// Register game service
builder.Services.AddScoped<Match3GameService>();

// Logging
builder.Services.AddLogging();

await builder.Build().RunAsync();
