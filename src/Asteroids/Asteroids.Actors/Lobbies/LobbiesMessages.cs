﻿namespace Asteroids.Shared.Lobbies;

public record LobbyInfo(long Id, string Name, int PlayerCount);

public record ViewAllLobbiesQuery();
public record ViewAllLobbiesResponse(IEnumerable<LobbyInfo> Lobbies);

public record CreateLobbyCommand(string Name);
public record CreateLobbyEvent(IEnumerable<LobbyInfo> Lobbies);

public record JoinLobbyCommand(long Id, string UserActorPath);
public record JoinLobbyEvent(GameStateSnapshot State, string? ErrorMessage = null);

public record InvalidSessionEvent();