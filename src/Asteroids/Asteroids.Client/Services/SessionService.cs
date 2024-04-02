using Akka.Actor;
using Blazored.LocalStorage;
using System.Text.Json;

namespace Asteroids.Client.Services;

public class SessionService
{
    private readonly ILocalStorageService localStorageService;

    const string SessionKey = "session";

    public SessionService(ILocalStorageService localStorageService)
    {
        this.localStorageService = localStorageService;
    }

    public async Task<bool> SessionExists()
    {
        var session = await GetSession();
        if (session == null)
        {
            return false;
        }
        return true;
    }

    public async Task<string?> GetSession()
    {
        var session = await localStorageService.GetItemAsStringAsync(SessionKey);
        if (!string.IsNullOrEmpty(session))
        {
            return session;
        }
        return null;
    }

    public async Task StoreSession(string userSessionActorPath)
    {
        await localStorageService.SetItemAsStringAsync(SessionKey, userSessionActorPath);
    }
}
