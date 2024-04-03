using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.UserSession;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Shared.Accounts;

public class AccountEmitterActor : EmittingActor
{
    private IAccountServiceHub hubProxy;

    public AccountEmitterActor() : base(AccountServiceHub.HubUrl)
    {
        Receive<CreateAccountEvent>(c =>
        {
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<IAccountServiceHub>();
                await hubProxy.NotifyAccountCreationEvent(c);
            });
        });

        Receive<LoginEvent>(l =>
        {
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<IAccountServiceHub>();
                await hubProxy.NotifyLoginEvent(l);
            });
        });

        Receive<StartUserSessionEvent>(e =>
        {
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<IAccountServiceHub>();
                await hubProxy.NotifyStartUserSessionEvent(e);
            });
        });
    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountEmitterActor>();
    }
}
