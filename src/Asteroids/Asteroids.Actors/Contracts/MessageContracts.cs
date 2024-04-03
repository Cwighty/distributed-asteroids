namespace Asteroids.Shared.Contracts;

internal interface IReturnableMessage
{
    public string ConnectionId { init; }
}

public interface IReturnable
{
}

public record Returnable<T> : IReturnable
{
    public string ConnectionId { get; init; }
    public T Message { get; init; }
}

public static class IReturnableExtensions
{
    public static Returnable<T> ToReturnableMessage<T>(this T message, string connectionId)
    {
        return new Returnable<T>
        {
            ConnectionId = connectionId,
            Message = message
        };
    }

}

public interface ISessionScoped
{
    public string ConnectionId { get; init; }
    public string SessionActorPath { get; init; }
}

public record SessionScoped<T> : ISessionScoped
{
    public string ConnectionId { get; init; }
    public string SessionActorPath { get; init; }
    public T Message { get; init; }
}

public static class ISessionableExtensions
{
    public static SessionScoped<T> ToSessionableMessage<T>(this T message, string connectionId, string sessionActorPath)
    {
        return new SessionScoped<T>
        {
            ConnectionId = connectionId,
            SessionActorPath = sessionActorPath,
            Message = message
        };
    }
}

