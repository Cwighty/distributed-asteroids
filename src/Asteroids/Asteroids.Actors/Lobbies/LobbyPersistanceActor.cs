using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Akka.Util.Internal;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;

namespace Asteroids.Shared.Lobbies;


public record CurrentLobbiesQuery(Guid RequestId);
public record CurrentLobbiesResult(Guid RequestId, List<LobbyInfo> Lobbies);

public record CurrentLobbyStateQuery(Guid RequestId, long LobbyId);
public record CurrentLobbyStateResult(Guid RequestId, RecoverGameStateCommand GameState);


public record CommitLobbyInfosCommand(Guid RequestId, List<LobbyInfo> Lobbies);
public record CommitLobbyStateCommand(Guid RequestId, RecoverGameStateCommand RecoverGameStateCommand);

public record LobbiesCommittedEvent(CommitLobbyInfosCommand OriginalCommand, bool Success, string Error = "");
public record LobbyStateCommittedEvent(CommitLobbyStateCommand OriginalCommand, bool Success, string Error = "");

//public record SubscribeToAccountChanges(IActorRef Subscriber);

public class LobbyPersistenceActor : TraceActor
{
    private IActorRef? lobbySupervisor;
    public record InitializeLobbies(List<LobbyInfo> Lobbies);
    public record InitializeLobbyState(RecoverGameStateCommand? RecoverGameStateCommand);

    IStorageService storageService;
    const string LOBBIES_KEY = "lobbies";
    const string LOBBY_STATES_KEY = "lobby-states";
    private string GetLobbyKey(long id) => $"lobby-{id}";

    private List<LobbyInfo> _lobbies = new();
    private Dictionary<long, RecoverGameStateCommand> _lobbyStates = new();

    private Dictionary<Guid, IActorRef> _commitRequests = new();

    public LobbyPersistenceActor(IServiceProvider sp)
    {
        storageService = sp.GetRequiredService<IStorageService>();

        Receive<InitializeLobbies>(HandleInitializeLobbies);
        Receive<InitializeLobbyState>(HandleInitializeLobbyState);
        Receive<LobbiesCommittedEvent>(HandleLobbiesCommittedEvent);
        Receive<LobbyStateCommittedEvent>(HandleLobbyStateCommittedEvent);

        TraceableReceive<CommitLobbyInfosCommand>(HandleCommitLobbiesCommand);
        TraceableReceive<CommitLobbyStateCommand>(HandleCommitLobbyStateCommand);
        TraceableReceive<CurrentLobbiesQuery>(HandleCurrentLobbiesQuery);
        TraceableReceive<CurrentLobbyStateQuery>(HandleCurrentLobbyStateQuery);
    }

    private void HandleCurrentLobbyStateQuery(CurrentLobbyStateQuery query, Activity? activity)
    {
        Log.Info($"Sending current lobby state for lobby {query.LobbyId}");
        var success = _lobbyStates.TryGetValue(query.LobbyId, out var state);
        if (!success)
        {
            Log.Error("No state found for lobby");
            return;
        }
        var msg = new CurrentLobbyStateResult(query.RequestId, state);
        Sender.Tell(msg.ToTraceable(activity));
    }

    private void HandleCurrentLobbiesQuery(CurrentLobbiesQuery query, Activity? activity)
    {
        lobbySupervisor = Sender;
        Log.Info("Sending current lobbies to {Sender}", Sender.Path.Name);
        var msg = new CurrentLobbiesResult(query.RequestId, _lobbies);
        Sender.Tell(msg.ToTraceable(activity));
    }

    private void HandleInitializeLobbyState(InitializeLobbyState state)
    {
        if (state.RecoverGameStateCommand is null) return;
        Log.Info("Initialized lobby state for lobby {LobbyId}", state.RecoverGameStateCommand.LobbyId);

        _lobbyStates.AddOrSet(state.RecoverGameStateCommand.LobbyId, state.RecoverGameStateCommand!);
    }

