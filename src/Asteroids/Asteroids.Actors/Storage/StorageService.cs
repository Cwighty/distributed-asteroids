using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace Asteroids.Shared.Storage;

public record VersionedValue<T>(long Version, T Value);

public class CompareAndSwapRequest
{
    public string Key { get; set; } = null!;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = null!;
}

public interface IStorageService
{
    Task<VersionedValue<string>> StrongGet(string key);
    Task<VersionedValue<string>> EventualGet(string key);
    Task CompareAndSwap(string key, string oldValue, string newValue);
    Task ReduceValue(string key, string oldValue, Func<string, string> reducer);
    Task IdempodentReduceUntilSuccess(string key, string oldValue, Func<string, string> reducer, int retryCount = 5, int delay = 1000);
}

public class StorageService : IStorageService
{
    private HttpClient client;
    private readonly ILogger<StorageService> logger;

    public StorageService(HttpClient client, ILogger<StorageService> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    public async Task<VersionedValue<string>> StrongGet(string key)
    {
        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/StrongGet?key={key}");
        if (String.IsNullOrEmpty(response!.Value))
            return new VersionedValue<string>(0, "");
        return response;
    }

    public async Task<VersionedValue<string>> EventualGet(string key)
    {
        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/EventualGet?key={key}");
        if (String.IsNullOrEmpty(response!.Value))
            return new VersionedValue<string>(0, "");
        return response;
    }

    public async Task CompareAndSwap(string key, string oldValue, string newValue)
    {
        // logger.LogInformation("CompareAndSwap called with key: {0}, oldValue: {1}, newValue: {2}", key, oldValue, newValue);
        var request = new CompareAndSwapRequest
        {
            Key = key,
            OldValue = oldValue ?? "",
            NewValue = newValue
        };
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync($"gateway/Storage/CompareAndSwap", request);
            logger.LogInformation("CompareAndSwap response: {response}", response);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex.Message, "Failed to compare and swap");
            throw;
        }

    }

    public async Task ReduceValue(string key, string oldValue, Func<string, string> reducer)
    {
        var newValue = reducer(oldValue);
        await CompareAndSwap(key, oldValue, newValue);
    }

    public async Task IdempodentReduceUntilSuccess(string key, string oldValue, Func<string, string> reducer, int retryCount = 5, int delay = 1000)
    {
        // logger.LogInformation("IdempodentReduceUntilSuccess called with key: {key}, oldValue: {oldValue}, retryCount: {retryCount}, delay: {delay}", key, oldValue, retryCount, delay);
        int currentRetry = 0;
        while (currentRetry < retryCount)
        {
            try
            {
                await ReduceValue(key, oldValue, reducer);
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(delay);
                var refetchedValue = await StrongGet(key);
                oldValue = refetchedValue.Value;
                currentRetry++;
            }
        }
        throw new InvalidOperationException("Failed to reduce value, retry limit exceeded");
    }
}

public class InMemoryStorageService : IStorageService
{
    private Dictionary<string, string> data = new();
    private long version = 0;

    public InMemoryStorageService()
    {
    }

    public Task CompareAndSwap(string key, string oldValue, string newValue)
    {
        if (!data.ContainsKey(key))
        {
            data.Add(key, newValue);
            version++;
        }
        else if (data.ContainsKey(key) && data[key] == oldValue)
        {
            data[key] = newValue;
            version++;
        }
        else if (data.ContainsKey(key) && data[key] != oldValue)
        {
            throw new InvalidOperationException("Failed to compare and swap, value has changed since last read");
        }

        return Task.CompletedTask;
    }

    public Task<VersionedValue<string>> EventualGet(string key)
    {
        if (data.ContainsKey(key))
        {
            return Task.FromResult(new VersionedValue<string>(version, data[key]));
        }
        return Task.FromResult(new VersionedValue<string>(0, ""));
    }

    public Task IdempodentReduceUntilSuccess(string key, string oldValue, Func<string, string> reducer, int retryCount = 5, int delay = 1000)
    {
        int currentRetry = 0;
        while (currentRetry < retryCount)
        {
            try
            {
                ReduceValue(key, oldValue, reducer);
                return Task.CompletedTask;
            }
            catch (HttpRequestException)
            {
                Task.Delay(delay);
                var refetchedValue = StrongGet(key).Result;
                oldValue = refetchedValue.Value;
                currentRetry++;
            }
        }
        throw new InvalidOperationException("Failed to reduce value, retry limit exceeded");
    }

    public Task ReduceValue(string key, string oldValue, Func<string, string> reducer)
    {
        var newValue = reducer(oldValue);
        CompareAndSwap(key, oldValue, newValue);
        return Task.CompletedTask;
    }

    public Task<VersionedValue<string>> StrongGet(string key)
    {
        if (data.ContainsKey(key))
        {
            return Task.FromResult(new VersionedValue<string>(version, data[key]));
        }
        return Task.FromResult(new VersionedValue<string>(0, ""));
    }
}
