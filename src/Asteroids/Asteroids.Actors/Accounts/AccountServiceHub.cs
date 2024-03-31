using Akka.Actor;
using Akka.Hosting;
using Asteroids.Shared.Actors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Asteroids.Shared.Accounts;

public interface IAccountServiceHub
{
    Task CreateAccount(CreateNewAccountCommand command);
    Task Login(string username, string password);

    Task NotifyAccountCreated(AccountCreatedEvent createdEvent);
    Task NotifyAccountCreateFailed(string username, string reason);
    Task NotifySuccessfulLogin(string username);
    Task NotifyFailedLogin(string username, string reason);
}

public interface IAccountServiceClient
{
    public Task AccountCreated();
    public Task AccountCreationFailed(string username, string reason);
    public Task AccountLoggedIn(string username);
    public Task AccountLoginFailed(string username, string reason);
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
        accountActor = actorRegistry.Get<AccountActor>();
    }
    public Task CreateAccount(CreateNewAccountCommand command)
    {
        logger.LogInformation($"Creating account for {command.Username} at hub");
        accountActor.Tell(command);
        return Task.CompletedTask;
    }

    public Task Login(string username, string password)
    {
        logger.LogInformation($"Logging in account for {username} at hub");
        accountActor.Tell("login request");
        return Task.CompletedTask;
    }

    public Task NotifyAccountCreated(AccountCreatedEvent created)
    {
        Clients.Client(created.ConnectionId).AccountCreated();
        return Task.CompletedTask;
    }

    public Task NotifyAccountCreateFailed(string username, string reason)
    {
        Clients.Others.AccountCreationFailed(username, reason);
        return Task.CompletedTask;
    }

    public Task NotifySuccessfulLogin(string username)
    {
        Clients.Others.AccountLoggedIn(username);
        return Task.CompletedTask;
    }

    public Task NotifyFailedLogin(string username, string reason)
    {
        Clients.Others.AccountLoginFailed(username, reason);
        return Task.CompletedTask;
    }

    public static string HubRelativeUrl => "hubs/accountservice";
    public static string HubUrl => $"http://asteroids-system:8080/{HubRelativeUrl}";
}
