using FluentAssertions;

namespace Asteroids.Tests.GameMechanics;

public class MovementTests
{
    public MovementParameters TestMovementParams { get; }

    public MovementTests()
    {
        TestMovementParams = new MovementParameters()
        {
            MaxWidth = 1000,
            MaxHeight = 1000,
            MaxMomentum = 50,
            Acceleration = 1,
            TurnSpeed = 10,
        };
    }
    // PlayerState can calculate a new position based on its current momentum vector and a given deltaTime
    [Fact]
    public void test_calculate_new_position()
    {
        // Arrange
        var playerState = new PlayerState(TestMovementParams);
        playerState.Location = new Location(0, 0);
        playerState.MomentumVector = new MomentumVector(10, 10);
        var deltaTime = 1;

        // Act
        var newPosition = playerState.CalculateNewPosition(deltaTime);

        // Assert
        newPosition.X.Should().Be(10);
        newPosition.Y.Should().Be(10);
    }

    // PlayerState can calculate a new heading based on its current heading and a given deltaTime and turning direction
    [Fact]
    public void player_state_calculate_new_heading()
    {
        var playerState = new PlayerState(TestMovementParams);
        playerState.Heading = new Heading(90);

        var turningRight = playerState.CalculateNewHeading(true, 1);
        turningRight.Angle.Should().Be(100);

        var turningLeft = playerState.CalculateNewHeading(false, 1);
        turningLeft.Angle.Should().Be(80);
    }

    // PlayerState can apply thrust to its momentum vector based on its current heading and a given deltaTime
    [Fact]
    public void player_state_apply_thrust_at_angle()
    {
        // Arrange
        var playerState = new PlayerState(TestMovementParams);
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
        var playerState = new PlayerState(TestMovementParams);
        playerState.Location = new Location(1005, 1005);
        playerState.MomentumVector = new MomentumVector(10, 10);

        // Act
        var newPosition = playerState.CalculateNewPosition(1);

        // Assert
        newPosition.X.Should().BeInRange(0, 1000);
        newPosition.Y.Should().BeInRange(0, 1000);
        newPosition.X.Should().Be(15);
        newPosition.Y.Should().Be(15);
    }

}