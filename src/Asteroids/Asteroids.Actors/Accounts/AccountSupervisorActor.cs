using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Contracts;
namespace Asteroids.Shared.Accounts;

public record CreateAccountCommand(string ConnectionId, string Username, string Password) : IReturnableMessage;
public record CreateAccountEvent(string ConnectionId, bool success, string? errorMessage = null) : IReturnableMessage;

public record LoginCommand(string ConnectionId, string Username, string Password) : IReturnableMessage;
public record LoginEvent(string ConnectionId, bool success, string? errorMessage = null) : IReturnableMessage;

public class AccountSupervisorActor : ReceiveActor
{
    private IActorRef? accountStateActor;
    public Dictionary<Guid, string> CreationSagas { get; set; } = new(); 

    public AccountSupervisorActor()
    {
        accountStateActor = Context.ActorOf(AccountStateActor.Props(), "account-state");
        Context.Watch(accountStateActor);

        AcountCreation();
        Login();


        Receive<Terminated>(t =>
        {
            Log.Info("AccountStateActor terminated");
            accountStateActor = Context.ActorOf(AccountStateActor.Props(), "account-state");
        });

    }

    private void Login()
    {
        Receive<LoginCommand>(c =>
        {
            accountStateActor.Tell(new LoginCommand(c.ConnectionId, c.Username, c.Password));
        });

        Receive<LoginEvent>(e =>
        {
            var accountEmitterActor = Context.ActorOf(AccountEmitterActor.Props());
            if (e.success)
            {
                // create user session
            }
            else
            {
                accountEmitterActor.Tell(new LoginEvent(e.ConnectionId, false, e.errorMessage));
            }
        });
    }

    private void AcountCreation()
    {
        Receive<CreateAccountCommand>(c =>
        {
            Log.Info("Received CreateNewAccountCommand at AccountActor");

            var sagaId = Guid.NewGuid();
            var sagaActor = Context.ActorOf(AccountCreationSagaActor.Props(accountStateActor));

            CreationSagas.Add(sagaId, c.ConnectionId);

            var startSaga = new StartAccountCreation(sagaId, c.Username, c.Password);
            sagaActor.Tell(startSaga);
        });

        Receive<AccountCommittedEvent>(e =>
        {
            var connectionId = CreationSagas[e.OriginalCommand.RequestId];
            CreationSagas.Remove(e.OriginalCommand.RequestId);

            var accountEmitterActor = Context.ActorOf(AccountEmitterActor.Props());
            if (e.Success)
            {
                Log.Info("Account creation successful");
                accountEmitterActor.Tell(new CreateAccountEvent(connectionId, true));
            }
            else
            {
                Log.Info("Account creation failed");
                accountEmitterActor.Tell(new CreateAccountEvent(connectionId, false, e.Error));
            }
        });
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props()
    {
       return Akka.Actor.Props.Create<AccountSupervisorActor>();
    }
}
