using Asteroids.Shared;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class LoginPage : IAccountServiceClient
{
    private string username = String.Empty;
    private string password = String.Empty;

    private IAccountServiceHub hubProxy = default!;
    private HubConnection connection = default!;

    public async Task Login()
    {
    System.Diagnostics.Activity.Current = null;
    using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LoginPage)}: Login");

        var id = connection.ConnectionId;
        var loginCommand = new LoginCommand(id!, username, password);
        await hubProxy.Login(loginCommand.ToTraceable(activity));
    }

    public Task AccountCreated()
    {
        throw new NotImplementedException();
    }

    public Task AccountCreationFailed(string reason)
    {
        throw new NotImplementedException();
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
        if (!loginEvent.Success)
        {
            ToastService.ShowError(loginEvent.errorMessage ?? "Login failed");
        }
        return Task.CompletedTask;
    }

    public async Task OnStartUserSessionEvent(StartUserSessionEvent e)
    {
        await SessionService.StoreSession(e.SessionActorPath);
        Navigation.NavigateTo("/lobbies");
    }
}
