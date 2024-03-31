using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using static System.Formats.Asn1.AsnWriter;

namespace Asteroids.Shared.Accounts;


public record StartAccountCreation(Guid RequestId, string Username, string Password);
public record AccountCommittedEvent(Guid RequestId, bool Success, string ErrorMessage);

public class AccountCreationSagaActor : ReceiveActor
{
    public record CommitAccountCommand(Guid RequestId, string Username, string Password);
    public record UsernameIsUniqeResult(Guid RequestId, bool IsUnique, string ErrorMessage);

    private IServiceScope _scope;
    IStorageService storageService;
    const string ACCOUNT_KEY = "user-accounts";

    // References to other actors
    private readonly IActorRef _storageActor;
    private readonly IActorRef _accountEventEmitter;
    private StartAccountCreation _currentCommand;

    private IActorRef _originalSender;

    private Dictionary<string, string> _accounts;

    public AccountCreationSagaActor(IServiceProvider sp)
    {
        _scope = sp.CreateScope();
        storageService = _scope.ServiceProvider.GetRequiredService<IStorageService>();

        _accountEventEmitter = Context.ActorOf(AccountEmitterActor.Props());

        Receive<StartAccountCreation>(cmd => HandleStartAccountCreation(cmd));
        Receive<UsernameIsUniqeResult>(response => HandleUsernameUniqeResult(response));
        Receive<CommitAccountCommand>(response => HandleCommitAccountCommand(response));
        Receive<AccountCommittedEvent>(e => HandleAccountCommittedEvent(e));
    }

    private void HandleStartAccountCreation(StartAccountCreation cmd)
    {
        Log.Info("Received StartAccountCreation command");
        _originalSender = Sender;
        _currentCommand = cmd;

        var task = storageService.StrongGet(ACCOUNT_KEY);
        task.ContinueWith(r =>
        {
            Log.Info("Retrieved accounts from storage");
            try
            {
                if (string.IsNullOrEmpty(r.Result.Value))
                {
                    _accounts = new Dictionary<string, string>();
                }
                else
                {
                    _accounts = JsonSerializer.Deserialize<Dictionary<string, string>>(r.Result.Value);
                }
            } 
            catch (Exception e)
            {
                Log.Error(e, "Failed to deserialize accounts from storage");
                _accounts = new Dictionary<string, string>();
            }

            Log.Info("Checking if username has been seen before {0}", _accounts?.ContainsKey(cmd.Username));

            if (_accounts?.ContainsKey(cmd.Username) ?? false)
            {
                return new UsernameIsUniqeResult(cmd.RequestId, false, "Username already exists.");
            }

            return  new UsernameIsUniqeResult(cmd.RequestId, true, null);
        }).PipeTo(Self);
    }

    private void HandleUsernameUniqeResult(UsernameIsUniqeResult result)
    {
        if (!result.IsUnique)
        {
            _originalSender.Tell(new AccountCommittedEvent(_currentCommand.RequestId, false, result.ErrorMessage));
            Context.Stop(Self);
            return;
        }

        var commitCommand = new CommitAccountCommand(result.RequestId, _currentCommand.Username, _currentCommand.Password);
        Self.Tell(commitCommand);
    }

    private void HandleCommitAccountCommand(CommitAccountCommand command)
    {
        Log.Info("Received CommitAccountCommand");
        var unmodified = JsonSerializer.Serialize(_accounts);
        var reducer = (string oldValue) =>
        {
            var oldAccounts = JsonSerializer.Deserialize<Dictionary<string, string>>(oldValue);
            var modifiedAccounts = oldAccounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            modifiedAccounts.Add(command.Username, command.Password);
            return JsonSerializer.Serialize(modifiedAccounts);
        };
        var task = storageService.IdempodentReduceUntilSuccess(ACCOUNT_KEY, unmodified, reducer);

        task.ContinueWith(r =>
        {
            Log.Info("Account committed to storage", r);
            if (r.IsFaulted)
            {
               return new AccountCommittedEvent(command.RequestId, false, "Unable to commit account");
            }
            else
            { 
                return new AccountCommittedEvent(command.RequestId, true, "Account created successfully.");
            }
        }).PipeTo(Self);
    }

    private void HandleAccountCommittedEvent(AccountCommittedEvent e)
    {
        _originalSender.Tell(e);
        Context.Stop(Self);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    protected override void PostStop()
    {
        _scope.Dispose();
    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountCreationSagaActor>();
    }
}

