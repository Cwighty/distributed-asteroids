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

    public IActorRef UserSessionActor { get; set; }
    public string Username { get; set; }
    public int Health { get; set; }
    public int Score { get; set; }
    public bool IsAlive { get => Health > 0; }

    public Dictionary<GameControlMessages.Key, bool> KeyStates { get; set; } = new Dictionary<GameControlMessages.Key, bool>() {
        { GameControlMessages.Key.Up, false },
        { GameControlMessages.Key.Down, false },
        { GameControlMessages.Key.Left, false },
        { GameControlMessages.Key.Right, false }
        };

    public Location Location { get; set; } = new Location(0, 0);
    public Heading Heading { get; set; } = new Heading(0);

    public MomentumVector MomentumVector { get; set; } = new MomentumVector(0, 0);
    public PlayerParameters PlayerParameters { get; }

    public Location MoveToNextPosition(GameParameters gameParams, double deltaTime = 1)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // Apply screen wrapping
        newX = newX >= 0 ? newX % gameParams.GameWidth : gameParams.GameWidth + newX % gameParams.GameWidth;
        newY = newY >= 0 ? newY % gameParams.GameHeight : gameParams.GameHeight + newY % gameParams.GameHeight;

        Location = new Location(newX, newY);
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
        double newAngle = (Heading.Angle + turnAdjustment) % 360;

        if (newAngle < 0) newAngle += 360;

        Heading = new Heading(newAngle);
        return new Heading(newAngle);
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
        Health = Math.Max(Health - damage, 0);
    }

    public override string ToString()
    {
        return UserSessionActor.Path.Name;
    }

    public BulletState Shoot(GameParameters gameParameters)
    {
        return new BulletState
        {
            OwnerActorPath = UserSessionActor.Path.Name,
            Location = Location,
            Heading = new Heading(Heading.Angle),
            MomentumVector = MomentumVector.Normalize().Scale(gameParameters.BulletSpeed),
        };
    }
}
public static class PlayerStateExtensions
{
    public static PlayerStateSnapshot ToSnapshot(this PlayerState state)
    {
        return new PlayerStateSnapshot() with
        {
            Heading = state.Heading,
            Health = state.Health,
            Location = state.Location,
            Name = state.Username,
            IsAlive = state.IsAlive
        };
    }
}
