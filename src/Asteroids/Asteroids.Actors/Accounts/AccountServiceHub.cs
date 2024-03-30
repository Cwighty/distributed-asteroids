using Asteroids.Shared.Actors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Asteroids.Shared.Accounts;

public interface IAccountServiceHub
{
    Task CreateAccount(string username, string password);
    Task Login(string username, string password);

    Task NotifyAccountCreated(string username);
    Task NotifyAccountCreateFailed(string username, string reason);
    Task NotifySuccessfulLogin(string username);
    Task NotifyFailedLogin(string username, string reason);
}

public interface IAccountServiceClient
{
    public Task AccountCreated(string username);
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
    private readonly IActorBridge actorBridge;

    public AccountServiceHub(ILogger<AccountServiceHub> logger, IActorBridge actorBridge)
    {
        this.logger = logger;
        this.actorBridge = actorBridge;
    }
    public Task CreateAccount(string username, string password)
    {
        logger.LogInformation($"Creating account for {username} at hub");
        actorBridge.Tell("create account request");
        return Task.CompletedTask;
    }

    public Task Login(string username, string password)
    {
        logger.LogInformation($"Logging in account for {username} at hub");
        actorBridge.Tell("login request");
        return Task.CompletedTask;
    }

    public Task NotifyAccountCreated(string username)
    {
        Clients.Others.AccountCreated(username);
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
