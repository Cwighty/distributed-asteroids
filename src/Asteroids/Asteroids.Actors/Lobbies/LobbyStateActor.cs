using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.GameStateEntities;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;


public class LobbyStateActor : TraceActor, IWithTimers
{
    public record BroadcastStateCommand();
    public record BroadcastLobbyState();
    public record RecoverStateCommand(GameState GameState, string LobbyName, long LobbyId, IActorRef LobbyEmitter);

    public ITimerScheduler Timers { get; set; }
    public double TickInterval { get; set; } = .1;

    private long lobbyId;
    private readonly IActorRef supervisor;
    private IActorRef lobbyEmitter;
    private string lobbyName;
    private bool timerEnabled;

    private GameState game;
    public LobbyStateActor(string lobbyName, long lobbyId, IActorRef supervisor, IActorRef lobbyEmitter, bool timerEnabled = true)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;
        this.supervisor = supervisor;
        this.lobbyEmitter = lobbyEmitter;
        this.timerEnabled = timerEnabled;
        game = new GameState()
        {
            Lobby = new LobbyInfo(lobbyId, lobbyName, 0, GameStatus.Joining)
        };
        SubscribeToGameStart(game);

        TraceableReceive<JoinLobbyCommand>(HandleJoinLobbyCommand);
        TraceableReceive<LobbyStateQuery>(HandleLobbyStateQuery);
        TraceableReceive<StartGameCommand>(HandleStartGameCommand);

        TraceableReceive<SessionScoped<GameControlMessages.UpdateKeyStatesCommand>>(HandleUpdateKeyStatesCommand);

        TraceableReceive<Returnable<LobbyStateChangedEvent>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));
        TraceableReceive<Returnable<GameStateBroadcast>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));

        Receive<BroadcastStateCommand>((_) => GameTick());
        Receive<RecoverStateCommand>(HandleRecoverStateCommand);
        Receive<BroadcastLobbyState>((_) => HandleBroadcastLobbyState());
    }

    private void HandleBroadcastLobbyState()
    {
        Log.Info($"Broadcasting lobby state for lobby {lobbyName}");
        var lobbyInfo = new LobbyInfo(lobbyId, lobbyName, game.PlayerCount, game.Status);
        supervisor?.Tell(lobbyInfo);
        if (game.Status == GameStatus.GameOver)
        {
            Timers.Cancel(nameof(BroadcastStateCommand));
        }
    }

    private void HandleRecoverStateCommand(RecoverStateCommand cmd)
    {
        game = cmd.GameState;
        SubscribeToGameStart(game);
        lobbyName = cmd.LobbyName;
        lobbyId = cmd.LobbyId;
        lobbyEmitter = cmd.LobbyEmitter;
    }

    private void SubscribeToGameStart(GameState game)
    {
        game.StatusChanged += (sender, status) =>
        {
            if (status == GameStatus.Playing)
            {
                StartBroadcastOnSchedule();
            }
        };
    }

    private void HandleUpdateKeyStatesCommand(SessionScoped<GameControlMessages.UpdateKeyStatesCommand> msg, Activity? activity)
    {
        Log.Info($"Received key states from {msg.SessionActorPath}");
        var player = game.GetPlayer(msg.SessionActorPath.Split("/").Last());
        player.KeyStates = msg.Message.KeyStates;
    }

    private void HandleStartGameCommand(StartGameCommand cmd, Activity? activity)
    {
        if (game.Status == GameStatus.Joining)
        {
            game.StartGame();
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand(), Self);
        }
        else
        {
            Log.Warning($"Cannot start game in lobby {lobbyName} with state {game.Status}");
        }
    }

    private void GameTick()
    {
        game.Tick();

        if (game.Status == GameStatus.Countdown)
        {
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand(), Self);
        }

        if (game.Status == GameStatus.GameOver)
        {
            Log.Info($"Game over in lobby {lobbyName}");
            Timers.Cancel(nameof(BroadcastStateCommand));
        }

        var e = new GameStateBroadcast(game.ToSnapshot());
        foreach (var kv in game.Players)
            kv.Value.UserSessionActor.Tell(e.ToTraceable(null));
    }

    private void StartBroadcastOnSchedule()
    {
        Log.Info($"Starting broadcast on schedule for lobby {lobbyName}");
        if (timerEnabled)
            Timers.StartPeriodicTimer(nameof(BroadcastStateCommand), new BroadcastStateCommand(), TimeSpan.FromSeconds(.5), TimeSpan.FromSeconds(TickInterval));
    }

    private void HandleLobbyStateQuery(LobbyStateQuery query, Activity? activity)
    {
        var state = game.ToSnapshot();
        var e = new LobbyStateChangedEvent(state);
        Sender.Tell(e.ToTraceable(activity));
    }

    private void HandleJoinLobbyCommand(JoinLobbyCommand cmd, Activity? activity)
    {
        var userSessionActor = Sender;
        if (game.Players.ContainsKey(userSessionActor.Path.Name))
        {
            Log.Info($"Player {userSessionActor.Path.Name} is already in lobby {lobbyName}");
            userSessionActor.Tell(new JoinLobbyEvent(game.ToSnapshot()).ToTraceable(activity));
            return;
        }

        var player = new PlayerState
        {
            UserSessionActor = userSessionActor,
            Username = cmd.UserActorPath.Split("_").Last(),
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };

        game.JoinPlayer(player);

        Log.Info($"Player {userSessionActor.Path.Name} joined lobby {lobbyName}");

        userSessionActor.Tell(new JoinLobbyEvent(game.ToSnapshot())
            .ToTraceable(activity));

        var e = new LobbyStateChangedEvent(game.ToSnapshot());
        foreach (var kv in game.Players)
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), kv.Value.UserSessionActor, e.ToTraceable(activity), Self);
    }

    private void EmitEvent<T>(Traceable<Returnable<T>> returnable)
    {
        lobbyEmitter.Forward(returnable);
    }

    protected override void PreStart()
    {
        base.PreStart();
        Timers.StartPeriodicTimer(nameof(BroadcastLobbyState), new BroadcastLobbyState(), TimeSpan.FromSeconds(.5), TimeSpan.FromSeconds(5));
    }

    public static Props Props(string lobbyName, long lobbyId, IActorRef supervisor, IActorRef lobbyEmitter, bool timerEnabled = true)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId, supervisor, lobbyEmitter, timerEnabled);
    }
}
