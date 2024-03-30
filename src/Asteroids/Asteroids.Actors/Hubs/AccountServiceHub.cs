using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Asteroids.Shared.Hubs;

public interface IAccountServiceHub
{
    Task CreateAccount(string username, string password);
    Task Login(string username, string password);
}

public interface IAccountServiceClient
{
    Task AccountCreated(string username);
    Task AccountCreationFailed(string username, string reason);
    Task AccountLoggedIn(string username);
    Task AccountLoginFailed(string username, string reason);
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

    public AccountServiceHub(ILogger<AccountServiceHub> logger)
    {
        this.logger = logger;
    }
    public Task CreateAccount(string username, string password)
    {
        logger.LogInformation($"Creating account for {username} at hub");
        try
        {
            KeyValueStore.Add(username, password);
            Clients.Caller.AccountCreated(username);
        }
        catch (Exception ex)
        {
            Clients.Caller.AccountCreationFailed(username, ex.Message);
        }
        return Task.CompletedTask;
    }

    public Task Login(string username, string password)
    {
        logger.LogInformation($"Logging in account for {username} at hub");
        if (KeyValueStore.Data.ContainsKey(username) && KeyValueStore.Data[username] == password)
        {
            Clients.Caller.AccountLoggedIn(username);
        }
        else
        {
            Clients.Caller.AccountLoginFailed(username, "Invalid username or password");
        }
        return Task.CompletedTask;
    }
}
