using Akka.Actor;
using Akka.Hosting;
using Asteroids.Shared.Actors;
using Microsoft.AspNetCore.SignalR;

namespace Asteroids.AsteroidSystem.Hubs;
public class MessageHub : Hub
{
    private readonly IActorRef messageActor;

    public MessageHub(ActorRegistry actorRegistry)
    {
        messageActor = actorRegistry.Get<MessageActor>();
    }

    public void Tell(NewMessage message)
    {
        messageActor.Tell(message);
    }

    public async Task SendMessage(string message)
    {
        Console.WriteLine($"Received message at hub, broadcasting: {message}");
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
