using Asteroids.Shared.Accounts;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class CreateAccountPage : IAccountServiceClient
{
    private string username = String.Empty;

    public string Username
    {
        get => username;
        set
        {
            username = value;
        }
    }

    private string password = String.Empty;

    private IAccountServiceHub hubProxy = default!;
    private HubConnection connection = default!;

    public async Task CreateAccount()
    {
        Logger.LogInformation($"Creating account for {username}");
        var connectionId = connection.ConnectionId!;
        var command = new CreateAccountCommand(connectionId, username, password);
        await hubProxy.CreateAccount(command);
    }

    public Task AccountCreated()
    {
        Logger.LogInformation($"Account created");
        toastService.ShowSuccess("Account created");
        Navigation.NavigateTo("/login");
        return Task.CompletedTask;
    }

    public Task AccountCreationFailed(string reason)
    {
        Logger.LogError($"Account creation failed: {reason}");
        toastService.ShowError($"Account creation failed: {reason}");
        StateHasChanged();
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

    public Task OnLoginEvent(LoginEvent loginEvent)
    {
        throw new NotImplementedException();
    }

    public Task OnStartUserSessionEvent(StartUserSessionEvent e)
    {
        throw new NotImplementedException();
    }
}

