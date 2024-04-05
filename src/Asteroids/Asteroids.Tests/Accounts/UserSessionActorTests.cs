namespace Asteroids.Tests.Accounts;

public class UserSessionActorTests : TestKit
{

    // UserSessionActor receives SessionScoped<CreateLobbyCommand> message and forwards it to lobbySupervisor
    [Fact]
    public void Test_UserSessionActor_Forwards_CreateLobbyCommand()
    {
        // Arrange
        var connectionId = "connectionId";
        var username = "username";
        var lobbySupervisor = CreateTestProbe();
        var userSessionActor = Sys.ActorOf(Props.Create(() => new UserSessionActor(connectionId, username, lobbySupervisor.Ref)));

        // Act
        var createLobbyCommand = new CreateLobbyCommand("test");
        var sessionScoped = createLobbyCommand.ToSessionableMessage(connectionId, username);
        userSessionActor.Tell(sessionScoped);

        // Assert
        lobbySupervisor.ExpectMsg<CreateLobbyCommand>();
    }

    // UserSessionActor stores ref to lobby actor after receiving SessionScoped<JoinLobbyCommand> and sends lobby messages to it 
    [Fact]
    public void Test_UserSessionActor_Forwards_Lobby_Commands_Direct_To_Lobby_StartGameCommand()
    {
        // Arrange
        var connectionId = "connectionId";
        var username = "username";
        var lobby1 = CreateTestProbe();
        var userSessionActor = Sys.ActorOf(Props.Create(() => new UserSessionActor(connectionId, username, null)));

        // Act
        var joinLobbyEvent = new JoinLobbyEvent(new GameStateSnapshot());
        var traceableEvent = joinLobbyEvent.ToTraceable(null);
        userSessionActor.Tell(traceableEvent, lobby1.Ref);

        var startGameCommand = new StartGameCommand(1);
        var sessionScopedStart = startGameCommand.ToSessionableMessage(connectionId, username);
        var traceableStart = sessionScopedStart.ToTraceable(null);
        userSessionActor.Tell(traceableStart);

        // Assert
        lobby1.ExpectMsg<Traceable<StartGameCommand>>();
    }

    [Fact]
    public void Test_UserSessionActor_Forwards_Lobby_Commands_Direct_To_Lobby_LobbyStateQuery()
    {
        // Arrange
        var connectionId = "connectionId";
        var username = "username";
        var lobby1 = CreateTestProbe();
        var userSessionActor = Sys.ActorOf(Props.Create(() => new UserSessionActor(connectionId, username, null)));

        // Act
        var joinLobbyEvent = new JoinLobbyEvent(new GameStateSnapshot());
        var traceableEvent = joinLobbyEvent.ToTraceable(null);
        userSessionActor.Tell(traceableEvent, lobby1.Ref);

        var lobbyStateQuery = new LobbyStateQuery(1);
        var sessionScopedQuery = lobbyStateQuery.ToSessionableMessage(connectionId, username);
        var traceableQuery = sessionScopedQuery.ToTraceable(null);
        userSessionActor.Tell(traceableQuery);

        // Assert
        lobby1.ExpectMsg<Traceable<LobbyStateQuery>>();
    }

    [Fact]
    public void Test_UserSessionActor_Forwards_Lobby_Commands_Direct_To_Lobby_GameControlCommands()
    {
        // Arrange
        var connectionId = "connectionId";
        var username = "username";
        var lobby1 = CreateTestProbe();
        var userSessionActor = Sys.ActorOf(Props.Create(() => new UserSessionActor(connectionId, username, null)));

        // Act
        var joinLobbyEvent = new JoinLobbyEvent(new GameStateSnapshot());
        var traceableEvent = joinLobbyEvent.ToTraceable(null);
        userSessionActor.Tell(traceableEvent, lobby1);

        var cmd1 = new GameControlMessages.KeyDownCommand(GameControlMessages.Key.Down);
        var sessionCmd1 = cmd1.ToSessionableMessage(connectionId, username);
        var trcCmd1 = sessionCmd1.ToTraceable(null);
        userSessionActor.Tell(trcCmd1);

        var cmd2 = new GameControlMessages.KeyUpCommand(GameControlMessages.Key.Down);
        var sessionCmd2 = cmd2.ToSessionableMessage(connectionId, username);
        var trcCmd2 = sessionCmd2.ToTraceable(null);
        userSessionActor.Tell(trcCmd2);

        // Assert
        lobby1.ExpectMsg<Traceable<SessionScoped<GameControlMessages.KeyDownCommand>>>();
        lobby1.ExpectMsg<Traceable<SessionScoped<GameControlMessages.KeyUpCommand>>>();
    }

    // UserSessionActor receives CreateLobbyEvent message and forwards it to lobbySupervisor with correct connectionId to pass to the emitter with correct connectionId
    [Fact]
    public void Test_UserSessionActor_Forwards_CreateLobbyEvent_ToSupervisor_ToEmitter()
    {

        // Arrange
        var connectionId = "connectionId";
        var username = "username";
        var lobbiesEmitter = CreateTestProbe();
        var lobbySupervisor = Sys.ActorOf(Props.Create(() => new LobbySupervisor(lobbiesEmitter.Ref, null)));
        var userSessionActor = Sys.ActorOf(Props.Create(() => new UserSessionActor(connectionId, username, lobbySupervisor)));

        // Act
        var createLobbyEvent = new CreateLobbyEvent(new List<LobbyInfo>() { });
        userSessionActor.Tell(createLobbyEvent);

        // Assert
        lobbiesEmitter.ExpectMsg<Returnable<CreateLobbyEvent>>((e) => e.ConnectionId == connectionId);
    }

}