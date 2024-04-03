using Akka.Actor;
using Akka.Hosting;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Asteroids.Shared.Lobbies;

public interface ILobbyHub
{
    Task ViewAllLobbies(SessionScoped<ViewAllLobbiesQuery> query);
    Task CreateLobby(SessionScoped<CreateLobbyCommand> cmd);
    Task JoinLobby(SessionScoped<JoinLobbyCommand> cmd);

    Task NotifyViewAllLobbiesResponse(Returnable<ViewAllLobbiesResponse> response);
    Task NotifyCreateLobbyEvent(Returnable<CreateLobbyEvent> e);
    Task NotifyJoinLobbyEvent(Returnable<JoinLobbyEvent> e);
}

public interface ILobbyClient
{
    Task OnViewAllLobbiesResponse(ViewAllLobbiesResponse response);
    Task OnCreateLobbyEvent(CreateLobbyEvent e);
    Task OnJoinLobbyEvent(JoinLobbyEvent e);
    Task OnInvalidSessionEvent(InvalidSessionEvent e);
}

public class LobbyHub : Hub<ILobbyClient>, ILobbyHub
{
    private readonly ILogger<LobbyHub> logger;
    private readonly IActorRef lobbySupervisor;
    private readonly IActorRef userSessionSupervisor;

    public LobbyHub(ILogger<LobbyHub> logger, ActorRegistry actorRegistry)
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

    public async Task JoinLobby(SessionScoped<JoinLobbyCommand> cmd)
    {
        await ForwardToUserSessionActor(cmd);
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

    public async Task NotifyJoinLobbyEvent(Returnable<JoinLobbyEvent> e)
    {
        await Clients.Client(e.ConnectionId).OnJoinLobbyEvent(e.Message);
    }
    private async Task ForwardToUserSessionActor<T>(SessionScoped<T> message)
    {
        var result = await userSessionSupervisor.Ask<FindUserSessionResult>(new FindUserSessionRefQuery(message.SessionActorPath));
        var userSessionRef = result.UserSessionRef;

        if (userSessionRef is not null)
        {
            userSessionRef.Tell(message);
        }
        else
        {
            await Clients.Caller.OnInvalidSessionEvent(new InvalidSessionEvent());
            logger.LogWarning($"User session not found for {message.SessionActorPath}");
        }
    }

}
