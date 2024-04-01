using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;

namespace Asteroids.Shared.Accounts;


public record StartAccountCreation(Guid RequestId, string Username, string Password);

public class AccountCreationSagaActor : ReceiveActor
{
    private IActorRef? _originalSender;
    private IActorRef _accountStateActor;

    public AccountCreationSagaActor(IActorRef accountStateActor)
    {
        _accountStateActor = accountStateActor;

        Receive<StartAccountCreation>(cmd => HandleStartAccountCreation(cmd));
        Receive<AccountCommittedEvent>(e => HandleAccountCommittedEvent(e));
    }

    private void HandleStartAccountCreation(StartAccountCreation cmd)
    {
        Log.Info("Received StartAccountCreation command");
        _originalSender = Sender;

        _accountStateActor!.Tell(new CommitAccountCommand(cmd.RequestId, cmd.Username, cmd.Password));
    }

    private void HandleAccountCommittedEvent(AccountCommittedEvent e)
    {
        _originalSender!.Tell(e);
        Context.Stop(Self);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();


    public static Props Props(IActorRef accountStateActor)
    {
        return Akka.Actor.Props.Create(() => new AccountCreationSagaActor(accountStateActor));
    }
}