    private void HandleInitializeLobbies(InitializeLobbies lobbies)
    {
        _lobbies = lobbies.Lobbies;

        foreach (var lobby in lobbies.Lobbies)
        {
            var key = GetLobbyKey(lobby.Id);
            var task = storageService.StrongGet(key);
            task.ContinueWith(r =>
            {
                if (r.IsFaulted)
                {
                    Log.Error(r.Exception, "Failed to initialize lobby state");
                    return new InitializeLobbyState(null);
                }
                else
                {
                    if (string.IsNullOrEmpty(r.Result.Value))
                    {
                        return new InitializeLobbyState(null);
                    }
                    else
                    {
                        try
                        {
                            var deserialize = JsonSerializer.Deserialize<RecoverGameStateCommand>(r.Result.Value);
                            return new InitializeLobbyState(deserialize!);
                        }
                        catch
                        {
                            return new InitializeLobbyState(null);
                        }
                    }
                }
            }).PipeTo(Self);

            lobbySupervisor?.Tell(new CurrentLobbiesResult(Guid.NewGuid(), _lobbies)); // sometimes the lobby supervisor asks before the lobbies are initialized, so tell it anyway
        }
    }

    private void HandleCommitLobbiesCommand(CommitLobbyInfosCommand command, Activity? activity)
    {
        _commitRequests.Add(command.RequestId, Sender);

        // commit
        var unmodified = JsonSerializer.Serialize(_lobbies ?? new List<LobbyInfo>());
        var reducer = (string oldValue) =>
        {
            return JsonSerializer.Serialize(command.Lobbies);
        };
        var task = storageService.IdempodentReduceUntilSuccess(LOBBIES_KEY, unmodified, reducer);

        task.ContinueWith(r =>
        {
            if (r.IsFaulted)
            {
                Log.Error(r.Exception, "Failed to commit lobby info to lobbies record");
                return new LobbiesCommittedEvent(command, false, r.Exception.Message);
            }
            else
            {
                return new LobbiesCommittedEvent(command, true);
            }
        }).PipeTo(Self);
    }

    private void HandleLobbiesCommittedEvent(LobbiesCommittedEvent e)
    {
        // notify original requestor
        if (e.Success)
        {
            _lobbies = e.OriginalCommand.Lobbies;
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
        else
        {
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
    }

    private void HandleCommitLobbyStateCommand(CommitLobbyStateCommand command, Activity? activity)
    {
        _commitRequests.Add(command.RequestId, Sender);

        // commit
        _lobbyStates.TryGetValue(command.RecoverGameStateCommand.LobbyId, out var state);
        var unmodified = JsonSerializer.Serialize(state) ?? string.Empty;
        var reducer = (string oldValue) =>
        {
            var oldLobbyStates = JsonSerializer.Deserialize<RecoverGameStateCommand>(oldValue);
            var modifiedLobbyStates = command.RecoverGameStateCommand;
            return JsonSerializer.Serialize(modifiedLobbyStates);
        };
        var task = storageService.IdempodentReduceUntilSuccess(GetLobbyKey(command.RecoverGameStateCommand.LobbyId), unmodified, reducer);

        task.ContinueWith(r =>
        {
            if (r.IsFaulted)
            {
                Log.Error(r.Exception, "Failed to commit lobby state to lobby state record for lobby {LobbyId}", command.RecoverGameStateCommand.LobbyId);
                return new LobbyStateCommittedEvent(command, false, r.Exception.Message);
            }
            else
            {
                return new LobbyStateCommittedEvent(command, true);
            }
        }).PipeTo(Self);
    }

    private void HandleLobbyStateCommittedEvent(LobbyStateCommittedEvent e)
    {
        // notify original requestor
        if (e.Success)
        {
            _lobbyStates[e.OriginalCommand.RecoverGameStateCommand.LobbyId] = e.OriginalCommand.RecoverGameStateCommand;
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
        else
        {
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
    }


    protected override void PreStart()
    {
        Log.Info("Lobby Persistence Actor started, initializing lobbies from storage");
        var lobbiesTask = storageService.StrongGet(LOBBIES_KEY);

        lobbiesTask.ContinueWith(r =>
        {
            if (string.IsNullOrEmpty(r.Result.Value))
            {
                return new InitializeLobbies(new List<LobbyInfo>());
            }
            else
            {
                try
                {
                    var deserialize = JsonSerializer.Deserialize<List<LobbyInfo>>(r.Result.Value);
                    return new InitializeLobbies(deserialize!);
                }
                catch (JsonException)
                {
                    return new InitializeLobbies(new List<LobbyInfo>());
                }
            }
        }).PipeTo(Self);

    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<LobbyPersistenceActor>();
    }
    public static Props Props(ActorSystem system)
    {
        var spExtension = DependencyResolver.For(system);
        return spExtension.Props<LobbyPersistenceActor>();
    }

    public static Props Props(IServiceProvider sp)
    {
        return Akka.Actor.Props.Create(() => new LobbyPersistenceActor(sp));
    }
}

