using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.GameStateEntities;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public class LobbySupervisor : TraceActor
{
    public record CleanUpLobbyCommand(Guid Id);

    Dictionary<Guid, (IActorRef, LobbyInfo)> lobbies = new();
    private IActorRef lobbiesEmmitterActor;
    private IActorRef lobbyEmitterActor;
    private readonly IActorRef lobbyPersistanceActor;

    public LobbySupervisor(IActorRef lobbiesEmmitterActor, IActorRef lobbyEmitterActor, IActorRef lobbyPersistanceActor)
    {
        this.lobbiesEmmitterActor = lobbiesEmmitterActor;
        this.lobbyEmitterActor = lobbyEmitterActor;
        this.lobbyPersistanceActor = lobbyPersistanceActor;

        DiagnosticConfig.TrackPlayersInLobby(() => lobbies.Values.Sum(x => x.Item2.PlayerCount));

        Receive<CreateLobbyCommand>(HandleCreateLobbyCommand);
        Receive<ViewAllLobbiesQuery>(HandleViewAllLobbiesQuery);
        Receive<LobbyInfo>(HandleLobbyInfo);
        Receive<CleanUpLobbyCommand>(HandleCleanUpLobbyCommand);

        TraceableReceive<CurrentLobbiesResult>(HandleCurrentLobbiesResult);

        TraceableReceive<JoinLobbyCommand>(HandleJoinLobbyCommand);
        TraceableReceive<Returnable<JoinLobbyEvent>>((msg, activity) => HandleReturnable(msg.ToTraceable(activity), lobbiesEmmitterActor));
        // forward all types of returnable events to the emitter
        Receive<IReturnable>((msg) =>
        {
            Log.Info($"LobbySupervisor received {msg.GetType().Name}");
            lobbiesEmmitterActor.Forward(msg);
        });

        Receive<Terminated>(t =>
        {
            var lobbyId = lobbies.First(x => x.Value.Item1 == t.ActorRef).Key;
            lobbies.Remove(lobbyId);

            var persistMsg = new CommitLobbyInfosCommand(Guid.NewGuid(), lobbies.Values.Select(x => x.Item2).ToList());
            lobbyPersistanceActor.Tell(persistMsg.ToTraceable(null));
        });
    }

    private void HandleCurrentLobbiesResult(CurrentLobbiesResult result, Activity? activity)
    {
        Log.Info($"LobbySupervisor received {result.GetType().Name}, Initializing {result.Lobbies.Count} lobbies");
        lobbies.Clear();
        foreach (var lobbyInfo in result.Lobbies)
        {
            var lobbyStateActor = Context.ActorOf(LobbyStateActor.Props(lobbyInfo.Name, lobbyInfo.Id, Self, lobbyEmitterActor, lobbyPersistanceActor), $"lobby-{lobbyInfo.Id}");
            Context.Watch(lobbyStateActor);
            lobbies.Add(lobbyInfo.Id, (lobbyStateActor, lobbyInfo));
        }
    }

    private void HandleCleanUpLobbyCommand(CleanUpLobbyCommand command)
    {
        if (!lobbies.ContainsKey(command.Id)) return;

        Log.Info($"Cleaning up lobby {command.Id}");
        var lobby = lobbies[command.Id];
        lobby.Item1.Tell(PoisonPill.Instance);
    }

    private void HandleLobbyInfo(LobbyInfo info)
    {
        // update lobbies
        if (lobbies.ContainsKey(info.Id))
        {
            lobbies[info.Id] = (lobbies[info.Id].Item1, info);
        }
        var viewAllLobbies = new ViewAllLobbiesResponse(lobbies.Values.Select(x => x.Item2).ToList());
        lobbiesEmmitterActor.Tell(viewAllLobbies);

        if (info.Status == GameStatus.GameOver)
        {
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMinutes(1), Self, new CleanUpLobbyCommand(info.Id), Self);
        }
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
            Sender.Tell(new JoinLobbyEvent(null, "Lobby not found"));
        }
    }

    private void HandleCreateLobbyCommand(CreateLobbyCommand cmd)
    {
        Log.Info($"Creating lobby {cmd.Name} with countdown {cmd.GameParameters.CountdownSeconds}");
        var maxLobbyId = lobbies.Keys.DefaultIfEmpty().Max();
        var newLobbyId = Guid.NewGuid();

        var lobbyInfo = new LobbyInfo(newLobbyId, cmd.Name, 0, GameStatus.Joining);
        var lobbyStateActor = Context.ActorOf(LobbyStateActor.Props(cmd.Name, newLobbyId, Self, lobbyEmitterActor, lobbyPersistanceActor, gameParameters: cmd.GameParameters), $"lobby-{newLobbyId}");
        Context.Watch(lobbyStateActor);
        lobbies.Add(newLobbyId, (lobbyStateActor, lobbyInfo));

        DiagnosticConfig.LobbiesCreatedCounter.Add(1);

        var lobbyInfos = lobbies.Values.Select(x => x.Item2).ToList();
        lobbiesEmmitterActor.Tell(new CreateLobbyEvent(lobbyInfos));

        var persistMsg = new CommitLobbyInfosCommand(Guid.NewGuid(), lobbyInfos);
        lobbyPersistanceActor.Tell(persistMsg.ToTraceable(null));
    }

    private void HandleViewAllLobbiesQuery(ViewAllLobbiesQuery query)
    {
        var lobbyInfos = lobbies.Values.Select(x => x.Item2).ToList();
        lobbiesEmmitterActor.Tell(new ViewAllLobbiesResponse(lobbyInfos));
    }

    private void HandleReturnable<T>(Traceable<Returnable<T>> returnable, IActorRef toActor)
    {
        toActor.Forward(returnable);
    }

    protected override void PreStart()
    {
        base.PreStart();
        Log.Info("LobbySupervisor started");

        var lobbiesQuery = new CurrentLobbiesQuery(Guid.NewGuid());
        lobbyPersistanceActor.Tell(lobbiesQuery.ToTraceable(null));
    }

    public static Props Props(IActorRef lobbiesEmmitterActor, IActorRef lobbyEmitterActor, IActorRef lobbyPersistanceActor)
    {
        return Akka.Actor.Props.Create(() => new LobbySupervisor(lobbiesEmmitterActor, lobbyEmitterActor, lobbyPersistanceActor));
    }
}
