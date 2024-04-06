using Asteroids.Shared.GameStateEntities;
using FluentAssertions;

namespace Asteroids.Tests.GameMechanics;

public class GameStateTests : TestKit
{

    public GameStateTests()
    {
        TestParameters = new GameParameters
        {
            GameWidth = 1000,
            GameHeight = 1000,
            DeltaTime = 1,
            CountdownSeconds = 3
        };
    }

    public GameParameters TestParameters { get; private set; }
    // GameState can join a player
    [Fact]
    public void test_game_state_can_join_player()
    {
        // Arrange
        var gameState = new GameState();
        var playerState = new PlayerState()
        {
            UserSessionActor = CreateTestProbe().Ref
        };

        // Act
        gameState.JoinPlayer(playerState);

        // Assert
        gameState.Players.Count.Should().Be(1);
        gameState.Players.ContainsKey(playerState.UserSessionActor.Path.Name).Should().BeTrue();
    }

    // GameState can get a player by name
    [Fact]
    public void test_game_state_get_player_by_name()
    {
        // Arrange
        var gameState = new GameState();
        var player1 = new PlayerState { UserSessionActor = CreateTestProbe().Ref };
        var player2 = new PlayerState { UserSessionActor = CreateTestProbe().Ref };
        gameState.JoinPlayer(player1);
        gameState.JoinPlayer(player2);

        // Act
        var result = gameState.GetPlayer(player2.UserSessionActor.Path.Name);

        // Assert
        result.Should().Be(player2);
    }

