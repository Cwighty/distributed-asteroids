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
}