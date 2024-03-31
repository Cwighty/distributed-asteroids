using System.Net.Http.Json;
namespace Asteroids.Shared.Storage;

public class VersionedValue<T>
{
    public long Version { get; set; }
    public T Value { get; set; } = default!;
}

public class CompareAndSwapRequest
{
    public string Key { get; set; } = null!;
    public string? Unmodified { get; set; }
    public string Modified { get; set; } = null!;
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

    public StorageService(HttpClient client)
    {
        this.client = client;
    }
    public async Task<VersionedValue<string>> StrongGet(string key)
    {
        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/StrongGet?key={key}");
        if (String.IsNullOrEmpty(response!.Value))
            return new VersionedValue<string> { Value = "", Version = 0 };
        return response;
    }

    public async Task<VersionedValue<string>> EventualGet(string key)
    {
        var response = await client.GetFromJsonAsync<VersionedValue<string>>($"gateway/Storage/EventualGet?key={key}");
        if (String.IsNullOrEmpty(response!.Value))
            return new VersionedValue<string> { Value = "", Version = 0 };
        return response;
    }

    public async Task CompareAndSwap(string key, string oldValue, string newValue)
    {
        var request = new CompareAndSwapRequest
        {
            Key = key,
            Unmodified = oldValue,
            Modified = newValue
        };
        var response = await client.PostAsJsonAsync($"gateway/Storage/CompareAndSwap", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReduceValue(string key, string oldValue, Func<string, string> reducer)
    {
        var newValue = reducer(oldValue);
        await CompareAndSwap(key, oldValue, newValue);
    }

    public async Task IdempodentReduceUntilSuccess(string key, string oldValue, Func<string, string> reducer, int retryCount = 5, int delay = 1000)
    {
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
            return Task.FromResult(new VersionedValue<string> { Value = data[key], Version = version });
        }
        return Task.FromResult(new VersionedValue<string> { Value = "", Version = 0 });
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
            return Task.FromResult(new VersionedValue<string> { Value = data[key], Version = version });
        }
        return Task.FromResult(new VersionedValue<string> { Value = "", Version = 0 });
    }
}
