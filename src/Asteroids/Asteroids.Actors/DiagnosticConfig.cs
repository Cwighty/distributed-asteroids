using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Asteroids.Shared;

public static class DiagnosticConfig
{
    public const string SourceName = "asteroids";
    public readonly static ActivitySource Source = new(SourceName);

    public static Meter Meter = new Meter(SourceName);
    public static Counter<int> GameTickCounter = Meter.CreateCounter<int>("asteroids.game_ticks", null, "Number of game ticks");
    public static Counter<int> AccountsCreatedCounter = Meter.CreateCounter<int>("asteroids.accounts_created", null, "Number of accounts created");
    public static Counter<int> LobbiesCreatedCounter = Meter.CreateCounter<int>("asteroids.lobbies_created", null, "Number of lobbies created");
    public static Counter<int> LobbiesDestroyedCounter = Meter.CreateCounter<int>("asteroids.lobbies_destroyed", null, "Number of lobbies destroyed");

    public static Activity? Activity(this ITraceableMessage message, string activityName)
    {
        if (message.ParentTrace != null && message.ParentSpan != null)
        {
            ActivityContext parentContext = new ActivityContext(
              (ActivityTraceId)message.ParentTrace,
              (ActivitySpanId)message.ParentSpan,
              ActivityTraceFlags.Recorded
            );
            return Source?.StartActivity(activityName, ActivityKind.Internal, parentContext);
        }
        return Source?.StartActivity(activityName);
    }
}

public interface ITraceableMessage
{
    public ActivitySpanId? ParentSpan { get; init; }
    public ActivityTraceId? ParentTrace { get; init; }
}

public record Traceable<T> : ITraceableMessage
{
    public ActivitySpanId? ParentSpan { get; init; }
    public ActivityTraceId? ParentTrace { get; init; }
    public T Message { get; init; }
}

public static class ITracableMessageExtensions
{
    public static Traceable<T> ToTraceable<T>(this T message, Activity? activity)
    {
        return new Traceable<T>
        {
            ParentSpan = activity?.SpanId,
            ParentTrace = activity?.TraceId,
            Message = message
        };
    }
}
