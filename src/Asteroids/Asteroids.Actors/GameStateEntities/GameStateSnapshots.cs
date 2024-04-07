using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.GameStateEntities;

public enum GameStatus
{
    Joining,
    Countdown,
    Playing,
    GameOver
}

public record Location(double X, double Y);

public record Heading(double Angle);

public record PlayerStateSnapshot
{
    public PlayerStateSnapshot() { }
    public PlayerStateSnapshot(string name)
    {
        Name = name;
    }

    public int Health { get; init; } = 100;
    public Location Location { get; init; } = new(0, 0);
    public Heading Heading { get; init; } = new(0);
    public string Name { get; init; } = string.Empty;
    public bool IsAlive { get; init; } = true;
}

public record AsteroidSnapshot
{
    public long Id { get; init; }
    public Location Location { get; init; } = new(0, 0);
    public Heading Heading { get; init; } = new(0);
    public double Size { get; init; }
    public bool IsAlive { get; init; }
}

public record BulletSnapshot
{
    public Location Location { get; init; } = new(0, 0);
    public Heading Heading { get; init; } = new(0);
}
