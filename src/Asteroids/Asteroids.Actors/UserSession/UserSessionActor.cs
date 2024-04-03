using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.UserSession;
public class UserSessionActor : ReceiveActor
{
    private string connectionId;
    private readonly string username;

    public UserSessionActor(string connectionId, string username)
    {
        this.connectionId = connectionId;
        this.username = username;

        Receive<SessionScoped<CreateLobbyCommand>>(cmd => ForwardSessionScopedMessage(cmd, AkkaHelper.LobbySupervisorActorPath));
        Receive<SessionScoped<ViewAllLobbiesQuery>>(query => ForwardSessionScopedMessage(query, AkkaHelper.LobbySupervisorActorPath));

        Receive<CreateLobbyEvent>(e => ForwardLobbyEventToEmitter(e));
        Receive<ViewAllLobbiesResponse>(e => ForwardLobbyEventToEmitter(e));
    }

    private void ForwardSessionScopedMessage<T>(SessionScoped<T> sessionScopedMessage, string supervisorPath)
    {
        connectionId = sessionScopedMessage.ConnectionId;
        Log.Info($"User {username} received a message of type {typeof(T).Name}");
        Context.ActorSelection($"/user/{supervisorPath}").Tell(sessionScopedMessage.Message);
    }

    private void ForwardLobbyEventToEmitter<T>(T e)
    {
        Log.Info($"User {username} received a lobby event of type {e.GetType()}, forwarding to emitter");
        Context.ActorSelection($"/user/{AkkaHelper.LobbySupervisorActorPath}").Tell(e.ToReturnableMessage(connectionId));
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(string connectionId, string username)
    {
        return Akka.Actor.Props.Create<UserSessionActor>(connectionId, username);
    }
}