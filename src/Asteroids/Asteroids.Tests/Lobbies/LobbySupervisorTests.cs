using FluentAssertions;

namespace Asteroids.Tests.Lobbies;

public class LobbySupervisorTests : TestKit
{

    // HandleCreateLobbyCommand creates a new lobby and returns a CreateLobbyEvent
    [Fact]
    public void test_create_lobby_command()
    {
        // Arrange
        var lobbiesEmmitterActor = CreateTestProbe();
        var lobbyEmitterActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(Props.Create(() => new LobbySupervisor(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref)));

        // Act
        lobbySupervisor.Tell(new CreateLobbyCommand("Test Lobby"));

        // Assert
        lobbiesEmmitterActor.ExpectMsg<CreateLobbyEvent>();
    }

    // HandleViewAllLobbiesQuery returns a ViewAllLobbiesResponse with all lobbies
    [Fact]
    public void Test_HandleViewAllLobbiesQuery()
    {
        // Arrange
        var lobbiesEmmitterActor = CreateTestProbe();
        var lobbyEmitterActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(LobbySupervisor.Props(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref));

        var lobby1 = new LobbyInfo(1, "Test Lobby 1", 0);

        // Create lobbies
        lobbySupervisor.Tell(new CreateLobbyCommand(lobby1.Name));
        lobbiesEmmitterActor.ExpectMsg<CreateLobbyEvent>();

        // Act
        var viewAllLobbiesQuery = new ViewAllLobbiesQuery();
        lobbySupervisor.Tell(viewAllLobbiesQuery);

        // Assert
        lobbiesEmmitterActor.ExpectMsg<ViewAllLobbiesResponse>(msg =>
        {
            msg.Lobbies.Should().BeEquivalentTo(new List<LobbyInfo> { lobby1 });
        });
    }

    // HandleJoinLobbyCommand if lobby does not exist returns a JoinLobbyEvent with an error 
    [Fact]
    public void Test_HandleJoinLobbyCommand_LobbyDoesNotExist()
    {
        // Arrange
        var actorPath = "actorPath";
        var lobbiesEmmitterActor = CreateTestProbe();
        var lobbyEmitterActor = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(LobbySupervisor.Props(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref));

        var cmd = new JoinLobbyCommand(1, actorPath);
        var trc = cmd.ToTraceable(null);

        // Act
        lobbySupervisor.Tell(trc, userSessionActor.Ref);

        // Assert
        userSessionActor.ExpectMsg<JoinLobbyEvent>(msg =>
        {
            msg.ErrorMessage.Should().Be("Lobby not found");
        });
    }

    // HandleJoinLobbyCommand if lobby exists returns a JoinLobbyEvent
    [Fact]
    public void Test_HandleJoinLobbyCommand_LobbyExists_WithPlayerJoined()
    {
        // Arrange
        var actorPath = "actorPath";
        var lobbiesEmmitterActor = CreateTestProbe();
        var lobbyEmitterActor = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(LobbySupervisor.Props(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref));

        // Create lobby
        lobbySupervisor.Tell(new CreateLobbyCommand("Test Lobby"));
        lobbiesEmmitterActor.ExpectMsg<CreateLobbyEvent>();

        // Act
        var cmd = new JoinLobbyCommand(1, actorPath);
        var trc = cmd.ToTraceable(null);
        lobbySupervisor.Tell(trc, userSessionActor.Ref);

        // Assert
        userSessionActor.ExpectMsg<Traceable<JoinLobbyEvent>>(trc =>
        {
            trc.Message.State.Should().NotBeNull();
            trc.Message.State.Players.Should().ContainSingle(x => x.Name == actorPath);
        });
    }
}