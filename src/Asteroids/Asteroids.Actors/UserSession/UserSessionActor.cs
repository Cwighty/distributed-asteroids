using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;
using System.Diagnostics;

namespace Asteroids.Shared.UserSession;
public class UserSessionActor : TraceActor
{
    private string connectionId;
    private readonly string username;

    public UserSessionActor(string connectionId, string username)
    {
        this.connectionId = connectionId;
        this.username = username;

        Receive<SessionScoped<CreateLobbyCommand>>(cmd => ForwardSessionScopedMessage(cmd, AkkaHelper.LobbySupervisorActorPath));
        Receive<SessionScoped<ViewAllLobbiesQuery>>(query => ForwardSessionScopedMessage(query, AkkaHelper.LobbySupervisorActorPath));
        TraceableReceive<SessionScoped<JoinLobbyCommand>>((cmd, activity) => ForwardTracedSessionScopedMessage(cmd, activity, AkkaHelper.LobbySupervisorActorPath));
        TraceableReceive<SessionScoped<LobbyStateQuery>>((query, activity) => ForwardTracedSessionScopedMessage(query, activity, AkkaHelper.LobbySupervisorActorPath));
        TraceableReceive<SessionScoped<StartGameCommand>>((cmd, activity) => ForwardTracedSessionScopedMessage(cmd, activity, AkkaHelper.LobbySupervisorActorPath));

        Receive<CreateLobbyEvent>(e => ForwardLobbyEventToEmitter(e));
        Receive<ViewAllLobbiesResponse>(e => ForwardLobbyEventToEmitter(e));
        TraceableReceive<JoinLobbyEvent>((e, activity) => ForwardTracedLobbyEventToEmitter(e, activity));
        TraceableReceive<LobbyStateChangedEvent>((e, activity) => ReturnWithSession(e, activity));
        TraceableReceive<GameStateBroadcast>((e, activity) => ReturnWithSession(e, activity));
    }

    private void ForwardSessionScopedMessage<T>(SessionScoped<T> sessionScopedMessage, string supervisorPath)
    {
        connectionId = sessionScopedMessage.ConnectionId;
        Log.Info($"User {username} received a message of type {typeof(T).Name}");
        Context.ActorSelection($"/user/{supervisorPath}").Tell(sessionScopedMessage.Message);
    }
    private void ForwardTracedSessionScopedMessage<T>(SessionScoped<T> sessionScopedMessage, Activity? activity, string supervisorPath)
    {
        connectionId = sessionScopedMessage.ConnectionId;
        Log.Info($"User {username} received a message of type {typeof(T).Name}");
        Context.ActorSelection($"/user/{supervisorPath}").Tell(sessionScopedMessage.Message.ToTraceable(activity));
    }

    private void ForwardLobbyEventToEmitter<T>(T e)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        Context.ActorSelection($"/user/{AkkaHelper.LobbySupervisorActorPath}").Tell(e.ToReturnableMessage(connectionId));
    }

    private void ForwardTracedLobbyEventToEmitter<T>(T e, Activity? activity)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        var sessionScoped = e
            .ToReturnableMessage(connectionId)
            .ToTraceable(activity);
        Context.ActorSelection($"/user/{AkkaHelper.LobbySupervisorActorPath}").Tell(sessionScoped);
    }

    private void ReturnWithSession<T>(T e, Activity? activity)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        var sessionScoped = e
            .ToReturnableMessage(connectionId)
            .ToTraceable(activity);
        Sender.Tell(sessionScoped);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(string connectionId, string username)
    {
        return Akka.Actor.Props.Create<UserSessionActor>(connectionId, username);
    }
}