using Asteroids.Shared.Hubs;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class LoginPage : IAccountServiceClient
{
    private string username;
    private string password;
    private string errorMessage;

    private IAccountServiceHub hubProxy = default!;
    private HubConnection connection = default!;

    public async Task Login()
    {
         await hubProxy.Login(username, password);
    }

    public Task AccountCreated(string username)
    {
        throw new NotImplementedException();
    }

    public Task AccountCreationFailed(string username, string reason)
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
            .WithUrl(Navigation.ToAbsoluteUri("http://asteroids-system:8080/hubs/accountservice"))
            .Build();
        hubProxy = connection.ServerProxy<IAccountServiceHub>();
        _ = connection.ClientRegistration<IAccountServiceClient>(this);
        await connection.StartAsync();

    }
}
