using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.GameStateEntities;

public record AsteroidState
{
    private readonly AsteroidParams asteroidParams;

    public AsteroidState() { asteroidParams = new AsteroidParams(); }
    public AsteroidState(AsteroidParams asteroidParams)
    {
        this.asteroidParams = asteroidParams;
    }
    public required Location Location { get; set; }
    public required Heading Heading { get; set; }
    public required MomentumVector MomentumVector { get; set; }
    public double Size { get; set; } = 100;
    public double Rotation { get; set; } = 2;
    public bool IsAlive { get => Size > asteroidParams.MinSize; }


    public List<AsteroidState> BreakInTwo()
    {
        var currentAsteroid = new AsteroidState
        {
            Location = CalculateNewPosition(3),
            Heading = new Heading(Heading.Angle),
            Size = Size / 2,
            Rotation = Rotation,
            MomentumVector = new MomentumVector(-MomentumVector.X / 2, -MomentumVector.Y / 2),
        };

        var newAsteroid = new AsteroidState
        {
            Location = CalculateNewPosition(3),
            Heading = new Heading(Heading.Angle),
            Size = Size / 2,
            Rotation = -Rotation,
            MomentumVector = new MomentumVector(-MomentumVector.X / 2, -MomentumVector.Y / 2),
        };

        return new List<AsteroidState> { currentAsteroid, newAsteroid };
    }

    public Location CalculateNewPosition(double deltaTime)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // Apply screen wrapping
        newX = newX >= 0 ? newX % asteroidParams.MaxWidth : asteroidParams.MaxWidth + newX % asteroidParams.MaxWidth;
        newY = newY >= 0 ? newY % asteroidParams.MaxHeight : asteroidParams.MaxHeight + newY % asteroidParams.MaxHeight;

        Location = new Location(newX, newY);
        return Location;
    }

    public Heading CalculateNewRotation(double deltaTime)
    {
        var newAngle = Heading.Angle + Rotation * deltaTime;
        if (newAngle < 0) newAngle += 360;

        return new Heading(newAngle);
    }

    internal AsteroidState Collide()
    {
        return new AsteroidState
        {
            Location = Location,
            Heading = new Heading(Heading.Angle),
            Size = Size / 2,
            Rotation = -Rotation,
            MomentumVector = new MomentumVector(
                MomentumVector.X - new Random().NextDouble() * 10,
                MomentumVector.Y - new Random().NextDouble() * 10),
        };
    }
}

public static class AsteroidExtensions
{
    public static AsteroidSnapshot ToSnapshot(this AsteroidState asteroid)
    {
        return new AsteroidSnapshot
        {
            Location = asteroid.CalculateNewPosition(1),
            Heading = asteroid.Heading,
            Size = asteroid.Size,
            IsAlive = asteroid.IsAlive,
        };
    }

    public static bool CollidedWith(this AsteroidState asteroid, AsteroidState otherAsteroid, double bufferScale = .5)
    {
        var distance = Math.Pow(asteroid.Location.X - otherAsteroid.Location.X, 2) + Math.Pow(asteroid.Location.Y - otherAsteroid.Location.Y, 2);
        return distance < Math.Pow(asteroid.Size * bufferScale + otherAsteroid.Size * bufferScale, 2);
    }
    
    public static bool CollidedWith(this AsteroidState asteroid, PlayerState player, double bufferScale = .5)
    {
        var distance = Math.Pow(asteroid.Location.X - player.Location.X, 2) + Math.Pow(asteroid.Location.Y - player.Location.Y, 2);
        return distance < Math.Pow(asteroid.Size * bufferScale, 2);
    }
}
