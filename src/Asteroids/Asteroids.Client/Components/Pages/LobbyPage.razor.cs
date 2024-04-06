using Asteroids.Client.Components.Game;
using Asteroids.Shared;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.GameStateEntities;
using Asteroids.Shared.Lobbies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;
public partial class LobbyPage : ILobbyClient, IDisposable
{
    private ILobbyHub hubProxy = default!;
    private HubConnection connection = default!;
    private string? connectionId;

    private Dictionary<GameControlMessages.Key, bool> keyStates = new()
    {
        { GameControlMessages.Key.Up, false },
        { GameControlMessages.Key.Down, false },
        { GameControlMessages.Key.Left, false },
        { GameControlMessages.Key.Right, false },
        { GameControlMessages.Key.Space, false }
    };


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
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(HandleKeyDownAsync)}");
        GameControlMessages.Key key1 = GetKey(key);
        if (keyStates[key1])
            return;
        keyStates[key1] = true;

        Console.WriteLine("Key down: " + key1);
        var evt = new GameControlMessages.UpdateKeyStatesCommand(keyStates).ToSessionableMessage(connectionId!, SessionActorPath);
        await hubProxy.UpdateKeyStates(evt.ToTraceable(activity));
    }

    private async void HandleKeyUp(KeyCodes key)
    {
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(HandleKeyUp)}");
        GameControlMessages.Key key1 = GetKey(key);
        if (!keyStates.ContainsKey(key1))
            return;
        keyStates[key1] = false;

        Console.WriteLine("Key up: " + key1);
        var evt = new GameControlMessages.UpdateKeyStatesCommand(keyStates).ToSessionableMessage(connectionId!, SessionActorPath);
        await hubProxy.UpdateKeyStates(evt.ToTraceable(activity));
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

