﻿using Asteroids.Shared.GameStateEntities;

namespace Asteroids.Shared.Lobbies;


public record GameStateBroadcast(GameStateSnapshot State);


public record StartGameCommand(long LobbyId);
public record LobbyStateQuery(long LobbyId);
public record LobbyStateChangedEvent(GameStateSnapshot State);

public class GameControlMessages
{
    public enum Key
    {
        Up,
        Down,
        Left,
        Right,
        Space,
        None
    }

    public record UpdateKeyStatesCommand(Dictionary<Key, bool> KeyStates);
}
