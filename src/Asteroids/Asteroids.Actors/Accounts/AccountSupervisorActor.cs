using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.UserSession;
namespace Asteroids.Shared.Accounts;

public record CreateAccountCommand(string ConnectionId, string Username, string Password) : IReturnableMessage;
public record CreateAccountEvent(string ConnectionId, bool success, string? errorMessage = null) : IReturnableMessage;

public record LoginCommand(string ConnectionId, string Username, string Password) : IReturnableMessage;
public record LoginEvent(LoginCommand OriginalCommand, bool Success, string? errorMessage = null);

public class AccountSupervisorActor : ReceiveActor
{
    private IActorRef? accountStateActor;
    private IActorRef accountEmitterActor;

    public Dictionary<Guid, string> CreationSagas { get; set; } = new();

    public AccountSupervisorActor()
    {
        accountStateActor = Context.ActorOf(AccountStateActor.Props(), AkkaHelper.AccountStateActorPath);
        Context.Watch(accountStateActor);

        accountEmitterActor = Context.ActorOf(AccountEmitterActor.Props(), "account-emitter");
        Context.Watch(accountEmitterActor);

        AcountCreation();
        Login();


        Receive<Terminated>(t =>
        {
            if (t.ActorRef.Equals(accountStateActor))
            {
                Log.Info("AccountStateActor terminated");
                accountStateActor = Context.ActorOf(AccountStateActor.Props(), AkkaHelper.AccountStateActorPath);
            }
            if (t.ActorRef.Equals(accountEmitterActor))
            {
                Log.Info("AccountEmitterActor terminated");
                accountEmitterActor = Context.ActorOf(AccountEmitterActor.Props());
            }
        });

    }

    private void Login()
    {
        Receive<Traceable<LoginCommand>>(tc =>
        {
            using var activity = tc.Activity($"{nameof(AccountSupervisorActor)}: LoginCommand");
            var cmd = tc.Message;
            accountStateActor.Tell(cmd.ToTraceable(activity));
        });

        Receive<LoginEvent>(e =>
        {
            if (e.Success)
            {
                var userSessionSupervisor = Context.ActorSelection($"/user/{AkkaHelper.UserSessionSupervisorActorPath}");
                userSessionSupervisor.Tell(new StartUserSessionCommmand(e.OriginalCommand.ConnectionId, e.OriginalCommand.Username));
            }
            else
            {
                accountEmitterActor.Tell(new LoginEvent(e.OriginalCommand, false, e.errorMessage));
            }
        });

        Receive<StartUserSessionEvent>(e =>
        {
            accountEmitterActor.Tell(e);
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
