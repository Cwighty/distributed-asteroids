using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Asteroids.Actors;

public class SignalRActor : ReceiveActor
{
    private HubConnection hubConnection;

    public SignalRActor()
    {

        Receive<string>(async msg =>
        {
            hubConnection = new HubConnectionBuilder()
            .WithUrl("http://asteroids-system:8080/messagehub")
            .Build();
            Log.Info($"Received message: {msg}");
            await hubConnection.StartAsync();

            await hubConnection.SendAsync("SendMessage", msg);
        });
    }

    protected override void PreStart()
    {
    }

    protected ILoggingAdapter Log { get; } = Context.GetLogger();

    protected override void PostStop()
    {
        hubConnection.DisposeAsync();
    }

    public static Props Props()
    {
        var spExtension = DependencyResolver.For(Context.System);
        return spExtension.Props<SignalRActor>();
    }
}
