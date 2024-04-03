﻿using Asteroids.Shared;
using Asteroids.Shared.Contracts;
using Asteroids.Shared.Lobbies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Client.Components.Pages;
public partial class LobbyPage : ILobbyClient
{ 
    private ILobbyHub hubProxy = default!;
    private HubConnection connection = default!;
    private string? connectionId;

    public GameStateSnapshot? CurrentGameState { get; set; }

    [CascadingParameter(Name = "SessionActor")]
    public string SessionActorPath { get; set; } = string.Empty;


    [Parameter] public long LobbyId { get; set; }
    protected override async Task OnInitializedAsync()
    {
        connection = new HubConnectionBuilder()
            .WithUrl(LobbyHub.HubUrl)
            .WithAutomaticReconnect()
            .Build();
        hubProxy = connection.ServerProxy<ILobbyHub>();
        _ = connection.ClientRegistration<ILobbyClient>(this);
        await connection.StartAsync();
        connectionId = connection.ConnectionId;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (CurrentGameState == null)
        {
            await InitializeLobby();
        }
    }

    public async Task InitializeLobby()
    {
        using var activity = DiagnosticConfig.Source.StartActivity($"{nameof(LobbyPage)}: {nameof(InitializeLobby)}");
        var qry = new LobbyStateQuery(LobbyId).ToSessionableMessage(connectionId!, SessionActorPath);
        await hubProxy.LobbyStateQuery(qry.ToTraceable(activity));
    }

    public async Task OnInvalidSessionEvent(InvalidSessionEvent e)
    {
        await SessionService.ClearSession();
        Navigation.NavigateTo("/");
    }

    public async Task OnLobbyStateChangedEvent(Returnable<LobbyStateChangedEvent> e)
    {
        Console.WriteLine("LobbyStateChangedEvent {0}", e.Message.State);
        CurrentGameState = e.Message.State;
        await InvokeAsync(StateHasChanged);
    }
}
