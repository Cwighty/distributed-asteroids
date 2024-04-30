namespace Asteroids.Shared.GameStateEntities;

public record GameParameters
{
    public GameParameters()
    {
    }

    public int CountdownSeconds { get; set; }
    public double DeltaTime { get; set; }
    public int GameWidth { get; init; }
    public int GameHeight { get; init; }

    public int MaxPlayers { get; set; }
    public PlayerParameters PlayerParameters { get; init; } = new();

    public int MaxAsteroids { get; set; }
    public int MaxAsteroidSize { get; set; }
    public double AsteroidSpawnRate { get; set; }
    public double AsteroidDamageScale { get; set; }
    public int AsteroidCollisionTimeout { get; set; }
    public AsteroidParameters AsteroidParameters { get; init; } = new();

    public long BulletCooldownTicks { get; set; }
    public int BulletSpeed { get; set; }
    public int MaxBullets { get; set; }

    public static GameParameters Default => new()
    {
        PlayerParameters = new PlayerParameters
        {
            MaxMomentum = 100,
            Acceleration = 1,
            TurnSpeed = 10
        },
        AsteroidParameters = new AsteroidParameters
        {
            MaxMomentum = 10,
            MaxRotation = 10,
            MinSize = 40
        },
        CountdownSeconds = 3,
        DeltaTime = 1,
        GameWidth = 1200,
        GameHeight = 800,
        MaxPlayers = 10,
        MaxAsteroids = 10,
        MaxAsteroidSize = 200,
        AsteroidSpawnRate = 0.2,
        AsteroidDamageScale = .3,
        AsteroidCollisionTimeout = 400,
        BulletCooldownTicks = 1000,
        BulletSpeed = 50,
        MaxBullets = 100,
        CollisionBuffer = .5
    };

    public double CollisionBuffer { get; set; }
}

public class PlayerParameters
{
    public int MaxMomentum { get; set; } = 100;
    public int Acceleration { get; set; } = 1;
    public int TurnSpeed { get; set; } = 10;
}

public class AsteroidParameters
{
    public double MaxMomentum { get; set; }
    public double MaxRotation { get; set; }
    public double MinSize { get; set; }
}