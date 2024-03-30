using Asteroids.AsteroidSystem;
using Asteroids.AsteroidSystem.Hubs;
using Asteroids.AsteroidSystem.Options;
using Microsoft.AspNetCore.ResponseCompression;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.AddApiOptions();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddSingleton<IActorBridge, AkkaService>();
builder.Services.AddHostedService<AkkaService>(sp => (AkkaService)sp.GetRequiredService<IActorBridge>());

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
          new[] { "application/octet-stream" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHub<MessageHub>("/messagehub");

app.UseHttpsRedirection();


app.Run();
