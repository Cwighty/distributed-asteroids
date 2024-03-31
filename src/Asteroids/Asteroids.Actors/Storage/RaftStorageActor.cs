using Akka.Actor;
using Akka.Event;
using System.Net.Http.Json;

namespace Asteroids.Shared.Storage;

public record CompareAndSwapCommand(Guid requestId, string key, string unmodified, string modified);
public record CompareAndSwapResponse(Guid requestId, string key, string value);
public record StrongGetQuery(Guid requestId, string key);
public record StrongGetResponse(Guid requestId, string key, string value);
public record EventualGetQuery(Guid requestId, string key);
public record EventualGetResponse(Guid requestId, string key, string value);

public record VersionedValue(long version, string value);

public class RaftStorageActor : ReceiveActor
{
    const string STORAGE_API = "https://storage-api:8080/gateway/Storage";
    private readonly HttpClient _httpClient;


    public RaftStorageActor()
    {
        _httpClient = new HttpClient()
        {
            BaseAddress = new Uri(STORAGE_API)
        };

        Receive<CompareAndSwapCommand>(command => HandleCompareAndSwap(command));
        Receive<StrongGetQuery>(query => HandleStrongGet(query));
        Receive<EventualGetQuery>(query => HandleEventualGet(query));
    }

    private void HandleCompareAndSwap(CompareAndSwapCommand command)
    {
        var payload = new { key = command.key, oldValue = command.unmodified, newValue = command.modified };

        Task<HttpResponseMessage> response = _httpClient.PostAsJsonAsync("CompareAndSwap", payload);
        response.ContinueWith(task =>
        {
            if (task.Result.IsSuccessStatusCode)
            {
                return new CompareAndSwapResponse(command.requestId, command.key, command.modified);
            }
            else
            {
                // Handle HTTP request failure
                Context.GetLogger().Error($"HTTP request failed with status code {task.Result.StatusCode}");
                return new CompareAndSwapResponse(command.requestId, command.key, "");
            }
        }).PipeTo(Sender);
    }

    private void HandleStrongGet(StrongGetQuery query)
    {
        Task<HttpResponseMessage> response = _httpClient.GetAsync($"StrongGet?key={query.key}");
        response.ContinueWith(task =>
        {
            if (task.Result.IsSuccessStatusCode)
            {
                var value = task.Result.Content.ReadFromJsonAsync<VersionedValue>().Result;
                return new StrongGetResponse(query.requestId, query.key, value!.value);
            }
            else
            {
                // Handle HTTP request failure
                Context.GetLogger().Error($"HTTP request failed with status code {task.Result.StatusCode}");
                return new StrongGetResponse(query.requestId, query.key, "");
            }
        }).PipeTo(Sender);
    }

    private void HandleEventualGet(EventualGetQuery query)
    {
        Task<HttpResponseMessage> response = _httpClient.GetAsync($"EventualGet?key={query.key}");
        response.ContinueWith(task =>
        {
            if (task.Result.IsSuccessStatusCode)
            {
                var value = task.Result.Content.ReadFromJsonAsync<VersionedValue>().Result;
                return new StrongGetResponse(query.requestId, query.key, value!.value);
            }
            else
            {
                // Handle HTTP request failure
                Context.GetLogger().Error($"HTTP request failed with status code {task.Result.StatusCode}");
                return new StrongGetResponse(query.requestId, query.key, "");
            }
        }).PipeTo(Sender);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props()
    {
        return Akka.Actor.Props.Create(() => new RaftStorageActor());
    }
}

