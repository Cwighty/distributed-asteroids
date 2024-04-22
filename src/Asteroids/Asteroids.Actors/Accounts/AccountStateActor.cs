using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Asteroids.Shared.Accounts;


public record CurrentAccountsQuery(Guid RequestId);
public record CurrentAccountsResult(Guid RequestId, Dictionary<string, string> Accounts);

public record CommitAccountCommand(Guid RequestId, string Username, Password Password);
public record AccountCommittedEvent(CommitAccountCommand OriginalCommand, string hashedPassword, bool Success, string Error);

//public record SubscribeToAccountChanges(IActorRef Subscriber);

public class AccountStateActor : TraceActor
{
    public record InitializeAccounts(Dictionary<string, string> Accounts);

    IStorageService storageService;
    const string ACCOUNT_KEY = "user-accounts";
    private readonly int keySize;
    private readonly int iterations;
    private readonly HashAlgorithmName hashAlgorithm;
    private Dictionary<string, string> _accounts = new();

    private Dictionary<Guid, IActorRef> _commitRequests = new();

    public AccountStateActor(int keySize, int iterations, HashAlgorithmName hashAlgorithm, IServiceProvider sp)
    {
        storageService = sp.GetRequiredService<IStorageService>();

        Receive<InitializeAccounts>(cmd => HandleInitializeAccounts(cmd));
        Receive<CurrentAccountsQuery>(cmd => HandleCurrentAccountQuery(cmd));
        Receive<CommitAccountCommand>(response => HandleCommitAccountCommand(response));
        TraceableReceive<LoginCommand>((cmd, activity) => HandleLoginCommand(cmd, activity));
        Receive<AccountCommittedEvent>(e => HandleAccountCommittedEvent(e));
        this.keySize = keySize;
        this.iterations = iterations;
        this.hashAlgorithm = hashAlgorithm;
    }

    private void HandleLoginCommand(LoginCommand cmd, Activity? activity)
    {
        if (_accounts?.ContainsKey(cmd.Username) ?? false)
        {
            // create a salt based on username
            byte[] salt = Encoding.UTF8.GetBytes(cmd.Username);

            // encrypt password
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(cmd.Password.ToString()), salt, iterations, hashAlgorithm, keySize);
            var hashedPassword = Convert.ToHexString(hash);

            if (_accounts[cmd.Username] == hashedPassword)
            {
                Sender.Tell(new LoginEvent(cmd, true));
                return;
            }
        }
        Sender.Tell(new LoginEvent(cmd, false, "Invalid username or password"));
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
            Sender.Tell(new AccountCommittedEvent(command, null, false, "Username already exists"));
            _commitRequests.Remove(command.RequestId);
            return;
        }
        if (string.IsNullOrEmpty(command.Username) || command.Password == null || command.Password.Length == 0)
        {
            Sender.Tell(new AccountCommittedEvent(command, null, false, "Username or password cannot be empty"));
            _commitRequests.Remove(command.RequestId);
            return;
        }
        if (command.Username.Length < 3 || command.Username.Length > 20)
        {
            Sender.Tell(new AccountCommittedEvent(command, null, false, "Username must be between 3 and 20 characters"));
            _commitRequests.Remove(command.RequestId);
            return;
        }
        if (command.Password.Length < 6 || command.Password.Length > 20)
        {
            Sender.Tell(new AccountCommittedEvent(command, null, false, "Password must be between 6 and 20 characters"));
            _commitRequests.Remove(command.RequestId);
            return;
        }

        // create a salt based on username
        byte[] salt = Encoding.UTF8.GetBytes(command.Username);

        // encrypt password
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(command.Password.ToString()), salt, iterations, hashAlgorithm, keySize);
        var hashedPassword = Convert.ToHexString(hash);

        // commit
        var unmodified = JsonSerializer.Serialize(_accounts ?? new Dictionary<string, string>());
        var reducer = (string oldValue) =>
        {
            var oldAccounts = JsonSerializer.Deserialize<Dictionary<string, string>>(oldValue);
            var modifiedAccounts = oldAccounts?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>();
            modifiedAccounts.Add(command.Username, hashedPassword);
            return JsonSerializer.Serialize(modifiedAccounts);
        };
        var task = storageService.IdempodentReduceUntilSuccess(ACCOUNT_KEY, unmodified, reducer);

        task.ContinueWith(r =>
        {
            if (r.IsFaulted)
            {
                Log.Error(r.Exception, "Failed to commit account");
                return new AccountCommittedEvent(command, hashedPassword, false, "Unable to commit account");
            }
            else
            {
                return new AccountCommittedEvent(command, hashedPassword, true, "Account created successfully.");
            }
        }).PipeTo(Self);
    }

    private void HandleAccountCommittedEvent(AccountCommittedEvent e)
    {
        // notify original requestor
        if (e.Success)
        {
            _accounts.Add(e.OriginalCommand.Username, e.hashedPassword);
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
        else
        {
            _commitRequests[e.OriginalCommand.RequestId].Tell(e);
            _commitRequests.Remove(e.OriginalCommand.RequestId);
        }
    }


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


    public static Props Props(HashAlgorithmName hashAlgorithm, int keySize = 64, int iterations = 300_000)
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountStateActor>(keySize, iterations, hashAlgorithm);
    }

    public static Props Props(HashAlgorithmName hashAlgorithm, int keySize, int iterations, IServiceProvider sp)
    {
        return Akka.Actor.Props.Create(() => new AccountStateActor(keySize, iterations, hashAlgorithm, sp));
    }
}

