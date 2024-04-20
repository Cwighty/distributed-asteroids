using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.GameStateEntities;
using System.Diagnostics;
using System.Xml;

namespace Asteroids.Shared.Lobbies;


public record RecoverGameStateCommand(GameState GameState, string LobbyName, Guid LobbyId);
public class LobbyStateActor : TraceActor, IWithTimers
{
    public record BroadcastStateCommand();
    public record BroadcastLobbyState();

    public ITimerScheduler Timers { get; set; }
    public double TickInterval { get; set; } = .1;

    private Guid lobbyId;
    private readonly IActorRef supervisor;
    private IActorRef lobbyEmitter;
    private IActorRef? lobbyPersister;
    private string lobbyName;
    private bool timerEnabled;

    private bool persisterResponded = false;

    private GameState game;
    public LobbyStateActor(string lobbyName, Guid lobbyId, IActorRef supervisor, IActorRef lobbyEmitter, IActorRef lobbyPersister, bool timerEnabled = true)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;
        this.supervisor = supervisor;
        this.lobbyEmitter = lobbyEmitter;
        this.lobbyPersister = lobbyPersister;
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
        Receive<RecoverGameStateCommand>(HandleRecoverStateCommand);
        Receive<BroadcastLobbyState>((_) => HandleBroadcastLobbyState());

        TraceableReceive<CurrentLobbyStateQuery>(HandleCurrentLobbyStateQuery);
        TraceableReceive<CurrentLobbyStateResult>((msg, activity) =>
        {
            Log.Info($"Received CurrentLobbyStateResult for lobby {lobbyName}");
            persisterResponded = true;
            HandleRecoverStateCommand(msg.GameState);
        });
    }

    private void HandleCurrentLobbyStateQuery(CurrentLobbyStateQuery query, Activity? activity)
    {
        if (persisterResponded)
        {
            return;
        }
        Log.Info($"Received CurrentLobbyStateQuery for lobby {lobbyName}");
        lobbyPersister?.Tell(query.ToTraceable(activity));
        // schedule to tell self in 1 second
        Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, query.ToTraceable(activity), Self);
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

        var recoverCmd = new RecoverGameStateCommand(game, lobbyName, lobbyId);
        var persistMsg = new CommitLobbyStateCommand(Guid.NewGuid(), recoverCmd);
        lobbyPersister.Tell(persistMsg.ToTraceable(null));
    }

    private void HandleRecoverStateCommand(RecoverGameStateCommand cmd)
    {
        Log.Info($"Recovering state for lobby {lobbyName}");
        lobbyName = cmd.LobbyName;
        lobbyId = cmd.LobbyId;
        game = cmd.GameState;
        if (game.Status == GameStatus.Joining)
            SubscribeToGameStart(game);
        else if (game.Status == GameStatus.Playing)
            StartBroadcastOnSchedule();
    }

    private void SubscribeToGameStart(GameState game)
    {
        game.StatusChanged += (sender, status) =>
        {
            if (status == GameStatus.Playing)
            {
                StartBroadcastOnSchedule();
            }

            if (status == GameStatus.GameOver)
                DiagnosticConfig.LobbiesDestroyedCounter.Add(1);
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
        Log.Info($"Starting game in lobby {lobbyName}");
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
        DiagnosticConfig.GameTickCounter.Add(1);
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
        {
            var actor = Context.ActorSelection(kv.Value.UserSessionActorPath);
            // Log.Info($"Sending GameStateBroadcast to {actor.Path}");
            actor.Tell(e.ToTraceable(null));
        }

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
            UserSessionActorPath = userSessionActor.Path.ToString(),
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
        {
            var actor = Context.ActorSelection(kv.Value.UserSessionActorPath);
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), actor, e.ToTraceable(activity), Self);
        }
    }

    private void EmitEvent<T>(Traceable<Returnable<T>> returnable)
    {
        lobbyEmitter.Forward(returnable);
    }

    protected override void PreStart()
    {
        base.PreStart();
        Log.Info("LobbyStateActor started for lobby {LobbyName}", lobbyName);
        Timers.StartPeriodicTimer(nameof(BroadcastLobbyState), new BroadcastLobbyState(), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        // ask for lobby state
        Self.Tell(new CurrentLobbyStateQuery(Guid.NewGuid(), lobbyId).ToTraceable(null));
    }

    public static Props Props(string lobbyName, Guid lobbyId, IActorRef supervisor, IActorRef lobbyEmitter, IActorRef? lobbyPersister = null, bool timerEnabled = true)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId, supervisor, lobbyEmitter, lobbyPersister, timerEnabled);
    }
}
