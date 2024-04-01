using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using static System.Formats.Asn1.AsnWriter;

namespace Asteroids.Shared.Accounts;


public record CurrentAccountsQuery(Guid RequestId);
public record CurrentAccountsResult(Guid RequestId, Dictionary<string, string> Accounts);

public record CommitAccountCommand(Guid RequestId, string Username, string Password);
public record AccountCommittedEvent(CommitAccountCommand OriginalCommand, bool Success, string Error);

//public record SubscribeToAccountChanges(IActorRef Subscriber);

public class AccountStateActor : ReceiveActor
{
    public record InitializeAccounts(Dictionary<string, string> Accounts);

    private IServiceScope _scope;
    IStorageService storageService;
    const string ACCOUNT_KEY = "user-accounts";

    private Dictionary<string, string> _accounts = new();

    private Dictionary<Guid, IActorRef> _commitRequests = new();

    public AccountStateActor(IServiceProvider sp)
    {
        _scope = sp.CreateScope();
        storageService = _scope.ServiceProvider.GetRequiredService<IStorageService>();

        Receive<InitializeAccounts>(cmd => HandleInitializeAccounts(cmd));
        Receive<CurrentAccountsQuery>(cmd => HandleCurrentAccountQuery(cmd));
        Receive<CommitAccountCommand>(response => HandleCommitAccountCommand(response));
        Receive<LoginCommand>(cmd => HandleLoginCommand(cmd));
        Receive<AccountCommittedEvent>(e => HandleAccountCommittedEvent(e));
    }

    private void HandleLoginCommand(LoginCommand cmd)
    {
        if (_accounts?.ContainsKey(cmd.Username) ?? false)
        {
            if (_accounts[cmd.Username] == cmd.Password)
            {
                Sender.Tell(new LoginEvent(cmd.ConnectionId, true));
                return;
            }
        }
        Sender.Tell(new LoginEvent(cmd.ConnectionId, false, "Invalid username or password"));
    }

    private void HandleInitializeAccounts(InitializeAccounts cmd)
    {
        _accounts = cmd.Accounts;
    }

    private void HandleCurrentAccountQuery(CurrentAccountsQuery cmd)
    {
        Sender.Tell(new CurrentAccountsResult(cmd.RequestId, _accounts));
    }
    
    private void HandleCommitAccountCommand(CommitAccountCommand command)
    {
        _commitRequests.Add(command.RequestId, Sender);

        // validate
        if (_accounts?.ContainsKey(command.Username) ?? false)
        {
            Sender.Tell(new AccountCommittedEvent(command, false, "Username already exists"));
            _commitRequests.Remove(command.RequestId);
            return;
        }

        // commit
        var unmodified = JsonSerializer.Serialize(_accounts ?? new Dictionary<string, string>());
        var reducer = (string oldValue) =>
        {
            var oldAccounts = JsonSerializer.Deserialize<Dictionary<string, string>>(oldValue);
            var modifiedAccounts = oldAccounts?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>();
            modifiedAccounts.Add(command.Username, command.Password);
            return JsonSerializer.Serialize(modifiedAccounts);
        };
        var task = storageService.IdempodentReduceUntilSuccess(ACCOUNT_KEY, unmodified, reducer);

        task.ContinueWith(r =>
        {
            if (r.IsFaulted)
            {
               return new AccountCommittedEvent(command, false, "Unable to commit account");
            }
            else
            { 
                return new AccountCommittedEvent(command, true, "Account created successfully.");
            }
        }).PipeTo(Self);
    }

    private void HandleAccountCommittedEvent(AccountCommittedEvent e)
    {
        // notify original requestor
        if (e.Success)
        {
            _accounts.Add(e.OriginalCommand.Username, e.OriginalCommand.Password);
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
        else
        {
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    protected override void PreStart()
    {
        Log.Info("AccountStateActor started, initializing accounts from storage");
        var accountsTask = storageService.StrongGet(ACCOUNT_KEY);

        accountsTask.ContinueWith(r =>
        {
            if (string.IsNullOrEmpty(r.Result.Value))
            {
                return new InitializeAccounts(new Dictionary<string, string>());
            }
            else
            {
                try
                {
                    var deserialize = JsonSerializer.Deserialize<Dictionary<string, string>>(r.Result.Value);
                    return new InitializeAccounts(deserialize);
                }
                catch (JsonException)
                {
                    return new InitializeAccounts(new Dictionary<string, string>());
                }
            }
        }).PipeTo(Self);
    }

    protected override void PreRestart(Exception reason, object message)
    {
        base.PreRestart(reason, message);
        Log.Info("AccountStateActor restarting, saving accounts in initialize message");
        var saveAccounts = new InitializeAccounts(_accounts);
        Self.Tell(saveAccounts);
    }

    protected override void PostStop()
    {
        _scope.Dispose();
    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountStateActor>();
    }

    public static Props Props(IServiceProvider sp)
    {
        return Akka.Actor.Props.Create(() => new AccountStateActor(sp));
    }
}

