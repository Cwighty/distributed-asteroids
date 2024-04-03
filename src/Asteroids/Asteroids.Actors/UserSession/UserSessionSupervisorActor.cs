
using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;

namespace Asteroids.Shared.UserSession;

public record StartUserSessionCommmand(string ConnectionId, string Username) : IReturnableMessage;
public record StartUserSessionEvent(string ConnectionId, string SessionActorPath) : IReturnableMessage;

public record FindUserSessionRefQuery(string ActorPath);
public record FindUserSessionResult(IActorRef? UserSessionRef);

public class UserSessionSupervisor : ReceiveActor
{
    private Dictionary<string, IActorRef> userSessions = new();

    public UserSessionSupervisor()
    {
        Receive<StartUserSessionCommmand>(cmd => HandleStartUserSession(cmd));
        Receive<FindUserSessionRefQuery>(query => HanldeFindUserSessionRef(query));
    }

    private void HanldeFindUserSessionRef(FindUserSessionRefQuery query)
    {
        Log.Info($"Finding user session for {query.ActorPath}");
        try
        {
            var actorRef = Context.ActorSelection(query.ActorPath).ResolveOne(TimeSpan.FromSeconds(5)).Result;
            Sender.Tell(new FindUserSessionResult(actorRef));
        }
        catch
        {
            Log.Info($"User session for {query.ActorPath} not found.");
            Sender.Tell(new FindUserSessionResult(null));
        }
    }

    private void HandleStartUserSession(StartUserSessionCommmand cmd)
    {
        var actorPath = GetUserSessionActorPath(cmd.Username);
        if (userSessions.ContainsKey(actorPath))
        {
            Log.Info($"User session for {cmd.Username} already exists.");
            Sender.Tell(new StartUserSessionEvent(cmd.ConnectionId, userSessions[cmd.Username].Path.ToStringWithAddress()));
        }
        else
        {
            Log.Info($"Creating user session for {cmd.Username}");

            var userSessionActor = Context.ActorOf(UserSessionActor.Props(cmd.ConnectionId, cmd.Username), actorPath);

            userSessions.Add(actorPath, userSessionActor);
            Sender.Tell(new StartUserSessionEvent(cmd.ConnectionId, userSessionActor.Path.ToStringWithAddress()));
        }
    }

    private string GetUserSessionActorPath(string username)
    {
        var validActorPath = AkkaHelper.UsernameToActorPath(username);
        return $"user-session_{validActorPath}";
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props()
    {
        return Akka.Actor.Props.Create<UserSessionSupervisor>();
    }
}
