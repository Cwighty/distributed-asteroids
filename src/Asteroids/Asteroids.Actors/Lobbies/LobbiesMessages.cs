using Asteroids.Shared.GameStateEntities;

namespace Asteroids.Shared.Lobbies;

public record LobbyInfo(long Id, string Name, int PlayerCount, GameStatus Status);

public record ViewAllLobbiesQuery();
public record ViewAllLobbiesResponse(List<LobbyInfo> Lobbies);

public record CreateLobbyCommand(string Name);
public record CreateLobbyEvent(List<LobbyInfo> Lobbies);

public record JoinLobbyCommand(long Id, string UserActorPath);
public record JoinLobbyEvent(GameStateSnapshot State, string? ErrorMessage = null);

public record InvalidSessionEvent();
