﻿using System.ComponentModel.DataAnnotations;
using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.GameStateEntities;

public record AsteroidState
{
    private readonly AsteroidParameters asteroidParams;

    public AsteroidState() { asteroidParams = new AsteroidParameters(); }
    public AsteroidState(AsteroidParameters asteroidParams)
    {
        this.asteroidParams = asteroidParams;
    }
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ImmunityTicks { get; set; } = 0;
    public required Location Location { get; set; } = new Location(0, 0);
    public required Heading Heading { get; set; } = new Heading(0);
    public required MomentumVector MomentumVector { get; set; } = new MomentumVector(0, 0);
    public double Size { get; set; } = 100;
    public int Rotation { get; set; } = 2;
    public bool IsAlive { get => Size >= asteroidParams.MinSize; }


    public List<AsteroidState> BreakInTwo(GameParameters gameParams)
    {
        var currentAsteroid = new AsteroidState(gameParams.AsteroidParameters)
        {
            ImmunityTicks = gameParams.AsteroidCollisionTimeout,
            Location = new Location(Location.X, Location.Y),
            Heading = new Heading(Heading.Angle),
            Size = Size / 2,
            Rotation = Rotation,
            MomentumVector = MomentumVector.Rotate(45).Scale(0.5),
        };

        var newAsteroid = new AsteroidState(gameParams.AsteroidParameters)
        {
            ImmunityTicks = gameParams.AsteroidCollisionTimeout,
            Location = new Location(Location.X, Location.Y),
            Heading = new Heading(Heading.Angle),
            Size = Size / 2,
            Rotation = -Rotation,
            MomentumVector = MomentumVector.Rotate(-45).Scale(0.5),
        };

        return new List<AsteroidState> { currentAsteroid, newAsteroid };
    }

    public Location MoveToNextPosition(GameParameters gameParams, double deltaTime = 1)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // if asteroid hits edge of screen, delete it
        if (newX < 0 || newX > gameParams.GameWidth || newY < 0 || newY > gameParams.GameHeight)
        {
            Size = 0;
            return Location;
        }

        // Apply screen wrapping
        // newX = newX >= 0 ? newX % gameParams.GameWidth : gameParams.GameWidth + (newX % gameParams.GameWidth);
        // newY = newY >= 0 ? newY % gameParams.GameHeight : gameParams.GameHeight + newY % gameParams.GameHeight;

        Location = new Location((int)newX, (int)newY);
        return Location;
    }

    public Heading Rotate(double deltaTime = 1)
    {
        var newAngle = Heading.Angle + Rotation * deltaTime;

        if (newAngle >= 360 || newAngle < 0)
        {
            newAngle %= 360;
            Id = Guid.NewGuid();
        }

        if (newAngle < 0) newAngle += 360;

        Heading = new Heading((int)newAngle);
        return new Heading((int)newAngle);
    }

    public AsteroidState Collide()
    {
        return new AsteroidState(asteroidParams)
        {
            Id = Id,
            Location = Location,
            Heading = new Heading(Heading.Angle),
            Size = Size / 2,
            Rotation = -Rotation,
            MomentumVector = new MomentumVector(
              (int)(MomentumVector.X - new Random().NextDouble() * 10),
              (int)(MomentumVector.Y - new Random().NextDouble() * 10)),
        };
    }
}

public static class AsteroidExtensions
{
    public static AsteroidSnapshot ToSnapshot(this AsteroidState asteroid)
    {
        return new AsteroidSnapshot
        {
            Id = asteroid.Id,
            Location = asteroid.Location,
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

    public static bool CollidedWith(this AsteroidState asteroid, BulletState bullet, double bufferScale = 1)
    {
        var distance = Math.Pow(asteroid.Location.X - bullet.Location.X, 2) + Math.Pow(asteroid.Location.Y - bullet.Location.Y, 2);
        return distance < Math.Pow(asteroid.Size * bufferScale, 2);
    }
}
