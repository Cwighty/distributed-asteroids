using System.Diagnostics;
using Asteroids.Shared.GameStateEntities;
using FluentAssertions;
using static Asteroids.Shared.Lobbies.LobbyStateActor;

namespace Asteroids.Tests.Lobbies;

public class LobbyStateActorTests : TestKit
{
    // LobbyStateActor can handle JoinLobbyCommand and add a new player to the lobby
    [Fact]
    public void test_join_lobby_command()
    {
        // Arrange
        var lobbyId = Guid.NewGuid();
        var lobbyEmitter = CreateTestProbe();
        var supervisor = CreateTestProbe();
        var lobbyPersister = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", lobbyId, supervisor, lobbyEmitter, lobbyPersister));

        // Act
        var cmd = new JoinLobbyCommand(lobbyId, "user-session_user1");
        var trc = cmd.ToTraceable(null);
        lobbyStateActor.Tell(trc);

        // Assert
        ExpectMsg<Traceable<JoinLobbyEvent>>(trc =>
        {
            trc.Message.State.Lobby.Id.Should().Be(lobbyId);
            trc.Message.State.Lobby.Name.Should().Be("Test Lobby");
            trc.Message.State.Lobby.PlayerCount.Should().Be(1);
        });
    }

    // LobbyStateActor cannot start game in a non-Joining state
    [Fact]
    public void test_cannot_start_game_in_non_joining_state()
    {
        // Arrange
        var lobbyId = Guid.NewGuid();
        var lobbyEmitter = CreateTestProbe();
        var supervisor = CreateTestProbe();
        var lobbyPersister = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", lobbyId, supervisor, lobbyEmitter, lobbyPersister));
        var cmd = new StartGameCommand(lobbyId);
        var trc = cmd.ToTraceable(null);
        lobbyStateActor.Tell(trc);

        // Act
        lobbyStateActor.Tell(trc);

        // Assert
        ExpectNoMsg();
    }

    //LobbyStateActor starts broadcasting after 1 second after start game with countdown state
    [Fact]
    public void test_broadcast_state_after_1_second()
    {
        // Arrange
        var lobbyId = Guid.NewGuid();
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var supervisor = CreateTestProbe();
        var lobbyPersister = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", lobbyId, supervisor, lobbyEmitter, lobbyPersister));

        // Join
        var cmd = new JoinLobbyCommand(lobbyId, "user-session_user1");
        var trc = cmd.ToTraceable(null);
        lobbyStateActor.Tell(trc, userSessionActor);
        userSessionActor.ExpectMsg<Traceable<JoinLobbyEvent>>();

        var startCmd = new StartGameCommand(lobbyId);
        var startTrc = startCmd.ToTraceable(null);
        lobbyStateActor.Tell(startTrc, userSessionActor);
        userSessionActor.ExpectMsg<Traceable<LobbyStateChangedEvent>>();
        userSessionActor.ExpectMsg<Traceable<GameStateBroadcast>>(trc =>
        {
            trc.Message.State.Status.Should().Be(GameStatus.Countdown);
        }
        );
    }


