using Asteroids.Shared.Accounts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class CreateAccountPage : IAccountServiceClient
{
    private string username = String.Empty;
    private string password = String.Empty;
    private string errorMessage = String.Empty;

    private IAccountServiceHub hubProxy = default!;
    private HubConnection connection = default!;

    public async Task CreateAccount()
    {
        Logger.LogInformation($"Creating account for {username}");
        var connectionId = connection.ConnectionId!;
        var command = new CreateNewAccountCommand(connectionId, username, password);
        await hubProxy.CreateAccount(command);
    }

    public Task AccountCreated()
    {
        Logger.LogInformation($"Account created");
        Navigation.NavigateTo("/login");
        return Task.CompletedTask;
    }

    public Task AccountCreationFailed(string reason)
    {
        Logger.LogError($"Account creation failed: {reason}");
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
            .WithUrl(AccountServiceHub.HubUrl)
            .Build();
        hubProxy = connection.ServerProxy<IAccountServiceHub>();
        _ = connection.ClientRegistration<IAccountServiceClient>(this);
        await connection.StartAsync();
    }
}

