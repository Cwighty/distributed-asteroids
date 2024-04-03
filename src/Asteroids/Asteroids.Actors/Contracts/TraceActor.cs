using Akka.Actor;
using Akka.Event;
using System.Diagnostics;

namespace Asteroids.Shared.Contracts;

public abstract class TraceActor : ReceiveActor
{
    public TraceActor()
    {

    }

    protected void TraceableReceive<T>(Action<T, Activity?> handler)
    {
        Receive<Traceable<T>>(tc =>
        {
            using var activity = tc.Activity($"{this.GetType().Name}: {typeof(T).Name}");
            T message = tc.Message;
            handler(message, activity);
        });
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();
    public static Props Props()
    {
        return Akka.Actor.Props.Create<TraceActor>();
    }
}
