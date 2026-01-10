using Match3.Web.Components;
using Match3.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<Match3GameService>();

// Editor Services
builder.Services.AddScoped<Match3.Editor.Interfaces.IPlatformService, Match3.Web.Services.EditorAdapters.WebPlatformService>();
builder.Services.AddScoped<Match3.Editor.Interfaces.IFileSystemService>(sp => 
    new Match3.Web.Services.EditorAdapters.PhysicalFileSystemService(@"d:\GitWorkSpace\LiuJun\ThreeMatchTrea\src\Match3.Core.Tests\Scenarios\Data"));
builder.Services.AddScoped<Match3.Editor.Interfaces.IJsonService, Match3.Web.Services.EditorAdapters.SystemTextJsonService>();
builder.Services.AddScoped<Match3.Core.Utility.IGameLogger>(sp => new MicrosoftGameLogger(sp.GetRequiredService<ILogger<MicrosoftGameLogger>>()));
builder.Services.AddScoped<Match3.Editor.ViewModels.LevelEditorViewModel>();

builder.Services.AddScoped<ScenarioLibraryService>(sp => 
    new ScenarioLibraryService(@"d:\GitWorkSpace\LiuJun\ThreeMatchTrea\src\Match3.Core.Tests\Scenarios\Data"));
builder.Services.AddScoped<Match3.Editor.Interfaces.IScenarioService>(sp => sp.GetRequiredService<ScenarioLibraryService>());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
