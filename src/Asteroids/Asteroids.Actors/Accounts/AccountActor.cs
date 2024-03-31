using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Shared.Accounts;

public record CreateNewAccountCommand(string ConnectionId, string Username, string Password) : IReturnableMessage;
public record AccountCreatedEvent(string ConnectionId) : IReturnableMessage;
public class AccountActor : ReceiveActor
{
    public AccountActor()
    {
        Receive<CreateNewAccountCommand>(c =>
        {
            Log.Info("Received CreateNewAccountCommand at AccountActor"); 
            // create account
            var created = new AccountCreatedEvent(c.ConnectionId);

            // tell acount emmitter
            var accountEmitter = Context.ActorOf(AccountEmmitterActor.Props());
            accountEmitter.Tell(created);
        });
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountActor>();
    }
}
