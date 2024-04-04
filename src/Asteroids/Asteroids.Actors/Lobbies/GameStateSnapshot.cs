namespace Asteroids.Shared.Lobbies;

public enum LobbyState
{
    Joining,
    Countdown,
    Playing,
    Ended
}

public record Location(double X, double Y);

public record Heading
{
    public Heading() { }
    public Heading(double Angle)
    {
        Angle = Angle % 360;
    }
};

public record PlayerStateSnapshot
{
    public PlayerStateSnapshot() { }
    public PlayerStateSnapshot(string name)
    {
        Name = name;
    }

    public Location Location { get; init; } = new(0, 0);
    public Heading Heading { get; init; } = new(0);
    public string Name { get; init; }
    public bool IsAlive { get; init; } = true;
}

public record GameStateSnapshot
{
    public GameStateSnapshot() { }
    public GameStateSnapshot(LobbyInfo lobby)
    {
        Lobby = lobby;
    }

    public long Tick { get; init; } = -1;
    public long CountDown { get; init; } = 10;
    public LobbyInfo Lobby { get; init; }
    public LobbyState State { get; init; } = LobbyState.Joining;
    public IEnumerable<PlayerStateSnapshot> Players { get; init; } = new List<PlayerStateSnapshot>();
}