    // Movement tests
    [Fact]
    public void ship_moves_after_a_tick()
    {
        var lobbyId = Guid.NewGuid();
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var supervisor = CreateTestProbe();
        var lobbyPersister = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", lobbyId, supervisor, lobbyEmitter, lobbyPersister, false));

        var player = new PlayerState
        {
            UserSessionActorPath = userSessionActor.Ref.Path.ToString(),
            Username = userSessionActor.Ref.Path.Name,
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };

        var players = new Dictionary<string, PlayerState> { { player.Username, player } };

        GameState game = new()
        {
            Status = GameStatus.Playing,
            Players = players,
            TickCount = 1,
            Lobby = new LobbyInfo(lobbyId, "test", 1, GameStatus.Playing),
        };

        var cmd = new RecoverGameStateCommand(game, "Test Lobby", lobbyId);
        lobbyStateActor.Tell(cmd);

        // Act
        var keyState = new Dictionary<GameControlMessages.Key, bool>
        {
            { GameControlMessages.Key.Left, false },
            { GameControlMessages.Key.Right, false },
            { GameControlMessages.Key.Up, true },
            { GameControlMessages.Key.Down, false },
        };

        var cmd2 = new GameControlMessages.UpdateKeyStatesCommand(keyState);
        var ses = cmd2.ToSessionableMessage(userSessionActor.Ref.Path.Name, userSessionActor.Ref.Path.ToString());
        var trc = ses.ToTraceable(null);
        lobbyStateActor.Tell(trc, userSessionActor);

        lobbyStateActor.Tell(new BroadcastStateCommand());

        userSessionActor.ExpectMsg<Traceable<GameStateBroadcast>>(trc =>
        {
            trc.Message.State.Status.Should().Be(GameStatus.Playing);
            trc.Message.State.Players.First().Location.Should().NotBe(new Location(0, 0));
        });
    }

    [Fact]
    public void ship_rotates_after_a_tick()
    {
        var lobbyId = Guid.NewGuid();
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var supervisor = CreateTestProbe();
        var lobbyPersister = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", lobbyId, supervisor, lobbyEmitter, lobbyPersister, false));

        var player = new PlayerState
        {
            UserSessionActorPath = userSessionActor.Ref.Path.ToString(),
            Username = userSessionActor.Ref.Path.Name,
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };

        var players = new Dictionary<string, PlayerState> { { player.Username, player } };

        GameState game = new()
        {
            Status = GameStatus.Playing,
            Players = players,
            TickCount = 1,
            Lobby = new LobbyInfo(lobbyId, "test", 1, GameStatus.Playing),
        };

        var cmd = new RecoverGameStateCommand(game, "Test Lobby", lobbyId);
        lobbyStateActor.Tell(cmd);

        // Act
        var keyState = new Dictionary<GameControlMessages.Key, bool>
        {
            { GameControlMessages.Key.Left, true },
            { GameControlMessages.Key.Right, false },
            { GameControlMessages.Key.Up, false },
            { GameControlMessages.Key.Down, false },
        };

        var cmd2 = new GameControlMessages.UpdateKeyStatesCommand(keyState);
        var ses = cmd2.ToSessionableMessage(userSessionActor.Ref.Path.Name, userSessionActor.Ref.Path.ToString());
        var trc = ses.ToTraceable(null);
        lobbyStateActor.Tell(trc, userSessionActor);

        lobbyStateActor.Tell(new BroadcastStateCommand());

        userSessionActor.ExpectMsg<Traceable<GameStateBroadcast>>(trc =>
        {
            trc.Message.State.Players.First().Heading.Should().NotBe(new Heading(0));
        });
    }

    // Lobby periodically broadcasts its state to the supervisor with updated player count
    [Fact]
    public void test_broadcast_state_command()
    {
        // Arrange
        var lobbyId = Guid.NewGuid();
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var supervisor = CreateTestProbe();
        var lobbyPersister = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", lobbyId, supervisor, lobbyEmitter, lobbyPersister, false));

        var player = new PlayerState
        {
            UserSessionActorPath = userSessionActor.Ref.Path.ToString(),
            Username = userSessionActor.Ref.Path.Name,
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };
        var game = new GameState
        {
            Status = GameStatus.Playing,
            Players = new Dictionary<string, PlayerState> { { player.Username, player } },
            TickCount = 1,
            Lobby = new LobbyInfo(lobbyId, "Test Lobby", 1, GameStatus.Playing)
        };

        var cmd = new RecoverGameStateCommand(game, "Test Lobby", lobbyId);
        lobbyStateActor.Tell(cmd);

        // Act
        lobbyStateActor.Tell(new BroadcastStateCommand());

        // Assert
        supervisor.ExpectMsg<LobbyInfo>(msg =>
        {
            msg.PlayerCount.Should().Be(1);
        }, TimeSpan.FromSeconds(6));
    }
}
