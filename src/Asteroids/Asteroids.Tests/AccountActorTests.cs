using Akka.TestKit.Xunit2;
using Asteroids.Shared.Accounts;
using Asteroids.Shared.Storage;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Asteroids.Tests;

public class AccountActorTests : TestKit
{
    // Actor receives StartAccountCreation command and successfully commits account to storage
    [Fact]
    public async Task actor_receives_start_account_creation_command_and_successfully_commits_account_to_storage()
    {
        // Arrange
        var mockStorageService = new Mock<IStorageService>();
        mockStorageService.Setup(s => s.StrongGet(It.IsAny<string>())).ReturnsAsync(new VersionedValue<string>(0,""));
        mockStorageService.Setup(s => s.IdempodentReduceUntilSuccess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, string>>(), It.IsAny<int>(), It.IsAny<int>())).Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(mockStorageService.Object)
            .BuildServiceProvider();

        var actor = Sys.ActorOf(AccountCreationSagaActor.Props(serviceProvider));
        var probe = CreateTestProbe();

        // Act
        actor.Tell(new StartAccountCreation(Guid.NewGuid(), "username", "password"), probe.Ref);
        var response = await probe.ExpectMsgAsync<AccountCommittedEvent>();

        // Assert
        response.Success.Should().BeTrue();
        response.ErrorMessage.Should().BeNull();
    }
}