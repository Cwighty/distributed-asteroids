using Asteroids.Client.Components.Game;
using Asteroids.Shared;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;
public partial class LobbyPage : ILobbyClient, IDisposable
{
    private ILobbyHub hubProxy = default!;
    private HubConnection connection = default!;
    private string? connectionId;


    public GameStateSnapshot? CurrentGameState { get; set; }

    [CascadingParameter(Name = "SessionActor")]
    public string SessionActorPath { get; set; } = string.Empty;


    [Parameter] public long LobbyId { get; set; }
    protected override async Task OnInitializedAsync()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(LobbyHub.HubUrl)
            .WithAutomaticReconnect()
            .Build();
        hubProxy = connection.ServerProxy<ILobbyHub>();
        _ = connection.ClientRegistration<ILobbyClient>(this);
        await connection.StartAsync();
        connectionId = connection.ConnectionId;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (CurrentGameState == null)
        {
            var session = await SessionService.GetSession();
            if (session == null)
            {
                Navigation.NavigateTo("/");
                return;
            }
            await InitializeLobby(session);
        }
    }

    public async Task InitializeLobby(string session)
    {
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(InitializeLobby)}");
        var qry = new LobbyStateQuery(LobbyId).ToSessionableMessage(connectionId!, session);
        await hubProxy.GetLobbyState(qry.ToTraceable(activity));
    }

    public async Task StartGame()
    {
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(StartGame)}");
        var cmd = new StartGameCommand(LobbyId).ToSessionableMessage(connectionId!, SessionActorPath);
        await hubProxy.StartGame(cmd.ToTraceable(activity));
    }

    public async Task OnInvalidSessionEvent(InvalidSessionEvent e)
    {
        await SessionService.ClearSession();
        Navigation.NavigateTo("/");
    }

    public async Task OnLobbyStateChangedEvent(Returnable<LobbyStateChangedEvent> e)
    {
        Console.WriteLine("LobbyStateChangedEvent {0}", e.Message.State);
        if (e.Message.State.Lobby.Id != LobbyId)
            return;
        if (CurrentGameState == null || CurrentGameState.Tick < e.Message.State.Tick)
            CurrentGameState = e.Message.State;
        await InvokeAsync(StateHasChanged);
    }

    public async Task OnGameStateBroadcast(Returnable<GameStateBroadcast> e)
    {
        if (e.Message.State.Lobby.Id != LobbyId)
        {
            return;
        }
        if (CurrentGameState.Tick >= e.Message.State.Tick)
        {
            return;
        }
        CurrentGameState = e.Message.State;
        await InvokeAsync(StateHasChanged);
    }

    #region KeyboardListener
    private KeyboardListener keyboardListener;

    private async Task HandleKeyDownAsync(KeyCodes key)
    {
        Console.WriteLine("KeyDown {0}", key);
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(HandleKeyDownAsync)}");
        GameControlMessages.Key key1 = GetKey(key);
        var evt = new GameControlMessages.KeyDownCommand(key1).ToSessionableMessage(connectionId!, SessionActorPath);
        await hubProxy.KeyDown(evt.ToTraceable(activity));
    }
   
    private void HandleKeyUp(KeyCodes key)
    {
        Console.WriteLine("KeyUp {0}", key);
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(HandleKeyUp)}");
        GameControlMessages.Key key1 = GetKey(key);
        var evt = new GameControlMessages.KeyUpCommand(key1).ToSessionableMessage(connectionId!, SessionActorPath);
        hubProxy.KeyUp(evt.ToTraceable(activity));
    }

    private static GameControlMessages.Key GetKey(KeyCodes key)
    {
        return key switch
        {
            KeyCodes.KEY_W => GameControlMessages.Key.Up,
            KeyCodes.KEY_A => GameControlMessages.Key.Left,
            KeyCodes.KEY_S => GameControlMessages.Key.Down,
            KeyCodes.KEY_D => GameControlMessages.Key.Right,
            KeyCodes.SPACE => GameControlMessages.Key.Space,
            _ => GameControlMessages.Key.None
        };
    }

    #endregion

    public void Dispose()
    {
        connection.DisposeAsync();
        keyboardListener?.Dispose();
    }
}

