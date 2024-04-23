using Asteroids.Shared.Lobbies;

namespace Asteroids.Shared.GameStateEntities;

public class BulletState
{
    public BulletState()
    {
    }

    public BulletState(Location location, Heading heading)
    {
        Location = location;
        Heading = heading;
    }
    public Guid HtmlKey { get; set; } = Guid.NewGuid();
    public required Location Location { get; set; }
    public required Heading Heading { get; set; }
    public required MomentumVector MomentumVector { get; set; }
    public required string OwnerActorPath { get; set; }

    public Location MoveToNextPosition(GameParameters gameParameters, double deltaTime = 1)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        if (newX < 0 || newX > gameParameters.GameWidth || newY < 0 || newY > gameParameters.GameHeight)
        {
            HtmlKey = Guid.NewGuid();
        }
        // Apply screen wrapping
        newX = newX >= 0 ? newX % gameParameters.GameWidth : gameParameters.GameWidth + newX % gameParameters.GameWidth;
        newY = newY >= 0 ? newY % gameParameters.GameHeight : gameParameters.GameHeight + newY % gameParameters.GameHeight;

        Location = new Location((int)newX, (int)newY);
        return Location;
    }
}

public static class BulletStateExtensions
{
    public static BulletSnapshot ToSnapshot(this BulletState bulletState)
    {
        return new BulletSnapshot
        {
            Key = bulletState.HtmlKey,
            Location = bulletState.Location,
            Heading = bulletState.Heading
        };
    }
}
