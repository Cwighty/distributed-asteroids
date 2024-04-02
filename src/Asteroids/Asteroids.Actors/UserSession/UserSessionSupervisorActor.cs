
using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;

namespace Asteroids.Shared.UserSession;

public record StartUserSessionCommmand(string ConnectionId, string Username) : IReturnableMessage;
public record StartUserSessionEvent(string ConnectionId, string SessionActorPath) : IReturnableMessage;

public class UserSessionSupervisor : ReceiveActor
{
    private Dictionary<string, IActorRef> userSessions = new();

    public UserSessionSupervisor()
    {
        Receive<StartUserSessionCommmand>(cmd => HandleStartUserSession(cmd));
    }

    private void HandleStartUserSession(StartUserSessionCommmand cmd)
    {
        if (userSessions.ContainsKey(cmd.Username))
        {
            Log.Info($"User session for {cmd.Username} already exists.");
            Sender.Tell(new StartUserSessionEvent(cmd.ConnectionId, userSessions[cmd.Username].Path.ToStringWithAddress()));
        }
        else
        {
            Log.Info($"Creating user session for {cmd.Username}");

            var actorPath = GetUserSessionActorPath(cmd.Username);
            var userSessionActor = Context.ActorOf(UserSessionActor.Props(cmd.ConnectionId, cmd.Username), actorPath);

            userSessions.Add(cmd.Username, userSessionActor);
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
