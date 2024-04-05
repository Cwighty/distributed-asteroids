using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.DependencyInjection;
using Akka.Event;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Asteroids.AsteroidSystem.Options;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;
using Asteroids.Shared.Storage;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.ResponseCompression;
using Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.AddApiOptions();
// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(opt =>
{
    opt.EnableDetailedErrors = true;
});

builder.Services.AddHttpClient("RaftStore", client => client.BaseAddress = new Uri(builder.Configuration.GetSection(nameof(ApiOptions))["RaftStorageUrl"] ?? throw new InvalidOperationException("RaftStorageUrl address not found.")));
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("RaftStore"));
builder.Services.AddSingleton<IStorageService, InMemoryStorageService>();

builder.Services.AddAkka("asteroid-system", cb =>
{
    cb.WithRemoting("localhost", 8110)
    .ConfigureLoggers((setup) =>
    {
        setup.AddLoggerFactory();
    })
     .WithClustering(new ClusterOptions()
     {
         SeedNodes = new[] { "akka.tcp://asteroid-system@localhost:8110" }
     })
     .WithActors((system, registry) =>
     {
         var accountActor = system.ActorOf(AccountSupervisorActor.Props(), AkkaHelper.AccountSupervisorActorPath);
         registry.TryRegister<AccountSupervisorActor>(accountActor);
         var sessionSupervisorActor = system.ActorOf(UserSessionSupervisor.Props(), AkkaHelper.UserSessionSupervisorActorPath);
         registry.TryRegister<UserSessionSupervisor>(sessionSupervisorActor);

         var lobbiesEmmitterActor = system.ActorOf(LobbiesEmitterActor.Props(), AkkaHelper.LobbiesEmitterActorPath);
         var lobbyEmitterActor = system.ActorOf(LobbyEmitterActor.Props(), AkkaHelper.LobbyEmitterActorPath);
         var lobbySupervisorActor = system.ActorOf(LobbySupervisor.Props(lobbiesEmmitterActor, lobbyEmitterActor), AkkaHelper.LobbySupervisorActorPath);
         registry.TryRegister<LobbySupervisor>(lobbySupervisorActor);

         // custom handle dead letters
         var deadLetterProps = DependencyResolver.For(system).Props<DeadLetterActor>();
         var deadLetterActor = system.ActorOf(deadLetterProps, "deadLetterActor");
         system.EventStream.Subscribe(deadLetterActor, typeof(DeadLetter));
         system.EventStream.Subscribe(deadLetterActor, typeof(UnhandledMessage));
         system.EventStream.Subscribe(deadLetterActor, typeof(AllDeadLetters));
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


app.MapHub<AccountServiceHub>(AccountServiceHub.HubRelativeUrl);
app.MapHub<LobbiesHub>(LobbiesHub.HubRelativeUrl);
app.MapHub<LobbyHub>(LobbyHub.HubRelativeUrl);

app.UseHttpsRedirection();


app.Run();
