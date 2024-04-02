using Akka.Actor;
using Akka.Event;

namespace Asteroids.Shared.UserSession;
public class UserSessionActor : ReceiveActor
{
    private readonly string connectionId;
    private readonly string username;

    public UserSessionActor(string connectionId, string username)
    {
        this.connectionId = connectionId;
        this.username = username;
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props(string connectionId, string username)
    {
       return Akka.Actor.Props.Create<UserSessionActor>(connectionId, username);
    }
}