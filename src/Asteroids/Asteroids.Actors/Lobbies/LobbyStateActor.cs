using Akka.Actor;
using Akka.Event;

namespace Asteroids.Shared.Lobbies;

public class LobbyStateActor : ReceiveActor
{
    public LobbyStateActor()
    {

    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props()
    {
        return Akka.Actor.Props.Create<LobbyStateActor>();
    }
}
