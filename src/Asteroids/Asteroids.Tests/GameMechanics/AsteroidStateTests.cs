using Asteroids.Shared.GameStateEntities;
using FluentAssertions;

namespace Asteroids.Tests.GameMechanics;

public class AsteroidStateTests
{
    public AsteroidStateTests()
    {

        TestParameters = new GameParameters
        {
            GameWidth = 1000,
            GameHeight = 1000,
            DeltaTime = 1
        };
    }

    public GameParameters TestParameters { get; private set; }

    // Asteroid can move to the next position
    [Fact]
    public void asteroid_state_can_move_to_next_position()
    {
        // Arrange
        var asteroidState = CreateAsteroidState();
        asteroidState.MomentumVector = new MomentumVector(10, 10);

        // Act
        asteroidState.MoveToNextPosition(TestParameters);

        // Assert
        asteroidState.Location.X.Should().Be(10);
        asteroidState.Location.Y.Should().Be(10);
    }


    // Asteroid can calculate a new rotation
    [Fact]
    public void asteroid_state_can_calculate_new_rotation()
    {
        // Arrange
        var asteroidState = CreateAsteroidState();

        // Act
        asteroidState.Rotate();

        // Assert
        asteroidState.Heading.Angle.Should().Be(2);
    }

    // AsteroidState can collide with another asteroid
    [Fact]
    public void asteroid_state_can_collide_with_another_asteroid()
    {
        // Arrange
        var asteroidState1 = new AsteroidState()
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 100
        };

        var asteroidState2 = new AsteroidState()
        {
            Id = 2,
            Location = new Location(50, 50),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 100
        };

        // Act
        var collided = asteroidState1.CollidedWith(asteroidState2);

        // Assert
        collided.Should().BeTrue();
    }

    // Asteroid does not collide with other asteroids when they are too far apart
    [Fact]
    public void asteroid_does_not_collide_with_other_asteroids_when_too_far_apart()
    {
        // Arrange
        var asteroidState1 = new AsteroidState()
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 100
        };

        var asteroidState2 = new AsteroidState()
        {
            Id = 2,
            Location = new Location(101, 101),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 100
        };

        // Act
        var collided = asteroidState1.CollidedWith(asteroidState2);

        // Assert
        collided.Should().BeFalse();
    }

    // AsteroidState can collide with a player
    [Fact]
    public void asteroid_state_can_collide_with_player()
    {
        // Arrange
        var asteroidState = new AsteroidState()
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            Size = 100,
            Rotation = 0,
            MomentumVector = new MomentumVector(0, 0)
        };

        var playerState = new PlayerState()
        {
            Location = new Location(5, 5)
        };

        // Act
        bool collided = asteroidState.CollidedWith(playerState);

        // Assert
        collided.Should().BeTrue();
    }

    // Asteroid does not collide with player when they are too far apart 
    [Fact]
    public void asteroid_does_not_collide_with_player_when_too_far_apart()
    {
        // Arrange
        var asteroidState = new AsteroidState()
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            Size = 100,
            Rotation = 0,
            MomentumVector = new MomentumVector(0, 0)
        };

        var playerState = new PlayerState()
        {
            Location = new Location(101, 101)
        };

        // Act
        bool collided = asteroidState.CollidedWith(playerState);

        // Assert
        collided.Should().BeFalse();
    }

    // Asteroid considered alive if its size is greater than the minimum size
    [Fact]
    public void asteroid_state_can_be_considered_alive()
    {
        // Arrange
        var asteroidParams = new AsteroidParameters() { MinSize = 10 };
        var asteroidState = new AsteroidState(asteroidParams)
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 50,
        };

        // Act
        var isAlive = asteroidState.IsAlive;

        // Assert
        Assert.True(isAlive);
    }

    // Asteroid considered dead if its size is less than the minimum size
    [Fact]
    public void asteroid_state_can_be_considered_dead()
    {
        // Arrange
        var asteroidParams = new AsteroidParameters() { MinSize = 10 };
        var asteroidState = new AsteroidState(asteroidParams)
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 5,
        };

        // Act
        var isAlive = asteroidState.IsAlive;

        // Assert
        Assert.False(isAlive);
    }

    // Asteroid collision randomizes momentum and halves asteroid
    [Fact]
    public void asteroid_collision_randomizes_momentum_and_shrinks_asteroid()
    {
        // Arrange
        var asteroidParams = new AsteroidParameters() { MinSize = 10 };
        var movementVector = new MomentumVector(10, 10);
        var asteroidState = new AsteroidState(asteroidParams)
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            MomentumVector = movementVector,
            Size = 50,
        };

        // Act
        var newAsteroidState = asteroidState.Collide();

        // Assert
        newAsteroidState.MomentumVector.Should().NotBe(movementVector);
        newAsteroidState.Size.Should().Be(25);
    }


    private AsteroidState CreateAsteroidState()
    {
        return new AsteroidState
        {
            Id = 1,
            Location = new Location(0, 0),
            Heading = new Heading(0),
            MomentumVector = new MomentumVector(0, 0),
            Size = 100
        };
    }

}