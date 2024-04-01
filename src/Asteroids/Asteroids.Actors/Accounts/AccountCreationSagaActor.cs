using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;

namespace Asteroids.Shared.Accounts;


public record StartAccountCreation(Guid RequestId, string Username, string Password);

public class AccountCreationSagaActor : ReceiveActor
{
    private IServiceScope _scope;
    private IActorRef _originalSender;
    private StartAccountCreation _currentCommand;
    private IActorRef _accountStateActor;

    public AccountCreationSagaActor()
    {
        _accountStateActor = Context.System.ActorSelection("/user/account/account-state").ResolveOne(TimeSpan.FromSeconds(5)).Result;

        Receive<StartAccountCreation>(cmd => HandleStartAccountCreation(cmd));
        Receive<AccountCommittedEvent>(e => HandleAccountCommittedEvent(e));
    }

    private void HandleStartAccountCreation(StartAccountCreation cmd)
    {
        Log.Info("Received StartAccountCreation command");
        _originalSender = Sender;
        _currentCommand = cmd;

        _accountStateActor.Tell(new CommitAccountCommand(cmd.RequestId, cmd.Username, cmd.Password));
    }

    private void HandleAccountCommittedEvent(AccountCommittedEvent e)
    {
        _originalSender.Tell(e);
        Context.Stop(Self);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    protected override void PostStop()
    {
        _scope?.Dispose();
    }

    public static Props Props()
    {
        return Akka.Actor.Props.Create<AccountCreationSagaActor>();
    }
}

