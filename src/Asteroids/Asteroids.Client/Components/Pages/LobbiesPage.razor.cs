using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;

public partial class LobbiesPage : ILobbyClient, IDisposable
{
    private ILobbyHub hubProxy = default!;
    private HubConnection connection = default!;

    private string lobbyName = string.Empty;
    private List<LobbyInfo>? lobbies;
    private string? connectionId;

    [CascadingParameter(Name = "SessionActor")]
    public string SessionActorPath { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(LobbyHub.HubUrl)
            .Build();
        hubProxy = connection.ServerProxy<ILobbyHub>();
        _ = connection.ClientRegistration<ILobbyClient>(this);
        await connection.StartAsync();
        connectionId = connection.ConnectionId;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (SessionActorPath != null && lobbies == null)
        {
            Console.WriteLine("ViewAllLobbies {0}", connectionId);
            await hubProxy.ViewAllLobbies(new ViewAllLobbiesQuery().ToSessionableMessage(connectionId, SessionActorPath));
        }
    }

    private async Task CreateLobby()
    {
       var cmd = new CreateLobbyCommand(lobbyName);
       await hubProxy.CreateLobby(cmd.ToSessionableMessage(connectionId!, SessionActorPath));
       lobbyName = string.Empty;
    }

    private async Task JoinLobby(long lobbyId)
    {
    }

    public async Task OnCreateLobbyEvent(CreateLobbyEvent e)
    {
        Console.WriteLine("OnCreateLobbyEvent");
        lobbies = e.Lobbies.ToList();
        await InvokeAsync(StateHasChanged);
    }

    public async Task OnInvalidSessionEvent(InvalidSessionEvent e)
    {
        await SessionService.ClearSession();
        Navigation.NavigateTo("/");
    }

    public Task OnJoinLobbyEvent(JoinLobbyEvent e)
    {
        throw new NotImplementedException();
    }

    public async Task OnViewAllLobbiesResponse(ViewAllLobbiesResponse response)
    {
        Console.WriteLine("OnViewAllLobbiesResponse");
        lobbies = response.Lobbies.ToList();
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        connection?.DisposeAsync();
    }
}