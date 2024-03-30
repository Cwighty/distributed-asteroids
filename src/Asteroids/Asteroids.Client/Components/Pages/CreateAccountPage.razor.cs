using Asteroids.Shared.Hubs;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class CreateAccountPage : IAccountServiceClient
{
    private string username;
    private string password;
    private string errorMessage;

    private IAccountServiceHub hubProxy = default!;
    private HubConnection connection = default!;

    public async Task CreateAccount()
    {
        Logger.LogInformation($"Creating account for {username}");
        await hubProxy.CreateAccount(username, password);
    }

    public Task AccountCreated(string username)
    {
        Logger.LogInformation($"Account created for {username}");
        Navigation.NavigateTo("/login");
        return Task.CompletedTask;
    }

    public Task AccountCreationFailed(string username, string reason)
    {
        Logger.LogError($"Account creation failed for {username}: {reason}");
        errorMessage = reason;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public Task AccountLoggedIn(string username)
    {
        Logger.LogInformation($"Account logged in for {username}");
        return Task.CompletedTask;
    }

    public Task AccountLoginFailed(string username, string reason)
    {
        Logger.LogError($"Account login failed for {username}: {reason}");
        return Task.CompletedTask;
    }

    protected override async Task OnInitializedAsync()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("http://asteroids-system:8080/hubs/accountservice"))
            .Build();
        hubProxy = connection.ServerProxy<IAccountServiceHub>();
        _ = connection.ClientRegistration<IAccountServiceClient>(this);
        await connection.StartAsync();

    }
}

