using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Lobbies;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Shared.Contracts;

public abstract class EmittingActor : TraceActor
{
    internal HubConnection connection;
    private readonly string hubUrl;

    public EmittingActor(string hubUrl)
    {
        this.hubUrl = hubUrl;
    }

    protected override void PreStart()
    {
        base.PreStart();
        EstablishConnection()
            .PipeTo(Self, failure: ex => ex);
    }

    private async Task EstablishConnection()
    {
        connection = new HubConnectionBuilder()
        .WithUrl(hubUrl)
        .Build();

        try
        {
            await connection.StartAsync();
            Log.Info("SignalR connection established.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to establish SignalR connection: {ex.Message}");
            // Schedule a retry or implement a strategy to handle the connection failure
        }

        connection.Closed += async (error) =>
        {
            Log.Warning("Connection closed. Trying to reconnect...");
            await Task.Delay(new Random().Next(0, 5) * 1000);
            await EstablishConnection();
        };
    }

    private async Task EnsureConnectedAndExecute(Func<Task> action)
    {
        if (connection.State != HubConnectionState.Connected)
        {
            await EstablishConnection();
        }
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log.Error($"Error executing action: {ex.Message}");
        }
    }

    internal void ExecuteAndPipeToSelf(Func<Task> action)
    {
        EnsureConnectedAndExecute(async () =>
        {
            await action();
        }).PipeTo(Self, failure: ex => ex);
    }

    protected override void PostStop()
    {
        base.PostStop();
        connection?.DisposeAsync();
        Log.Info("SignalR connection disposed.");
    }
    protected ILoggingAdapter Log { get; } = Context.GetLogger();
}
