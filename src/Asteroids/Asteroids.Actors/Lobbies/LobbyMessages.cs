namespace Asteroids.Shared.Lobbies;


public record GameStateBroadcast(GameStateSnapshot State);

public record StartGameCommand(long LobbyId);
public record LobbyStateQuery(long LobbyId);
public record LobbyStateChangedEvent(GameStateSnapshot State);

