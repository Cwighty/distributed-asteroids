using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Shared.Accounts;

public class AccountEmmitterActor : ReceiveActor
{
    private HubConnection connection;
    private IAccountServiceHub hubProxy;

    public AccountEmmitterActor()
    {

        Receive<string>(async msg =>
        {

            connection = new HubConnectionBuilder()
                .WithUrl(AccountServiceHub.HubUrl)
                .Build();
            hubProxy = connection.ServerProxy<IAccountServiceHub>();
            await connection.StartAsync();
            Log.Info($"Attempting to broadcast to signalr");

            await hubProxy.NotifyAccountCreated(msg);
        });
    }

    protected override void PreStart()
    {
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    protected override void PostStop()
    {
        connection.DisposeAsync();
    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountEmmitterActor>();
    }
}
