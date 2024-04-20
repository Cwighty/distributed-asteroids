using Asteroids.Shared.GameStateEntities;
using FluentAssertions;

namespace Asteroids.Tests.GameMechanics;

public class PlayerStateTests : TestKit
{
    public GameParameters TestParameters { get; }

    public PlayerStateTests()
    {
        TestParameters = new GameParameters
        {
            GameWidth = 1000,
            GameHeight = 1000,
            DeltaTime = 1
        };

    }
    // PlayerState can calculate a new position based on its current momentum vector and a given deltaTime
    [Fact]
    public void test_calculate_new_position()
    {
        // Arrange
        var playerState = new PlayerState();
        playerState.Location = new Location(0, 0);
        playerState.MomentumVector = new MomentumVector(10, 10);

        // Act
        var newPosition = playerState.MoveToNextPosition(TestParameters);

        // Assert
        newPosition.X.Should().Be(10);
        newPosition.Y.Should().Be(10);
    }

    // PlayerState can calculate a new heading based on its current heading and a given deltaTime and turning direction
    [Fact]
    public void player_state_calculate_new_heading()
    {
        var playerState = new PlayerState();
        playerState.Heading = new Heading(90);

        var turningRight = playerState.RotateRight();
        turningRight.Angle.Should().Be(100);

        var turningLeft = playerState.RotateLeft();
        turningLeft.Angle.Should().Be(90);
    }

    // PlayerState can apply thrust to its momentum vector based on its current heading and a given deltaTime
    [Fact]
    public void player_state_apply_thrust_at_angle()
    {
        // Arrange
        var playerState = new PlayerState();
        playerState.Heading = new Heading(45);
        playerState.MomentumVector = new MomentumVector(1, 1);

        // Act
        playerState.ApplyThrust(1);

        // Assert
        playerState.MomentumVector.Should().BeEquivalentTo(new MomentumVector(1.7071067811865475, 1.7071067811865475));
    }

    // PlayerState can wrap its position around the screen when it goes off the edge
    [Fact]
    public void test_player_state_wraps_position_around_screen()
    {
        // Arrange
        var playerState = new PlayerState();
        playerState.Location = new Location(1005, 1005);
        playerState.MomentumVector = new MomentumVector(10, 10);

        // Act
        var newPosition = playerState.MoveToNextPosition(TestParameters);

        // Assert
        newPosition.X.Should().BeInRange(0, 1000);
        newPosition.Y.Should().BeInRange(0, 1000);
        newPosition.X.Should().Be(15);
        newPosition.Y.Should().Be(15);
    }

    //Player can be damaged
    [Fact]
    public void test_player_can_be_damaged()
    {
        var playerState = new PlayerState();
        playerState.Health = 100;
        playerState.Damage(10);
        playerState.Health.Should().Be(90);

    }

    // player cant be damaged below 0
    [Fact]
    public void test_player_cant_be_damaged_below_zero()
    {
        var playerState = new PlayerState();
        playerState.Health = 0;
        playerState.Damage(10);
        playerState.Health.Should().Be(-1);
    }

    // player considers it dead when health is 0
    [Fact]
    public void test_player_considers_it_dead_when_health_is_zero()
    {
        var playerState = new PlayerState();
        playerState.Health = 0;
        playerState.IsAlive.Should().BeFalse();
    }

    // player can be alive when health is greater than 0
    [Fact]
    public void test_player_can_be_alive_when_health_is_greater_than_zero()
    {
        var playerState = new PlayerState();
        playerState.Health = 10;
        playerState.IsAlive.Should().BeTrue();
    }

    // player to string is session actor path
    [Fact]
    public void test_player_to_string_is_session_actor_path()
    {
        var actor = CreateTestProbe();
        var playerState = new PlayerState()
        {
            UserSessionActorPath = actor.Ref.Path.ToString()
        };
        playerState.ToString().Should().Be(actor.Ref.Path.Name);
    }

}