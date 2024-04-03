using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;

namespace Asteroids.Shared.Lobbies;

public class LobbySupervisor : ReceiveActor
{

    Dictionary<long, (IActorRef, LobbyInfo)> lobbies = new();
    private IActorRef lobbyEmmitterActor;

    public LobbySupervisor()
    {
        lobbyEmmitterActor = Context.ActorOf(LobbyEmitterActor.Props(), "lobby-emitter");

        Receive<CreateLobbyCommand>(cmd => HandleCreateLobbyCommand(cmd));
        Receive<ViewAllLobbiesQuery>(query => HandleViewAllLobbiesQuery(query));

        // forward all types of returnable events to the emitter
        Receive<IReturnable>((msg) =>
        {
            Log.Info($"LobbySupervisor received {msg.GetType().Name}");
            lobbyEmmitterActor.Forward(msg);
        });

        Receive<Terminated>(t =>
        {
            var lobbyId = lobbies.First(x => x.Value.Item1 == t.ActorRef).Key;
            lobbies.Remove(lobbyId);
        });
    }

    private void HandleCreateLobbyCommand(CreateLobbyCommand cmd)
    {
        var maxLobbyId = lobbies.Keys.DefaultIfEmpty().Max();
        var newLobbyId = maxLobbyId + 1;

        var lobbyInfo = new LobbyInfo(newLobbyId, cmd.Name, 0);
        var lobbyStateActor = Context.ActorOf(LobbyStateActor.Props(), $"lobby-{newLobbyId}");
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

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props()
    {
        return Akka.Actor.Props.Create<LobbySupervisor>();
    }
}