    // GameState throws an exception if game is not started before ticking
    [Fact]
    public void test_game_state_throws_exception_if_not_started_before_ticking()
    {
        // Arrange
        var gameState = new GameState();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => gameState.Tick());
    }

    // GameState can start a game
    [Fact]
    public void test_game_state_can_start_game()
    {
        // Arrange
        var gameState = new GameState();
        var player1 = new PlayerState { UserSessionActor = CreateTestProbe().Ref };
        gameState.JoinPlayer(player1);

        // Act
        gameState.StartGame();

        // Assert
        gameState.Status.Should().Be(GameStatus.Countdown);
    }

    // Game starts after Ticks for CountdownSeconds
    [Fact]
    public void test_game_starts_after_ticks_for_countdown_seconds()
    {
        // Arrange
        var gameState = new GameState();
        gameState.Status.Should().Be(GameStatus.Joining);
        var player1 = new PlayerState
        {
            UserSessionActor = CreateTestProbe().Ref,
            Health = 100,
        };
        gameState.JoinPlayer(player1);

        // Act
        gameState.StartGame();
        gameState.Status.Should().Be(GameStatus.Countdown);
        for (int i = 0; i < TestParameters.CountdownSeconds; i++)
        {
            gameState.Tick();
        }

        // Assert
        gameState.Status.Should().Be(GameStatus.Playing);
    }

    // GameState can randomly spawn asteroids
    [Fact]
    public void test_game_state_randomly_spawn_asteroids()
    {
        var gameParams = new GameParameters
        {
            AsteroidSpawnRate = 1, // 100% chance
            MaxAsteroids = 10,
            MaxAsteroidSize = 400,
            AsteroidParameters = new AsteroidParameters
            {
                MinSize = 0
            }
        };

        // Arrange
        var gameState = new GameState(gameParams)
        {
            Status = GameStatus.Playing
        };

        var initialAsteroidCount = gameState.Asteroids.Count;

        // Act
        gameState.Tick();

        // Assert
        gameState.Asteroids.Count.Should().BeGreaterThan(initialAsteroidCount);
    }

    // GameState can check for collisions
    [Fact]
    public void test_game_state_check_for_player_asteroid_collisions()
    {
        var gameParams = new GameParameters
        {
            AsteroidSpawnRate = 0,
            MaxAsteroids = 10,
            MaxAsteroidSize = 400,
            AsteroidParameters = new AsteroidParameters
            {
                MinSize = 0
            }
        };
        // Arrange
        var gameState = new GameState(gameParams)
        {
            Status = GameStatus.Playing
        };
        var player1 = new PlayerState()
        {
            Location = new Location(10, 10),
            Health = 100,
            UserSessionActor = CreateTestProbe().Ref,
            MomentumVector = new MomentumVector(0, 0),
            Heading = new Heading(0)
        };

        var asteroid1 = new AsteroidState()
        {
            Id = 1,
            MomentumVector = new MomentumVector(0, 0),
            Location = new Location(0, 0),
            Heading = new Heading(0),
            Size = 100
        };

        gameState.Players.Add("player1", player1);
        gameState.Asteroids.Add(asteroid1);

        // Act
        gameState.Tick();

        // Assert
        gameState.Asteroids.Count.Should().Be(1);
        player1.Health.Should().NotBe(100);
        gameState.Asteroids.Where(a => a.Id == asteroid1.Id).First().Size.Should().Be(50);
    }

    // GameState can check for collisions between asteroids
    [Fact]
    public void test_game_state_check_for_asteroid_asteroid_collisions()
    {
        var gameParams = new GameParameters
        {
            AsteroidSpawnRate = 0,
            MaxAsteroids = 10,
            MaxAsteroidSize = 400,
            AsteroidParameters = new AsteroidParameters
            {
                MinSize = 0
            }
        };
        // Arrange
        var gameState = new GameState(gameParams)
        {
            Status = GameStatus.Playing
        };
        var asteroid1 = new AsteroidState()
        {
            Id = 1,
            MomentumVector = new MomentumVector(0, 0),
            Location = new Location(0, 0),
            Heading = new Heading(0),
            Size = 100
        };
        var asteroid2 = new AsteroidState()
        {
            Id = 2,
            MomentumVector = new MomentumVector(0, 0),
            Location = new Location(10, 10),
            Heading = new Heading(0),
            Size = 100
        };
        gameState.Asteroids.Add(asteroid1);
        gameState.Asteroids.Add(asteroid2);

        // Act
        gameState.Tick();

        // Assert
        gameState.Asteroids.Count.Should().Be(2);
        gameState.Asteroids.Where(a => a.Id == asteroid1.Id).First().Size.Should().Be(50);
        gameState.Asteroids.Where(a => a.Id == asteroid2.Id).First().Size.Should().Be(50);
    }

    // GameState moves players every tick according to their momentum vector
    [Fact]
    public void test_game_state_moves_players_every_tick()
    {
        // Arrange
        var gameState = new GameState()
        {
            Status = GameStatus.Playing
        };

        var playerState = new PlayerState()
        {
            Location = new Location(0, 0),
            MomentumVector = new MomentumVector(10, 10),
            Heading = new Heading(0),
        };
        gameState.Players.Add("Player1", playerState);

        // Act
        gameState.Tick();

        // Assert
        playerState.Location.Should().Be(new Location(10, 10));
    }

    // GameState moves asteroids every tick according to their momentum vector
    [Fact]
    public void test_game_state_moves_asteroids_every_tick()
    {
        // Arrange
        var gameState = new GameState()
        {
            Status = GameStatus.Playing
        };
        var asteroidState = new AsteroidState()
        {
            Location = new Location(0, 0),
            MomentumVector = new MomentumVector(10, 10),
            Heading = new Heading(0),
            Size = 100
        };
        gameState.Asteroids.Add(asteroidState);

        // Act
        gameState.Tick();

        // Assert
        asteroidState.Location.Should().Be(new Location(10, 10));
    }

    // GameState throws an exception if game is started before joining players
    [Fact]
    public void game_state_throws_exception_if_game_started_before_joining_players()
    {
        // Arrange
        var gameState = new GameState();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => gameState.StartGame());
    }

    // GameState does not spawn asteroids if max asteroids is reached
    [Fact]
    public void game_state_does_not_spawn_asteroids_if_max_asteroids_is_reached()
    {
        // Arrange
        var gameParams = new GameParameters
        {
            AsteroidSpawnRate = 1, // 100% chance
            MaxAsteroids = 1,
        };
        var gameState = new GameState(gameParams)
        {
            Status = GameStatus.Playing
        };
        gameState.Asteroids = new List<AsteroidState>()
        {
            new AsteroidState()
            {
                Id = 1,
                MomentumVector = new MomentumVector(0, 0),
                Location = new Location(0, 0),
                Heading = new Heading(0),
                Size = 100
            }
        };

        // Act
        gameState.Tick();
        gameState.Tick();
        gameState.Tick();

        // Assert
        gameState.Asteroids.Count.Should().Be(1);
    }

    // GameState removes dead asteroids
    [Fact]
    public void test_game_state_removes_dead_asteroids()
    {
        // Arrange
        var gameParams = new GameParameters
        {
            AsteroidSpawnRate = 0,
            MaxAsteroids = 10,
            MaxAsteroidSize = 200,
        };

        var asteroidParams = new AsteroidParameters
        {
            MinSize = 100
        };

        var gameState = new GameState(gameParams)
        {
            Status = GameStatus.Playing,
            Lobby = new LobbyInfo(1, "", 0),
        };
        var playerState = new PlayerState();
        gameState.Players.Add("Player1", playerState);

        var asteroid1 = new AsteroidState(asteroidParams)
        {
            Id = 1,
            MomentumVector = new MomentumVector(0, 0),
            Location = new Location(0, 0),
            Heading = new Heading(0),
            Size = 50
        };
        var asteroid2 = new AsteroidState(asteroidParams)
        {
            Id = 2,
            MomentumVector = new MomentumVector(0, 0),
            Location = new Location(400, 400),
            Heading = new Heading(0),
            Size = 200
        };
        gameState.Asteroids = new List<AsteroidState> { asteroid1, asteroid2 };

        // Act
        gameState.Tick();

        // Assert
        gameState.ToSnapshot().Asteroids.Count().Should().Be(1);
    }

    // Game ends if all players are dead
    [Fact]
    public void test_game_ends_if_all_players_are_dead()
    {
        // Arrange
        var gameParams = new GameParameters
        {
            AsteroidSpawnRate = 0,
            MaxAsteroids = 10,
            MaxAsteroidSize = 200,
        };
        var gameState = new GameState(gameParams)
        {
            Status = GameStatus.Playing,
            Lobby = new LobbyInfo(1, "", 0),
        };
        var playerState = new PlayerState();
        gameState.Players.Add("Player1", playerState);

        playerState.Damage(100);

        // Act
        gameState.Tick();

        // Assert
        gameState.Status.Should().Be(GameStatus.GameOver);
    }
}