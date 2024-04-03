using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public class LobbyStateActor : TraceActor
{
    private readonly long lobbyId;
    private readonly string lobbyName;

    private Dictionary<string, IActorRef> playerSessions = new();
    private int playerCount => playerSessions.Count;

    public LobbyStateActor(string lobbyName, long lobbyId)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;

        TraceableReceive<JoinLobbyCommand>((cmd, activity) => HandleJoinLobbyCommand(cmd, activity));
    }

    private void HandleJoinLobbyCommand(JoinLobbyCommand cmd, Activity? activity)
    {
        var userSessionActor = Sender;
        if (playerSessions.ContainsKey(userSessionActor.Path.Name))
        {
            Log.Info($"Player {userSessionActor.Path.Name} is already in lobby {lobbyName}");
            userSessionActor.Tell(new JoinLobbyEvent(lobbyId, lobbyName).ToTraceable(activity));
            return;
        }

        playerSessions.Add(userSessionActor.Path.Name, userSessionActor);
        Log.Info($"Player {userSessionActor.Path.Name} joined lobby {lobbyName}");

        userSessionActor.Tell(new JoinLobbyEvent(lobbyId, lobbyName).ToTraceable(activity));
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(string lobbyName, long lobbyId)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId);
    }
}
