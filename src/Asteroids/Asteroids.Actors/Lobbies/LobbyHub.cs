﻿using System.Diagnostics;
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
    Task NotifyInvalidSessionEvent(Returnable<InvalidSessionEvent> e);
}

public interface ILobbyClient
{
    Task OnInvalidSessionEvent(InvalidSessionEvent e);
}

public class LobbyHub : Hub<ILobbyClient>, ILobbyHub
{
    private readonly ILogger<LobbiesHub> logger;
    private readonly IActorRef lobbySupervisor;
    private readonly IActorRef userSessionSupervisor;

    private Dictionary<string, IActorRef> userSessionActors = new();

    public LobbyHub(ILogger<LobbiesHub> logger, ActorRegistry actorRegistry)
    {
        this.logger = logger;
        lobbySupervisor = actorRegistry.Get<LobbySupervisor>();
        userSessionSupervisor = actorRegistry.Get<UserSessionSupervisor>();
    }

    public static string HubRelativeUrl => "hubs/lobby";
    public static string HubUrl => $"http://asteroids-system:8080/{HubRelativeUrl}";

    public Task NotifyInvalidSessionEvent(Returnable<InvalidSessionEvent> e)
    {
        throw new NotImplementedException();
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
