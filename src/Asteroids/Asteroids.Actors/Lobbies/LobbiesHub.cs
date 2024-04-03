using System.Diagnostics;
using Akka.Actor;
using Akka.Hosting;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Asteroids.Shared.Lobbies;

public interface ILobbiesHub
{
    Task ViewAllLobbies(SessionScoped<ViewAllLobbiesQuery> query);
    Task CreateLobby(SessionScoped<CreateLobbyCommand> cmd);
    Task JoinLobby(Traceable<SessionScoped<JoinLobbyCommand>> cmd);

    Task NotifyViewAllLobbiesResponse(Returnable<ViewAllLobbiesResponse> response);
    Task NotifyCreateLobbyEvent(Returnable<CreateLobbyEvent> e);
    Task NotifyJoinLobbyEvent(Traceable<Returnable<JoinLobbyEvent>> e);
    Task NotifyInvalidSessionEvent(Returnable<InvalidSessionEvent> e);
}

public interface ILobbiesClient
{
    Task OnViewAllLobbiesResponse(ViewAllLobbiesResponse response);
    Task OnCreateLobbyEvent(CreateLobbyEvent e);
    Task OnJoinLobbyEvent(JoinLobbyEvent e);
    Task OnInvalidSessionEvent(InvalidSessionEvent e);
}

public class LobbiesHub : Hub<ILobbiesClient>, ILobbiesHub
{
    private readonly ILogger<LobbiesHub> logger;
    private readonly IActorRef lobbySupervisor;
    private readonly IActorRef userSessionSupervisor;

    private Dictionary<string, IActorRef> userSessionActors = new();

    public LobbiesHub(ILogger<LobbiesHub> logger, ActorRegistry actorRegistry)
    {
        this.logger = logger;
        lobbySupervisor = actorRegistry.Get<LobbySupervisor>();
        userSessionSupervisor = actorRegistry.Get<UserSessionSupervisor>();
    }

    public static string HubRelativeUrl => "hubs/lobbies";
    public static string HubUrl => $"http://asteroids-system:8080/{HubRelativeUrl}";

    public async Task ViewAllLobbies(SessionScoped<ViewAllLobbiesQuery> query)
    {
        await ForwardToUserSessionActor(query);
    }

    public async Task CreateLobby(SessionScoped<CreateLobbyCommand> cmd)
    {
        logger.LogInformation($"CreateLobby at hub for session: {cmd.SessionActorPath}");
        await ForwardToUserSessionActor(cmd);
    }

    public async Task JoinLobby(Traceable<SessionScoped<JoinLobbyCommand>> traceable)
    {
        await ExecuteTraceableAsync(traceable, async (sessionScoped, activity) =>
        {
            logger.LogInformation($"JoinLobby at hub for session: {sessionScoped.SessionActorPath}");
            await ForwardToUserSessionActor(sessionScoped.ToTraceable(activity));
        });
    }

    public async Task NotifyViewAllLobbiesResponse(Returnable<ViewAllLobbiesResponse> response)
    {
        logger.LogInformation($"NotifyViewAllLobbiesResponse: {response.Message.Lobbies.Count()}");
        await Clients.Client(response.ConnectionId).OnViewAllLobbiesResponse(response.Message);
    }

    public async Task NotifyCreateLobbyEvent(Returnable<CreateLobbyEvent> e)
    {
        await Clients.All.OnCreateLobbyEvent(e.Message);
    }

    public async Task NotifyJoinLobbyEvent(Traceable<Returnable<JoinLobbyEvent>> traceable)
    {
        logger.LogInformation($"NotifyJoinLobbyEvent at hub: {traceable.Message.Message.Id}");
        await ExecuteTraceableAsync(traceable, async (returnable, activity) =>
        {
            await Clients.Client(returnable.ConnectionId).OnJoinLobbyEvent(returnable.Message);
        });
    }

    public async Task NotifyInvalidSessionEvent(Returnable<InvalidSessionEvent> e)
    {
        await Clients.Client(e.ConnectionId).OnInvalidSessionEvent(new InvalidSessionEvent());
    }

    private async Task ExecuteTraceableAsync<T>(Traceable<T> traceable, Func<T, Activity?, Task> action)
    {
        using var activity = traceable.Activity($"{nameof(AccountServiceHub)}: {typeof(T).Name}");
        var command = traceable.Message;
        await action(command, activity);
    }

    private async Task ForwardToUserSessionActor<T>(SessionScoped<T> message)
    {
        if (userSessionActors.TryGetValue(message.SessionActorPath, out var userSessionRef))
        {
            userSessionRef.Tell(message);
        }
        else
        {
            var query = new FindUserSessionRefQuery(message.SessionActorPath).ToSessionableMessage(message.ConnectionId, message.SessionActorPath);
            var result = await userSessionSupervisor.Ask<FindUserSessionResult>(query);
            userSessionRef = result.UserSessionRef;

            if (userSessionRef is not null)
            {
                userSessionActors[message.SessionActorPath] = userSessionRef;
                userSessionRef.Tell(message);
            }
            else
            {
                await Clients.Caller.OnInvalidSessionEvent(new InvalidSessionEvent());
                logger.LogWarning($"User session not found for {message.SessionActorPath}");
            }
        }
    }

    private async Task ForwardToUserSessionActor<T>(Traceable<SessionScoped<T>> tmsg)
    {
        if (userSessionActors.TryGetValue(tmsg.Message.SessionActorPath, out var userSessionRef))
        {
            userSessionRef.Tell(tmsg);
        }
        else
        {
            var query = new FindUserSessionRefQuery(tmsg.Message.SessionActorPath).ToSessionableMessage(tmsg.Message.ConnectionId, tmsg.Message.SessionActorPath);
            var result = await userSessionSupervisor.Ask<FindUserSessionResult>(query);
            userSessionRef = result.UserSessionRef;

            if (userSessionRef is not null)
            {
                userSessionActors[tmsg.Message.SessionActorPath] = userSessionRef;
                userSessionRef.Tell(tmsg);
            }
            else
            {
                await Clients.Caller.OnInvalidSessionEvent(new InvalidSessionEvent());
                logger.LogWarning($"User session not found for {tmsg.Message.SessionActorPath}");
            }
        }
    }
}
