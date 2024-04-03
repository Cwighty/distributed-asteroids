using Akka.Actor;
using Akka.Hosting;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shared.Observability;
using System.Diagnostics;

namespace Asteroids.Shared.Accounts;

public interface IAccountServiceHub
{
    Task CreateAccount(CreateAccountCommand command);
    Task Login(Traceable<LoginCommand> command);

    Task NotifyAccountCreationEvent(CreateAccountEvent createdEvent);
    Task NotifyLoginEvent(LoginEvent loginEvent);
    Task NotifyStartUserSessionEvent(StartUserSessionEvent e);
}

public interface IAccountServiceClient
{
    public Task AccountCreated();
    public Task AccountCreationFailed(string reason);
    public Task OnLoginEvent(LoginEvent loginEvent);
    public Task OnStartUserSessionEvent(StartUserSessionEvent e);
}

public static class KeyValueStore
{
    public static Dictionary<string, string> Data { get; } = new();

    public static void Add(string key, string value)
    {
        Data.Add(key, value);
    }

    public static string Get(string key)
    {
        return Data[key];
    }
}


public class AccountServiceHub : Hub<IAccountServiceClient>, IAccountServiceHub
{
    private readonly ILogger<AccountServiceHub> logger;
    private readonly IActorRef accountActor;

    public AccountServiceHub(ILogger<AccountServiceHub> logger, ActorRegistry actorRegistry)
    {
        this.logger = logger;
        accountActor = actorRegistry.Get<AccountSupervisorActor>();
    }

    public Task CreateAccount(CreateAccountCommand command)
    {
        logger.LogInformation($"Creating account for {command.Username} at hub");
        accountActor.Tell(command);
        return Task.CompletedTask;
    }

    public Task Login(Traceable<LoginCommand> tcommand)
    {
        return ExecuteTraceable(tcommand, (command, activity) =>
        {
            logger.LogInformation($"Logging in account for {command.Username} at hub");
            accountActor.Tell(command.ToTraceable(activity));
        });
    }

    public Task NotifyAccountCreationEvent(CreateAccountEvent created)
    {
        logger.LogInformation("Notifying account creation event {0}", created.errorMessage);
        if (created.success)
        {
            Clients.Client(created.ConnectionId).AccountCreated();
        }
        else
        {
            Clients.Client(created.ConnectionId).AccountCreationFailed(created.errorMessage ?? "Failed to create account");
        }
        return Task.CompletedTask;
    }

    public Task NotifyLoginEvent(LoginEvent e)
    {
        Clients.Client(e.OriginalCommand.ConnectionId).OnLoginEvent(e);
        return Task.CompletedTask;
    }

    public Task NotifyStartUserSessionEvent(StartUserSessionEvent e)
    {
        Clients.Client(e.ConnectionId).OnStartUserSessionEvent(e);
        return Task.CompletedTask;
    }

    private Task ExecuteTraceable<T>(Traceable<T> traceable, Action<T, Activity?> action)
    {
        using var activity = traceable.Activity($"{nameof(AccountServiceHub)}: {typeof(T).Name}");
        var command = traceable.Message;
        action(command, activity);
        return Task.CompletedTask;
    }

    public static string HubRelativeUrl => "hubs/accountservice";
    public static string HubUrl => $"http://asteroids-system:8080/{HubRelativeUrl}";
}
