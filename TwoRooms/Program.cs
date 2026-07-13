using TwoRooms.Client.Pages;
using TwoRooms.Components;
using TwoRooms.Hubs;
using TwoRooms.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddSignalR();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ReactionDuelService>();
builder.Services.AddSingleton<MazeService>();
builder.Services.AddSingleton<SymbolLockService>();
builder.Services.AddSingleton<BombService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
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
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(TwoRooms.Client._Imports).Assembly);

app.MapHub<GameHub>(TwoRooms.Client.Game.GameConstants.HubPath);

app.Run();
