using System.Security.Cryptography;
using System.Text;
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Microsoft.Extensions.DependencyInjection;

namespace Asteroids.Shared.Accounts;


public record StartAccountCreation(Guid RequestId, string Username, Password Password);

public class AccountCreationSagaActor : ReceiveActor
{
    private IActorRef? _originalSender;
    private IActorRef _accountStateActor;
    private readonly HashAlgorithmName hashAlgorithm;
    private readonly int keySize;
    private readonly int iterations;

    public AccountCreationSagaActor(IActorRef accountStateActor, HashAlgorithmName hashAlgorithm, int keySize = 64, int iterations = 300_000)
    {
        _accountStateActor = accountStateActor;
        this.hashAlgorithm = hashAlgorithm;
        this.keySize = keySize;
        this.iterations = iterations;
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

    public static Props Props(IActorRef accountStateActor, HashAlgorithmName hashAlgorithm, int keySize = 64, int iterations = 300_000)
    {
        return Akka.Actor.Props.Create(() => new AccountCreationSagaActor(accountStateActor, hashAlgorithm, keySize, iterations));
    }
}

