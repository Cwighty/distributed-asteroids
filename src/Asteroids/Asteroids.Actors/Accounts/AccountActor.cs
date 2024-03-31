using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Storage;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace Asteroids.Shared.Accounts;

public record CreateNewAccountCommand(string ConnectionId, string Username, string Password) : IReturnableMessage;
public record AccountCreatedEvent(string ConnectionId, bool success, string? errorMessage = null) : IReturnableMessage;

public class AccountActor : ReceiveActor
{
    public Dictionary<Guid, string> CreationSagas { get; set; } = new(); 

    public AccountActor()
    {
        Receive<CreateNewAccountCommand>(c =>
        {
            Log.Info("Received CreateNewAccountCommand at AccountActor"); 

            var sagaId = Guid.NewGuid();
            var sagaActor = Context.ActorOf(AccountCreationSagaActor.Props());

            CreationSagas.Add(sagaId, c.ConnectionId);

            var startSaga = new StartAccountCreation(sagaId, c.Username, c.Password);
            sagaActor.Tell(startSaga);
        });

        Receive<AccountCommittedEvent>(e =>
        {
            var connectionId = CreationSagas[e.RequestId];
            CreationSagas.Remove(e.RequestId);

            var accountEmitterActor = Context.ActorOf(AccountEmitterActor.Props());
            if (e.Success)
            {
                Log.Info("Account creation successful");
                accountEmitterActor.Tell(new AccountCreatedEvent(connectionId, true));
            }
            else
            {
                Log.Info("Account creation failed");
                accountEmitterActor.Tell(new AccountCreatedEvent(connectionId, false, e.ErrorMessage));
            }
        });

    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountActor>();
    }
}
