using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Asteroids.AsteroidSystem;
using Asteroids.AsteroidSystem.Hubs;
using Asteroids.AsteroidSystem.Options;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.Actors;
using Asteroids.Shared.Storage;
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

builder.Services.AddHttpClient("RaftStore", client => client.BaseAddress = new Uri(builder.Configuration.GetSection(nameof(ApiOptions))["RaftStorageUrl"] ?? throw new InvalidOperationException("RaftStorageUrl address not found.")));
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("RaftStore"));
builder.Services.AddSingleton<IStorageService, InMemoryStorageService>();

builder.Services.AddAkka("asteroid-system", cb =>
{
    cb.WithRemoting("localhost", 8110)
     .WithClustering(new ClusterOptions()
     {
         SeedNodes = new[] { "akka.tcp://asteroid-system@localhost:8110" }
     })
     .WithActors((system, registry) =>
     {
         var accountActor = system.ActorOf<AccountActor>();
         registry.TryRegister<AccountActor>(accountActor);
         var messageActor = system.ActorOf<MessageActor>();
         registry.TryRegister<MessageActor>(messageActor);
     });
}
);


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
app.MapHub<AccountServiceHub>(AccountServiceHub.HubRelativeUrl);

app.UseHttpsRedirection();


app.Run();
