using static Asteroids.Shared.UserSession.UserSessionSupervisor;

namespace Asteroids.Tests.Accounts;

public class UserSessionSupervisorTests : TestKit
{
    [Fact]
    public void test_create_user_session_actor()
    {
        var connectionId = "connectionId";
        var username = "username";
        // Arrange
        var lobbySupervisor = CreateTestProbe();
        var userSessionSupervisor = Sys.ActorOf(Akka.Actor.Props.Create(() => new UserSessionSupervisor()));
        userSessionSupervisor.Tell(new FetchedLobbySupervisorEvent(lobbySupervisor.Ref));

        // Act
        userSessionSupervisor.Tell(new StartUserSessionCommmand(connectionId, username));

        // Assert
        lobbySupervisor.ExpectNoMsg();
        ExpectMsg<StartUserSessionEvent>(msg =>
        {
            Assert.Equal(connectionId, msg.ConnectionId);
            Assert.EndsWith($"user-session_{username}", msg.SessionActorPath);
        });
    }

    [Fact]
    public void test_create_user_session_actor_then_ask_again() // second time it is pulled from the dictionary
    {
        var connectionId = "connectionId";
        var username = "username";
        // Arrange
        var lobbySupervisor = CreateTestProbe();
        var userSessionSupervisor = Sys.ActorOf(Akka.Actor.Props.Create(() => new UserSessionSupervisor()));
        userSessionSupervisor.Tell(new FetchedLobbySupervisorEvent(lobbySupervisor.Ref));

        // Act
        userSessionSupervisor.Tell(new StartUserSessionCommmand(connectionId, username));
        ExpectMsg<StartUserSessionEvent>();

        userSessionSupervisor.Tell(new StartUserSessionCommmand(connectionId, username));
        ExpectMsg<StartUserSessionEvent>();

        // Assert
    }

    [Fact]
    public void test_find_existing_user_session_actor()
    {
        // Arrange
        var lobbySupervisor = CreateTestProbe();
        var userSessionSupervisor = Sys.ActorOf(Akka.Actor.Props.Create(() => new UserSessionSupervisor()));
        userSessionSupervisor.Tell(new FetchedLobbySupervisorEvent(lobbySupervisor.Ref));

        // Create a user session actor
        var connectionId = "connectionId";
        var username = "usern$ame";
        var actorPath = $"user-session_{AkkaHelper.UsernameToActorPath(username)}";
        var userSessionActor = CreateTestProbe();
        userSessionSupervisor.Tell(new StartUserSessionCommmand(connectionId, username));

        ExpectMsg<StartUserSessionEvent>(msg => actorPath = msg.SessionActorPath);

        // Act
        userSessionSupervisor.Tell(new FindUserSessionRefQuery(actorPath).ToSessionableMessage(connectionId, actorPath));

        // Assert
        ExpectMsg<FindUserSessionResult>(msg =>
        {
            Assert.Equal(actorPath, msg.UserSessionRef!.Path.ToString());
        });
    }
}