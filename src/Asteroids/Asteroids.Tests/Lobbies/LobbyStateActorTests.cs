using System.Diagnostics;
using FluentAssertions;

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
        userSessionActor.ExpectMsg<Traceable<GameStateBroadcast>>(trc =>
        {
            trc.Message.State.State.Should().Be(LobbyState.Countdown);
        }
        );
    }
}