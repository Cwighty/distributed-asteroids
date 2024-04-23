using System.ComponentModel.DataAnnotations;
using Akka.Actor;
using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.GameStateEntities;

public class PlayerState
{
    public PlayerState()
    {
        PlayerParameters = new PlayerParameters();
    }
    public PlayerState(PlayerParameters movementParameters)
    {
        PlayerParameters = movementParameters;
    }

    public string UserSessionActorPath { get; set; }
    public string Username { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Health { get; set; }
    public int Score { get; set; }
    public bool IsAlive { get => Health > 0; }

    public Dictionary<GameControlMessages.Key, bool> KeyStates { get; set; } = new Dictionary<GameControlMessages.Key, bool>() {
        { GameControlMessages.Key.Up, false },
        { GameControlMessages.Key.Down, false },
        { GameControlMessages.Key.Left, false },
        { GameControlMessages.Key.Right, false },
        { GameControlMessages.Key.Space, false },
        };

    public Location Location { get; set; } = new Location(0, 0);
    public Heading Heading { get; set; } = new Heading(0);

    public MomentumVector MomentumVector { get; set; } = new MomentumVector(0, 0);
    public PlayerParameters PlayerParameters { get; }

    public long LastShotTick { get; set; }

    public Location MoveToNextPosition(GameParameters gameParams, double deltaTime = 1)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        if (newX < 0 || newX > gameParams.GameWidth || newY < 0 || newY > gameParams.GameHeight)
        {
            Id = Guid.NewGuid();
        }

        // Apply screen wrapping
        newX = newX >= 0 ? newX % gameParams.GameWidth : gameParams.GameWidth + newX % gameParams.GameWidth;
        newY = newY >= 0 ? newY % gameParams.GameHeight : gameParams.GameHeight + newY % gameParams.GameHeight;

        Location = new Location((int)newX, (int)newY);
        return Location;
    }

    public Heading RotateRight(double deltaTime = 1)
    {
        return RotateToNewHeading(true, deltaTime);
    }

    public Heading RotateLeft(double deltaTime = 1)
    {
        return RotateToNewHeading(false, deltaTime);
    }

    private Heading RotateToNewHeading(bool isTurningRight, double deltaTime = 1)
    {
        double turnAdjustment = PlayerParameters.TurnSpeed * deltaTime * (isTurningRight ? 1 : -1);
        double newAngle = (Heading.Angle + turnAdjustment);

        if (newAngle >= 360 || newAngle < 0)
        {
            newAngle %= 360;
            Id = Guid.NewGuid();
        }

        if (newAngle < 0) newAngle += 360;

        Heading = new Heading((int)newAngle);
        return new Heading((int)newAngle);
    }

    public MomentumVector ApplyThrust(double deltaTime = 1)
    {
        double angleInRadians = Heading.Angle * (Math.PI / 180);
        var accelerationX = Math.Cos(angleInRadians) * PlayerParameters.Acceleration * deltaTime;
        var accelerationY = Math.Sin(angleInRadians) * PlayerParameters.Acceleration * deltaTime;

        // Update momentum vector based on the direction of thrust
        var momentumX = MomentumVector.X + accelerationX;
        var momentumY = MomentumVector.Y + accelerationY;

        // Clamp the maximum speed to prevent the ship from going too fast
        var totalMomentum = Math.Sqrt(momentumX * momentumX + momentumY * momentumY);
        if (totalMomentum > PlayerParameters.MaxMomentum)
        {
            momentumX = momentumX / totalMomentum * PlayerParameters.MaxMomentum;
            momentumY = momentumY / totalMomentum * PlayerParameters.MaxMomentum;
        }

        MomentumVector = new MomentumVector(momentumX, momentumY);
        return MomentumVector;
    }

    public void Damage(int damage)
    {
        Health = Math.Max((Health - damage), -1);
    }

    public override string ToString()
    {
        return UserSessionActorPath.Split('/').Last();
    }

    public BulletState Shoot(GameParameters gameParameters)
    {
        return new BulletState
        {
            OwnerActorPath = UserSessionActorPath.Split('/').Last(),
            Location = Location,
            Heading = new Heading(Heading.Angle),
            // vector based on heading and bullet speed
            MomentumVector = new MomentumVector(
                (int)(Math.Cos(Heading.Angle * Math.PI / 180) * gameParameters.BulletSpeed),
                (int)(Math.Sin(Heading.Angle * Math.PI / 180) * gameParameters.BulletSpeed)
            )
        };
    }
}
public static class PlayerStateExtensions
{
    public static PlayerStateSnapshot ToSnapshot(this PlayerState state)
    {
        return new PlayerStateSnapshot() with
        {
            Key = state.Id,
            Heading = state.Heading,
            Health = state.Health,
            Location = state.Location,
            Name = state.Username,
            IsAlive = state.IsAlive
        };
    }
}
