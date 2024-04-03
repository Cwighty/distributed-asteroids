using Asteroids.Shared.Contracts;

namespace Asteroids.Shared.Lobbies;

public record LobbyInfo(long Id, string Name, int PlayerCount);

public record ViewAllLobbiesQuery();
public record ViewAllLobbiesResponse(IEnumerable<LobbyInfo> Lobbies);

public record CreateLobbyCommand(string Name);
public record CreateLobbyEvent(IEnumerable<LobbyInfo> Lobbies);

public record JoinLobbyCommand(long Id);
public record JoinLobbyEvent(long Id, string Name);

