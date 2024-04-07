namespace Asteroids.Shared.GameStateEntities;

public record GameParameters
{
    public int CountdownSeconds { get; init; } = 3;
    public double DeltaTime { get; init; } = 1;
    public int GameWidth { get; init; } = 1000;
    public int GameHeight { get; init; } = 1000;

    public int MaxPlayers { get; init; } = 10;
    public PlayerParameters PlayerParameters { get; init; } = new();

    public int MaxAsteroids { get; init; } = 10;
    public int MaxAsteroidSize { get; init; } = 200;
    public double AsteroidSpawnRate { get; init; } = 0.2;
    public double AsteroidDamageScale { get; init; } = .3;
    public int AsteroidCollisionTimeout { get; init; } = 400;
    public AsteroidParameters AsteroidParameters { get; init; } = new();

    public long BulletCooldownTicks { get; init; } = 1000;
    public int BulletSpeed { get; init; } = 50;
    public int MaxBullets { get; init; } = 100;

    public static GameParameters Default => new();

}

public class PlayerParameters
{
    public int MaxMomentum { get; set; } = 100;
    public int Acceleration { get; set; } = 1;
    public int TurnSpeed { get; set; } = 10;
}

public class AsteroidParameters
{
    public double MaxMomentum { get; set; } = 100;
    public double MaxRotation { get; set; } = 10;
    public double MinSize { get; set; } = 40;
}