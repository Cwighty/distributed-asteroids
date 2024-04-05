using Asteroids.Shared.Accounts;
using Asteroids.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Asteroids.Shared.Accounts.AccountStateActor;

namespace Asteroids.Tests.Accounts;

public class AccountStateActorTests : TestKit
{
    private const string Test_Username = "test-username";
    private const string Test_Password = "test-password";
    private const string Test_ConnectedId = "test-connected-id";
    private readonly Mock<IStorageService> storageServiceMock;
    private readonly IServiceProvider serviceProvider;

    public AccountStateActorTests()
    {
        storageServiceMock = new Mock<IStorageService>();
        storageServiceMock
            .Setup(x => x.IdempodentReduceUntilSuccess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, string>>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IStorageService>(storageServiceMock.Object);

        serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void AccountStateActor_ValidLoginCommand_ReturnsLoginEventWithSuccessFalse_WhenAccountDoesNotExist()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var loginCommand = new LoginCommand(Test_ConnectedId, Test_Username, Test_Password).ToTraceable(null);

        // Act
        actorRef.Tell(loginCommand);
        var result = ExpectMsg<LoginEvent>();

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public void AccountStateActor_ValidLoginCommand_ReturnsLoginEventWithSuccessTrue_WhenAccountDoesExist()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        actorRef.Tell(new InitializeAccounts(new Dictionary<string, string> { { Test_Username, Test_Password } }));
        var loginCommand = new LoginCommand(Test_ConnectedId, Test_Username, Test_Password).ToTraceable(null);

        // Act
        actorRef.Tell(loginCommand);
        var result = ExpectMsg<LoginEvent>();

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public void AccountStateActor_EmptyUsernameCommitAccountCommand_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "", "password");

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username or password cannot be empty", result.Error);
    }

    [Fact]
    public void AccountStateActor_EmptyPasswordCommitAccountCommand_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "username", "");

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username or password cannot be empty", result.Error);
    }

    [Fact]
    public void AccountStateActor_HandleExistingUsernameCommitAccountCommand_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "existing_username", "password");
        actorRef.Tell(new InitializeAccounts(new Dictionary<string, string> { { "existing_username", "password" } }));

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username already exists", result.Error);
    }

    [Fact]
    public void AccountStateActor_HandleCommitAccountCommand_WithShortUsername_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "us", "password");

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username must be between 3 and 20 characters", result.Error);
    }

    [Fact]
    public void AccountStateActor_HandleLongUsernameCommitAccountCommand_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "thisusernameistoolong", "password");

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username must be between 3 and 20 characters", result.Error);
    }

    [Fact]
    public void AccountStateActor_HandleCommitAccountCommand_WithShortPassword_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "username", "pass");

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Password must be between 6 and 20 characters", result.Error);
    }

    [Fact]
    public void AccountStateActor_HandleLongPasswordCommitAccountCommand_ReturnsAccountCommittedEventWithSuccessFalseAndErrorMessage()
    {
        // Arrange
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "username", "thispasswordiswaytoolong");

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Password must be between 6 and 20 characters", result.Error);
    }

}
