using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;
using System.Diagnostics;

namespace Asteroids.Shared.UserSession;
public class UserSessionActor : TraceActor
{
    private string connectionId;
    private readonly string username;

    private readonly IActorRef lobbySupervisor;
    private IActorRef? joinedLobbyActor;

    public UserSessionActor(string connectionId, string username, IActorRef lobbySupervisor)
    {
        this.connectionId = connectionId;
        this.username = username;
        this.lobbySupervisor = lobbySupervisor;

        Receive<SessionScoped<CreateLobbyCommand>>(cmd => ForwardSessionScopedMessage(cmd, lobbySupervisor));
        Receive<SessionScoped<ViewAllLobbiesQuery>>(query => ForwardSessionScopedMessage(query, lobbySupervisor));
        TraceableReceive<SessionScoped<JoinLobbyCommand>>((cmd, activity) => ForwardTracedMessage(cmd, activity, lobbySupervisor));

        TraceableReceive<SessionScoped<LobbyStateQuery>>((query, activity) => ForwardTracedMessage(query, activity, joinedLobbyActor!)); // should have lobby actor after join
        TraceableReceive<SessionScoped<StartGameCommand>>((cmd, activity) => ForwardTracedMessage(cmd, activity, joinedLobbyActor!));

        TraceableReceive<SessionScoped<GameControlMessages.KeyDownCommand>>((cmd, activity) => ForwardTracedSessionScopedMessage(cmd, activity, joinedLobbyActor!));
        TraceableReceive<SessionScoped<GameControlMessages.KeyUpCommand>>((cmd, activity) => ForwardTracedSessionScopedMessage(cmd, activity, joinedLobbyActor!));

        TraceableReceive<JoinLobbyEvent>((e, activity) => HandleJoinLobbyEvent(e, activity)); // maybe I should snag the lobby actor from the join and use it directly
        Receive<CreateLobbyEvent>(e => ForwardLobbyEventToEmitter(e));
        Receive<ViewAllLobbiesResponse>(e => ForwardLobbyEventToEmitter(e));

        TraceableReceive<LobbyStateChangedEvent>((e, activity) => ReturnWrappedInASession(e, activity));
        TraceableReceive<GameStateBroadcast>((e, activity) => ReturnWrappedInASession(e, activity));
    }

    private void HandleJoinLobbyEvent(JoinLobbyEvent e, Activity? activity)
    {
        joinedLobbyActor = Sender; // store the lobby actor for future reference
        ForwardTracedLobbyEventToEmitter(e, activity);
    }

    private void ForwardSessionScopedMessage<T>(SessionScoped<T> sessionScopedMessage, IActorRef actorRef)
    {
        connectionId = sessionScopedMessage.ConnectionId;
        Log.Info($"User {username} received a message of type {typeof(T).Name}");
        actorRef.Tell(sessionScopedMessage.Message);
    }

    private void ForwardTracedMessage<T>(SessionScoped<T> sessionScopedMessage, Activity? activity, IActorRef actorRef)
    {
        connectionId = sessionScopedMessage.ConnectionId;
        Log.Info($"User {username} received a message of type {typeof(T).Name}");
        actorRef.Tell(sessionScopedMessage.Message.ToTraceable(activity));
    }

    private void ForwardTracedSessionScopedMessage<T>(SessionScoped<T> sessionScopedMessage, Activity? activity, IActorRef actorRef)
    {
        connectionId = sessionScopedMessage.ConnectionId;
        Log.Info($"User {username} received a message of type {typeof(T).Name}");
        actorRef.Tell(sessionScopedMessage.ToTraceable(activity));
    }
    private void ForwardLobbyEventToEmitter<T>(T e)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        (lobbySupervisor).Tell(e.ToReturnableMessage(connectionId));
    }

    private void ForwardTracedLobbyEventToEmitter<T>(T e, Activity? activity)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        var sessionScoped = e
            .ToReturnableMessage(connectionId)
            .ToTraceable(activity);
        Context.ActorSelection($"/user/{AkkaHelper.LobbySupervisorActorPath}").Tell(sessionScoped);
    }

    private void ReturnWrappedInASession<T>(T e, Activity? activity)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        var sessionScoped = e
            .ToReturnableMessage(connectionId)
            .ToTraceable(activity);
        Sender.Tell(sessionScoped);
    }

    public static Props Props(string connectionId, string username, IActorRef lobbySupervisor)
    {
        return Akka.Actor.Props.Create<UserSessionActor>(connectionId, username, lobbySupervisor);
    }
}