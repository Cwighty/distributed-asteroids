using Asteroids.Client.Components;
using Asteroids.Client.Options;
using Asteroids.Client.Services;
using Blazored.LocalStorage;
using Blazored.Toast;
using Microsoft.AspNetCore.DataProtection;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.AddApiOptions();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazoredToast();
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddScoped<SessionService>();

builder.Services.AddAntiforgery(options => { options.Cookie.Expiration = TimeSpan.Zero; });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
