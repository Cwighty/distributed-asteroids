using Asteroids.Shared.Accounts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class LoginPage : IAccountServiceClient
{
    private string username = String.Empty;
    private string password = String.Empty;
    private string errorMessage = String.Empty;

    private IAccountServiceHub hubProxy = default!;
    private HubConnection connection = default!;

    public async Task Login()
    {
        var id = connection.ConnectionId;
        await hubProxy.Login(username, password);
    }

    public Task AccountCreated()
    {
        throw new NotImplementedException();
    }

    public Task AccountCreationFailed(string reason)
    {
        throw new NotImplementedException();
    }

    public Task AccountLoggedIn(string username)
    {
        throw new NotImplementedException();
    }

    public Task AccountLoginFailed(string username, string reason)
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
}
