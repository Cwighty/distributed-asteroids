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

    public required Location Location { get; set; }
    public required Heading Heading { get; set; }
    public required MomentumVector MomentumVector { get; set; }
    public required string OwnerActorPath { get; set; }

    public Location MoveToNextPosition(GameParameters gameParameters, double deltaTime = 1)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // Apply screen wrapping
        newX = newX >= 0 ? newX % gameParameters.GameWidth : gameParameters.GameWidth + newX % gameParameters.GameWidth;
        newY = newY >= 0 ? newY % gameParameters.GameHeight : gameParameters.GameHeight + newY % gameParameters.GameHeight;

        Location = new Location(newX, newY);
        return Location;
    }
}