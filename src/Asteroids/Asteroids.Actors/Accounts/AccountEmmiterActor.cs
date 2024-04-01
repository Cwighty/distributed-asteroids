﻿using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Shared.Accounts;

public class AccountEmitterActor : ReceiveActor
{
    private HubConnection connection;
    private IAccountServiceHub hubProxy;

    public AccountEmitterActor()
    {
        Receive<CreateAccountEvent>(async c =>
        {
            connection = new HubConnectionBuilder()
                .WithUrl(AccountServiceHub.HubUrl)
                .Build();
            hubProxy = connection.ServerProxy<IAccountServiceHub>();
            await connection.StartAsync();

            await hubProxy.NotifyAccountCreationEvent(c);
        });

        Receive<LoginEvent>(async l =>
        {
            connection = new HubConnectionBuilder()
                .WithUrl(AccountServiceHub.HubUrl)
                .Build();
            hubProxy = connection.ServerProxy<IAccountServiceHub>();
            await connection.StartAsync();

            await hubProxy.NotifyLoginEvent(l);
        });
    }

    protected override void PreStart()
    {
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    protected override void PostStop()
    {
        connection?.DisposeAsync();
    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<AccountEmitterActor>();
    }
}
