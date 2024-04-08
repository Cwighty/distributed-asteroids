
using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.UserSession;

public record StartUserSessionCommmand(string ConnectionId, string Username) : IReturnableMessage;
public record StartUserSessionEvent(string ConnectionId, string SessionActorPath) : IReturnableMessage;

public record FindUserSessionRefQuery(string ActorPath);
public record FindUserSessionResult(IActorRef? UserSessionRef);

public class UserSessionSupervisor : ReceiveActor
{
    public record FetchedLobbySupervisorEvent(IActorRef LobbySupervisor);

    IActorRef lobbySupervisor;
    private Dictionary<string, IActorRef> userSessions = new();

    public UserSessionSupervisor(IActorRef lobbySupervisor)
    {
        this.lobbySupervisor = lobbySupervisor;
        Receive<FetchedLobbySupervisorEvent>(e => lobbySupervisor = e.LobbySupervisor);

        Receive<StartUserSessionCommmand>(cmd => HandleStartUserSession(cmd));
        Receive<SessionScoped<FindUserSessionRefQuery>>(query => HandleFindUserSessionRef(query));
    }

    private void HandleFindUserSessionRef(SessionScoped<FindUserSessionRefQuery> query)
    {
        Log.Info($"Finding user session for {query.Message.ActorPath}");
        try
        {
            var actorRef = Context.ActorSelection(query.Message.ActorPath).ResolveOne(TimeSpan.FromSeconds(5)).Result;
            Sender.Tell(new FindUserSessionResult(actorRef));
        }
        catch
        {
            Log.Info($"User session for {query.Message.ActorPath} not found.");
            var invalidSession = new InvalidSessionEvent().ToReturnableMessage(query.ConnectionId);
            Context.ActorSelection($"/user/{AkkaHelper.LobbySupervisorActorPath}").Tell(invalidSession);
            Sender.Tell(new FindUserSessionResult(null));
        }
    }

    private void HandleStartUserSession(StartUserSessionCommmand cmd)
    {
        var actorPath = GetUserSessionActorPath(cmd.Username);
        if (userSessions.ContainsKey(actorPath))
        {
            Log.Info($"User session for {cmd.Username} already exists.");
            Sender.Tell(new StartUserSessionEvent(cmd.ConnectionId, userSessions[actorPath].Path.ToStringWithAddress()));
        }
        else
        {
            Log.Info($"Creating user session for {cmd.Username}");

            var userSessionActor = Context.ActorOf(UserSessionActor.Props(cmd.ConnectionId, cmd.Username, lobbySupervisor), actorPath);

            userSessions.Add(actorPath, userSessionActor);
            Sender.Tell(new StartUserSessionEvent(cmd.ConnectionId, userSessionActor.Path.ToStringWithAddress()));
        }
    }

    private string GetUserSessionActorPath(string username)
    {
        var validActorPath = AkkaHelper.UsernameToActorPath(username);
        return $"user-session_{validActorPath}";
    }

    protected override void PreStart()
    {
        Log.Info("UserSessionSupervisor started");
        // Context.ActorSelection($"/user/{AkkaHelper.LobbySupervisorActorPath}")
        //     .ResolveOne(TimeSpan.FromSeconds(5))
        //     .ContinueWith(task => new FetchedLobbySupervisorEvent(task.Result))
        //     .PipeTo(Self);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(IActorRef lobbySupervisor)
    {
        return Akka.Actor.Props.Create<UserSessionSupervisor>(lobbySupervisor);
    }
}
