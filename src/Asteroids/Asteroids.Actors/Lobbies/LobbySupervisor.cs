using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public class LobbySupervisor : TraceActor
{

    Dictionary<long, (IActorRef, LobbyInfo)> lobbies = new();
    private IActorRef lobbyEmmitterActor;

    public LobbySupervisor()
    {
        lobbyEmmitterActor = Context.ActorOf(LobbyEmitterActor.Props(), "lobby-emitter");

        Receive<CreateLobbyCommand>(cmd => HandleCreateLobbyCommand(cmd));
        Receive<ViewAllLobbiesQuery>(query => HandleViewAllLobbiesQuery(query));
        TraceableReceive<JoinLobbyCommand>((cmd, activity) => HandleJoinLobbyCommand(cmd, activity));

        // forward all types of returnable events to the emitter
        Receive<IReturnable>((msg) =>
        {
            Log.Info($"LobbySupervisor received {msg.GetType().Name}");
            lobbyEmmitterActor.Forward(msg);
        });

        TraceableReceive<Returnable<JoinLobbyEvent>>((msg, activity) => HandleReturnable(msg.ToTraceable(activity)));

        Receive<Terminated>(t =>
        {
            var lobbyId = lobbies.First(x => x.Value.Item1 == t.ActorRef).Key;
            lobbies.Remove(lobbyId);
        });
    }

    private void HandleJoinLobbyCommand(JoinLobbyCommand cmd, Activity? activity)
    {
        if (lobbies.TryGetValue(cmd.Id, out var lobby))
        {
            var (lobbyStateActor, lobbyInfo) = lobby;
            // forward to keep the sender as the user session actor
            lobbyStateActor.Forward(cmd.ToTraceable(activity));
        }
        else
        {
            Sender.Tell(new JoinLobbyEvent(cmd.Id, string.Empty, "Lobby not found"));
        }
    }

    private void HandleCreateLobbyCommand(CreateLobbyCommand cmd)
    {
        var maxLobbyId = lobbies.Keys.DefaultIfEmpty().Max();
        var newLobbyId = maxLobbyId + 1;

        var lobbyInfo = new LobbyInfo(newLobbyId, cmd.Name, 0);
        var lobbyStateActor = Context.ActorOf(LobbyStateActor.Props(cmd.Name, newLobbyId), $"lobby-{newLobbyId}");
        Context.Watch(lobbyStateActor);
        lobbies.Add(newLobbyId, (lobbyStateActor, lobbyInfo));

        var lobbyInfos = lobbies.Values.Select(x => x.Item2).ToList();
        Sender.Tell(new CreateLobbyEvent(lobbyInfos));
    }

    private void HandleViewAllLobbiesQuery(ViewAllLobbiesQuery query)
    {
        var lobbyInfos = lobbies.Values.Select(x => x.Item2).ToList();
        Sender.Tell(new ViewAllLobbiesResponse(lobbyInfos));
    }

    private void HandleReturnable<T>(T returnable)
    {
        Log.Info($"LobbySupervisor received {returnable.GetType().Name}");
        lobbyEmmitterActor.Forward(returnable);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props()
    {
        return Akka.Actor.Props.Create<LobbySupervisor>();
    }
}
