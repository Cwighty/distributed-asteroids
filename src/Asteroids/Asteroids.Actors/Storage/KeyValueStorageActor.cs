using Akka.Actor;
using Akka.Event;

namespace Asteroids.Shared.Storage;

public class KeyValueStorageActor : ReceiveActor
{
    public Dictionary<string,string> Data { get; } = new();

    public KeyValueStorageActor()
    {
        Receive<CompareAndSwapCommand>(command => HandleCompareAndSwap(command));
        Receive<StrongGetQuery>(query => HandleStrongGet(query));
        Receive<EventualGetQuery>(query => HandleEventualGet(query));
    }

    private void HandleCompareAndSwap(CompareAndSwapCommand command)
    {
        if (!Data.ContainsKey(command.key))
        {
            Data.Add(command.key, "");
        }
        if (Data.TryGetValue(command.key, out var value) && value == command.unmodified)
        {
            Data[command.key] = command.modified;
            Sender.Tell(new CompareAndSwapResponse(command.requestId, command.key, command.modified));
        }
        else
        {
            Sender.Tell(new CompareAndSwapResponse(command.requestId, command.key, ""));
        }
        Log.Info($"Stored value for key {command.key}: {Data[command.key]}");
    }

    private void HandleStrongGet(StrongGetQuery query)
    {
       Data.TryGetValue(query.key, out var value); 
       Sender.Tell(new StrongGetResponse(query.requestId, query.key, value ?? ""));
    }

    private void HandleEventualGet(EventualGetQuery query)
    {
       Data.TryGetValue(query.key, out var value); 
       Sender.Tell(new StrongGetResponse(query.requestId, query.key, value ?? ""));
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props()
    {
        return Akka.Actor.Props.Create(() => new KeyValueStorageActor());
    }
}

