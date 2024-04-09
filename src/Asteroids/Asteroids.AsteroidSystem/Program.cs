using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Cluster.Tools.Singleton;
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

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddObservability();
        builder.AddApiOptions();
        var apiOptions = builder.Configuration.GetSection(nameof(ApiOptions)).Get<ApiOptions>();
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

        builder.Services.AddAkka("MyAsteroidSystem", cb =>
            {
                if (apiOptions == null)
                {
                    throw new InvalidOperationException("ApiOptions not found.");
                }

                cb.WithRemoting(apiOptions.AkkaHostname, 0)
                    .WithClustering(new ClusterOptions()
                    {
                        Roles = apiOptions.AkkaRoles.Split(",", StringSplitOptions.RemoveEmptyEntries),
                        SeedNodes = apiOptions.AkkaSeeds.Split(",", StringSplitOptions.RemoveEmptyEntries),
                    })
                    .ConfigureLoggers((setup) =>
                    {
                        setup.AddLoggerFactory();
                    })
                    .WithActors((system, registry) =>
                    {
                        var selfMember = Cluster.Get(system).SelfMember;
                        if (selfMember.HasRole("SignalR"))
                        {
                            var lobbySupervisorProxy = system.ActorOf(ClusterSingletonProxy.Props(
                                    singletonManagerPath: $"/user/{AkkaHelper.LobbySupervisorActorPath}",
                                    settings: ClusterSingletonProxySettings.Create(system).WithRole("Lobbies")),
                                name: "lobbySupervisorProxy");
                            registry.TryRegister<LobbySupervisor>(lobbySupervisorProxy);
                            var accountActor = system.ActorOf(AccountSupervisorActor.Props(), AkkaHelper.AccountSupervisorActorPath);
                            registry.TryRegister<AccountSupervisorActor>(accountActor);
                            var sessionSupervisorActor = system.ActorOf(UserSessionSupervisor.Props(lobbySupervisorProxy), AkkaHelper.UserSessionSupervisorActorPath);
                            registry.TryRegister<UserSessionSupervisor>(sessionSupervisorActor);
                        }

                        if (selfMember.HasRole("Lobbies"))
                        {
                            RegisterLobbySingletons(system, registry);
                        }

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


        if (apiOptions.AkkaRoles.Contains("SignalR"))
        {
            app.MapHub<AccountServiceHub>(AccountServiceHub.HubRelativeUrl);
            app.MapHub<LobbiesHub>(LobbiesHub.HubRelativeUrl);
            app.MapHub<LobbyHub>(LobbyHub.HubRelativeUrl);
        }

        app.UseHttpsRedirection();


        app.Run();

    }

    private static IActorRef RegisterLobbySingletons(ActorSystem actorSystem, IActorRegistry actorRegistry)
    {
        // create lobbies emitter singleton
        actorSystem.ActorOf(ClusterSingletonManager.Props(
                singletonProps: LobbiesEmitterActor.Props(),
                terminationMessage: PoisonPill.Instance,
                settings: ClusterSingletonManagerSettings.Create(actorSystem).WithRole("Lobbies")),
            name: AkkaHelper.LobbiesEmitterActorPath);

        // create lobbies emitter proxy
        var lobbiesEmitterProxy = actorSystem.ActorOf(ClusterSingletonProxy.Props(
                singletonManagerPath: $"/user/{AkkaHelper.LobbiesEmitterActorPath}",
                settings: ClusterSingletonProxySettings.Create(actorSystem).WithRole("Lobbies")),
            name: "lobbiesEmitterProxy");

        // create lobby emitter singleton
        actorSystem.ActorOf(ClusterSingletonManager.Props(
                singletonProps: LobbyEmitterActor.Props(),
                terminationMessage: PoisonPill.Instance,
                settings: ClusterSingletonManagerSettings.Create(actorSystem).WithRole("Lobbies")),
            name: AkkaHelper.LobbyEmitterActorPath);

        // create lobby emitter proxy
        var lobbyEmitterProxy = actorSystem.ActorOf(ClusterSingletonProxy.Props(
                singletonManagerPath: $"/user/{AkkaHelper.LobbyEmitterActorPath}",
                settings: ClusterSingletonProxySettings.Create(actorSystem).WithRole("Lobbies")),
            name: "lobbyEmitterProxy");

        // create lobby supervisor singleton
        actorSystem.ActorOf(ClusterSingletonManager.Props(
                singletonProps: LobbySupervisor.Props(lobbiesEmitterProxy, lobbyEmitterProxy),
                terminationMessage: PoisonPill.Instance,
                settings: ClusterSingletonManagerSettings.Create(actorSystem).WithRole("Lobbies")),
            name: AkkaHelper.LobbySupervisorActorPath);
        // create lobby supervisor proxy
        var lobbySupervisorProxy = actorSystem.ActorOf(ClusterSingletonProxy.Props(
                singletonManagerPath: $"/user/{AkkaHelper.LobbySupervisorActorPath}",
                settings: ClusterSingletonProxySettings.Create(actorSystem).WithRole("Lobbies")),
            name: "lobbySupervisorProxy");
        actorRegistry.TryRegister<LobbySupervisor>(lobbySupervisorProxy);

        return lobbySupervisorProxy;
    }
}
