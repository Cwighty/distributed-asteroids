using Akka.Actor;
using Akka.Event;
using Akka.Routing;
using Asteroids.Actors;

namespace Asteroids.Shared.Actors;
public sealed class NewMessage : IConsistentHashable
{
    public NewMessage(long requestId, string message)
    {
        RequestId = requestId;
        Message = message;
    }

    public long RequestId { get; }
    public string Message { get; }

    public object ConsistentHashKey => RequestId;
}


public class MessageActor : ReceiveActor
{
    public MessageActor()
    {
        Receive<NewMessage>(msg =>
        {
            Log.Info($"Received message: {msg.Message}");
            var signalRActor = Context.ActorOf(SignalRActor.Props());
            signalRActor.Tell(Self.Path.ToString());
        });
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props() =>
        Akka.Actor.Props.Create<MessageActor>();
}

