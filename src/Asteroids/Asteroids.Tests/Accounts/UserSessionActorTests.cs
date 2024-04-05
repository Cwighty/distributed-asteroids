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

        var keyStates = new Dictionary<GameControlMessages.Key, bool>();

        var cmd1 = new GameControlMessages.UpdateKeyStatesCommand(keyStates);
        var sessionCmd1 = cmd1.ToSessionableMessage(connectionId, username);
        var trcCmd1 = sessionCmd1.ToTraceable(null);
        userSessionActor.Tell(trcCmd1);

        // Assert
        lobby1.ExpectMsg<Traceable<SessionScoped<GameControlMessages.UpdateKeyStatesCommand>>>();
    }

}