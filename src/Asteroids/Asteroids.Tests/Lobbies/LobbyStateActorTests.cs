using System.Diagnostics;
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
        var lobbyEmitter = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", 1, lobbyEmitter));

        // Act
        var cmd = new JoinLobbyCommand(1, "user-session_user1");
        var trc = cmd.ToTraceable(null);
        lobbyStateActor.Tell(trc);

        // Assert
        ExpectMsg<Traceable<JoinLobbyEvent>>(trc =>
        {
            trc.Message.State.Lobby.Id.Should().Be(1);
            trc.Message.State.Lobby.Name.Should().Be("Test Lobby");
            trc.Message.State.Lobby.PlayerCount.Should().Be(1);
        });
    }

    // LobbyStateActor cannot start game in a non-Joining state
    [Fact]
    public void test_cannot_start_game_in_non_joining_state()
    {
        // Arrange
        var lobbyEmitter = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", 1, lobbyEmitter));
        var cmd = new StartGameCommand(1);
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
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", 1, lobbyEmitter));

        // Join
        var cmd = new JoinLobbyCommand(1, "user-session_user1");
        var trc = cmd.ToTraceable(null);
        lobbyStateActor.Tell(trc, userSessionActor);
        userSessionActor.ExpectMsg<Traceable<JoinLobbyEvent>>();

        var startCmd = new StartGameCommand(1);
        var startTrc = startCmd.ToTraceable(null);
        lobbyStateActor.Tell(startTrc, userSessionActor);
        userSessionActor.ExpectMsg<Traceable<LobbyStateChangedEvent>>();
        userSessionActor.ExpectMsg<Traceable<GameStateBroadcast>>(trc =>
        {
            trc.Message.State.State.Should().Be(LobbyState.Countdown);
        }
        );
    }


    // Movement tests
    [Fact]
    public void ship_moves_after_a_tick()
    {
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", 1, lobbyEmitter, false));

        var player = new PlayerState
        {
            UserSessionActor = userSessionActor,
            Username = userSessionActor.Ref.Path.Name,
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };

        var players = new Dictionary<string, PlayerState> { { player.Username, player } };
        var cmd = new RecoverStateCommand(LobbyState.Playing, players, "Test Lobby", 1, 1, lobbyEmitter);
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
            trc.Message.State.State.Should().Be(LobbyState.Playing);
            trc.Message.State.Players.First().Location.Should().NotBe(new Location(0, 0));
        });
    }

    [Fact]
    public void ship_rotates_after_a_tick()
    {
        var lobbyEmitter = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var lobbyStateActor = Sys.ActorOf(LobbyStateActor.Props("Test Lobby", 1, lobbyEmitter, false));

        var player = new PlayerState
        {
            UserSessionActor = userSessionActor,
            Username = userSessionActor.Ref.Path.Name,
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };

        var players = new Dictionary<string, PlayerState> { { player.Username, player } };
        var cmd = new RecoverStateCommand(LobbyState.Playing, players, "Test Lobby", 1, 1, lobbyEmitter);
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
}