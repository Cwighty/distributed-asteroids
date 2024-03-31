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
    public const string ACCOUNT_KEY = "user-accounts";
    private IActorRef storageActor;

    public Dictionary<Guid, string> Transactions { get; set; } = new(); 

    public Dictionary<string, string> LastKnownAccounts { get; set; } = new();

    public AccountActor()
    {
        Receive<CreateNewAccountCommand>(c =>
        {
            Log.Info("Received CreateNewAccountCommand at AccountActor"); 

            if (LastKnownAccounts.ContainsKey(c.Username))
            {
                var accountExists = new AccountCreatedEvent(c.ConnectionId, false, "Username already taken.");
                var accountEmitter = Context.ActorOf(AccountEmmitterActor.Props());
                accountEmitter.Tell(accountExists);
                return;
            }

            var transactionId = Guid.NewGuid();
            Transactions.Add(transactionId, c.ConnectionId);

            var unmodified = JsonSerializer.Serialize(LastKnownAccounts);
            var modified = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { c.Username, c.Password }
            });

            var command = new CompareAndSwapCommand(transactionId, ACCOUNT_KEY, unmodified, modified);

            storageActor.Tell(command);
        });

        Receive<CompareAndSwapResponse>(c =>
        {
            Log.Info("Received CompareAndSwapResponse at AccountActor");
            Transactions.TryGetValue(c.requestId, out var connectionId);

            try
            {
                var deserialize = JsonSerializer.Deserialize<Dictionary<string, string>>(c.value);
                LastKnownAccounts = deserialize;
            }
            catch
            {
            }

            var created = new AccountCreatedEvent(connectionId, true);
            // tell acount emmitter
            var accountEmitter = Context.ActorOf(AccountEmmitterActor.Props());
            accountEmitter.Tell(created);

            Transactions.Remove(c.requestId);
        });

        Receive<StrongGetResponse>(c =>
        {
            Log.Info("Received StrongGetResponse at AccountActor");
            if (string.IsNullOrWhiteSpace(c.value))
            {
                LastKnownAccounts = new();
                return;
            }
            try
            {
                var deserialize = JsonSerializer.Deserialize<Dictionary<string, string>>(c.value);
                LastKnownAccounts = deserialize ?? new();
            }
            catch { }
        });
    }

    protected override void PreStart()
    {
        storageActor = Context.ActorOf(KeyValueStorageActor.Props());
        var request = new StrongGetQuery(Guid.NewGuid(), ACCOUNT_KEY);
        storageActor.Tell(request);
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountActor>();
    }
}
