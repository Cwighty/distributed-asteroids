using System.Security.Cryptography;
using System.Text;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Asteroids.Shared.Accounts.AccountStateActor;

namespace Asteroids.Tests.Accounts;

public class AccountStateActorTests : TestKit
{
    private const string Test_Username = "test-username";
    private Password Test_Password = new Password(Test_Username); //"test-password";
    private const string Test_ConnectedId = "test-connected-id";
    private const int keySize = 64;
    private const int iterations = 300_000;
    private readonly HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;
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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));

        // create a salt based on username
        byte[] salt = Encoding.UTF8.GetBytes(Test_Username);

        // encrypt password
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Test_Username), salt, iterations, hashAlgorithm, keySize);
        var hashedPassword = Convert.ToHexString(hash);


        actorRef.Tell(new InitializeAccounts(new Dictionary<string, string> { { Test_Username, hashedPassword } }));
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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "", new Password("password"));

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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "username", new Password(""));

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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "existing_username", new Password("password"));
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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "us", new Password("password"));

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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "thisusernameistoolong", new Password("password"));

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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "username", new Password("pass"));

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
        var actorRef = Sys.ActorOf(Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, serviceProvider)));
        var commitAccountCommand = new CommitAccountCommand(Guid.NewGuid(), "username", new Password("thispasswordiswaytoolong"));

        // Act
        actorRef.Tell(commitAccountCommand);
        var result = ExpectMsg<AccountCommittedEvent>();

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Password must be between 6 and 20 characters", result.Error);
    }

}
