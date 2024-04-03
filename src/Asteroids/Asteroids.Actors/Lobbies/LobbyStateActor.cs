using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public class LobbyStateActor : TraceActor
{
    private readonly long lobbyId;
    private readonly string lobbyName;
    private long tick = 0;
    private LobbyState state = LobbyState.Joining;

    private Dictionary<string, (string Username, IActorRef UserSessionActor)> playerSessions = new();
    private int playerCount => playerSessions.Count;

    public LobbyStateActor(string lobbyName, long lobbyId)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;

        TraceableReceive<JoinLobbyCommand>((cmd, activity) => HandleJoinLobbyCommand(cmd, activity));
        TraceableReceive<LobbyStateQuery>((query, activity) => HandleLobbyStateQuery(query, activity));
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

    public GameStateSnapshot GetState()
    {
        return new GameStateSnapshot(new LobbyInfo(lobbyId, lobbyName, playerCount))
        {
            State = state,
            Tick = tick++,
            Players = playerSessions.Values.Select(x => new PlayerStateSnapshot(x.Username)).ToArray()
        };
    }

    public PlayerStateSnapshot GetPlayerState(string playerName)
    {
        return new PlayerStateSnapshot(playerName);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(string lobbyName, long lobbyId)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId);
    }
}
