using Asteroids.Shared.GameStateEntities;
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
        var lobbyPersistenceActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(Props.Create(() => new LobbySupervisor(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref, lobbyPersistenceActor.Ref)));

        // Act
        lobbySupervisor.Tell(new CreateLobbyCommand("Test Lobby", GameParameters.Default));

        // Assert
        lobbiesEmmitterActor.ExpectMsg<CreateLobbyEvent>();
    }

    // HandleViewAllLobbiesQuery returns a ViewAllLobbiesResponse with all lobbies
    [Fact]
    public void handle_ViewAllLobbiesQuery()
    {
        // Arrange
        var lobbyId = Guid.NewGuid();
        var lobbiesEmmitterActor = CreateTestProbe();
        var lobbyEmitterActor = CreateTestProbe();
        var lobbyPersistenceActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(LobbySupervisor.Props(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref, lobbyPersistenceActor.Ref));

        var lobby1 = new LobbyInfo(lobbyId, "Test Lobby 1", 0, Shared.GameStateEntities.GameStatus.Joining);

        // Create lobbies
        lobbySupervisor.Tell(new CreateLobbyCommand(lobby1.Name, GameParameters.Default));
        lobbiesEmmitterActor.ExpectMsg<CreateLobbyEvent>();

        // Act
        var viewAllLobbiesQuery = new ViewAllLobbiesQuery();
        lobbySupervisor.Tell(viewAllLobbiesQuery);

        // Assert
        lobbiesEmmitterActor.ExpectMsg<ViewAllLobbiesResponse>(msg =>
        {
            msg.Lobbies.Select(x => x.Name).Should().BeEquivalentTo(new List<LobbyInfo> { lobby1 }.Select(x => x.Name));
        });
    }

    // HandleJoinLobbyCommand if lobby does not exist returns a JoinLobbyEvent with an error 
    [Fact]
    public void test_handle_join_lobby_when_lobby_does_not_exist()
    {
        // Arrange
        var lobbyId = Guid.NewGuid();
        var actorPath = "actorPath";
        var lobbiesEmmitterActor = CreateTestProbe();
        var lobbyEmitterActor = CreateTestProbe();
        var userSessionActor = CreateTestProbe();
        var lobbyPersistenceActor = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(LobbySupervisor.Props(lobbiesEmmitterActor.Ref, lobbyEmitterActor.Ref, lobbyPersistenceActor.Ref));

        var cmd = new JoinLobbyCommand(lobbyId, actorPath);
        var trc = cmd.ToTraceable(null);

        // Act
        lobbySupervisor.Tell(trc, userSessionActor.Ref);

        // Assert
        userSessionActor.ExpectMsg<JoinLobbyEvent>(msg =>
        {
            msg.ErrorMessage.Should().Be("Lobby not found");
        });
    }

}