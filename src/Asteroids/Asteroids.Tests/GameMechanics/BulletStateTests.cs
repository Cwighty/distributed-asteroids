
using Asteroids.Shared.GameStateEntities;
using FluentAssertions;

namespace Asteroids.Tests.GameMechanics;

public class BulletStateTests : TestKit
{
    public GameParameters TestParameters { get; }

    public BulletStateTests()
    {
        TestParameters = new GameParameters
        {
            GameWidth = 1000,
            GameHeight = 1000,
            DeltaTime = 1,
            BulletSpeed = 10
        };
    }

    // bullet is shot from player at its current location and direction
    [Fact]
    public void test_bullet_state_can_calculate_new_position()
    {
        var playerState = new PlayerState()
        {
            UserSessionActor = CreateTestProbe().Ref,
            Location = new Location(0, 0),
            MomentumVector = new MomentumVector(10, 10),
            Heading = new Heading(90),
        };
        var bulletState = playerState.Shoot(TestParameters);

        bulletState.Location.X.Should().Be(0);
        bulletState.Location.Y.Should().Be(0);
        bulletState.MomentumVector.Normalize().X.Should().BeApproximately(playerState.MomentumVector.Normalize().X, 0.01);
        bulletState.MomentumVector.Normalize().Y.Should().BeApproximately(playerState.MomentumVector.Normalize().Y, 0.01);
        bulletState.Heading.Angle.Should().Be(90);
    }

    // bullet moves to next position
    [Fact]
    public void test_bullet_state_can_move_to_next_position()
    {
        var playerState = new PlayerState()
        {
            UserSessionActor = CreateTestProbe().Ref,
            Location = new Location(0, 0),
            MomentumVector = new MomentumVector(10, 10),
            Heading = new Heading(90),
        };
        var bulletState = playerState.Shoot(TestParameters);

        bulletState.MoveToNextPosition(TestParameters);

        bulletState.Location.X.Should().BeApproximately(7, 0.1);
        bulletState.Location.Y.Should().BeApproximately(7, 0.1);
    }
}