using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public class LobbyStateActor : TraceActor
{
    public record BroadcastStateCommand();

    private readonly long lobbyId;
    private readonly IActorRef lobbyEmitter;
    private readonly string lobbyName;
    private long tick = 0;
    private LobbyState state = LobbyState.Joining;
    private long countdown = 10;

    private Dictionary<string, (string Username, IActorRef UserSessionActor)> playerSessions = new();
    private int playerCount => playerSessions.Count;

    public LobbyStateActor(string lobbyName, long lobbyId, IActorRef lobbyEmitter)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;
        this.lobbyEmitter = lobbyEmitter;
        TraceableReceive<JoinLobbyCommand>((cmd, activity) => HandleJoinLobbyCommand(cmd, activity));
        TraceableReceive<LobbyStateQuery>((query, activity) => HandleLobbyStateQuery(query, activity));
        TraceableReceive<StartGameCommand>((cmd, activity) => HandleStartGameCommand(cmd, activity));
        TraceableReceive<BroadcastStateCommand>((_, activity) => BroadcastState(activity));


        TraceableReceive<Returnable<LobbyStateChangedEvent>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));
        TraceableReceive<Returnable<GameStateBroadcast>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));
    }

    private void HandleStartGameCommand(StartGameCommand cmd, Activity? activity)
    {
        if (state == LobbyState.Joining)
        {
            state = LobbyState.Countdown;
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand().ToTraceable(activity), Self);

        }
        else
        {
            Log.Warning($"Cannot start game in lobby {lobbyName} with state {state}");
        }
    }

    private void BroadcastState(Activity? activity)
    {
        var e = new GameStateBroadcast(GetState());
        foreach (var kv in playerSessions)
            kv.Value.UserSessionActor.Tell(e.ToTraceable(activity));

        //Log.Info($"Broadcasted state of lobby {lobbyName}");
        if (state == LobbyState.Countdown)
        {
            countdown--;
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand().ToTraceable(null), Self);
            if (countdown == 0)
            {
                state = LobbyState.Playing;
                Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(500), Self, new BroadcastStateCommand().ToTraceable(null), Self);
            }
        }

        if (state == LobbyState.Playing)
        {
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(500), Self, new BroadcastStateCommand().ToTraceable(null), Self);
        }
    }

    private void HandleLobbyStateQuery(LobbyStateQuery query, Activity? activity)
    {
        var state = GetState();
        var e = new LobbyStateChangedEvent(state);
        Sender.Tell(e.ToTraceable(activity));
    }

    private void HandleJoinLobbyCommand(JoinLobbyCommand cmd, Activity? activity)
    {
        var userSessionActor = Sender;
        if (playerSessions.ContainsKey(userSessionActor.Path.Name))
        {
            Log.Info($"Player {userSessionActor.Path.Name} is already in lobby {lobbyName}");
            userSessionActor.Tell(new JoinLobbyEvent(GetState()).ToTraceable(activity));
            return;
        }

        playerSessions.Add(userSessionActor.Path.Name, (cmd.Username, userSessionActor));
        Log.Info($"Player {userSessionActor.Path.Name} joined lobby {lobbyName}");

        userSessionActor.Tell(new JoinLobbyEvent(GetState()).ToTraceable(activity));

        var e = new LobbyStateChangedEvent(GetState());
        foreach (var kv in playerSessions)
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(2), kv.Value.UserSessionActor, e.ToTraceable(activity), Self);
    }
    private void EmitEvent<T>(Traceable<Returnable<T>> returnable)
    {
        Log.Info($"Lobby {lobbyName} received {returnable.Message.Message.GetType().Name}");
        lobbyEmitter.Forward(returnable);
    }

    public GameStateSnapshot GetState()
    {
        return new GameStateSnapshot(new LobbyInfo(lobbyId, lobbyName, playerCount))
        {
            State = state,
            Tick = tick++,
            CountDown = countdown,
            Players = playerSessions.Values.Select(x => new PlayerStateSnapshot(x.Username)).ToArray()
        };
    }

    public PlayerStateSnapshot GetPlayerState(string playerName)
    {
        return new PlayerStateSnapshot(playerName);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(string lobbyName, long lobbyId, IActorRef lobbyEmitter)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId, lobbyEmitter);
    }
}
