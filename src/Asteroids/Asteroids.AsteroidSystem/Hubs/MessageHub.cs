using Asteroids.Shared.Actors;
using Microsoft.AspNetCore.SignalR;

namespace Asteroids.AsteroidSystem.Hubs;
public class MessageHub : Hub
{
    private readonly IActorBridge actorBridge;

    public MessageHub(IActorBridge actorBridge)
    {
        this.actorBridge = actorBridge;
    }

    public void Tell(NewMessage message)
    {
        actorBridge.Tell(message);
    }

    public async Task SendMessage(string message)
    {
        Console.WriteLine($"Received message at hub, broadcasting: {message}");
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
