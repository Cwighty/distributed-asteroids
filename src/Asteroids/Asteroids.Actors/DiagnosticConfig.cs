using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Asteroids.Shared;

public static class DiagnosticConfig
{
    public const string SourceName = "asteroids-system";
    public readonly static ActivitySource Source = new(SourceName);
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
