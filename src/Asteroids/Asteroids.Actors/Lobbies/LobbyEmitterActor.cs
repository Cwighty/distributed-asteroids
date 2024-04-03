﻿using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies
{
    public class LobbyEmitterActor : EmittingActor
    {
        ILobbiesHub hubProxy;
        public LobbyEmitterActor() : base(LobbiesHub.HubUrl)
        {
            Receive<Returnable<CreateLobbyEvent>>(e => HandleCreateLobbyEvent(e));
            Receive<Returnable<ViewAllLobbiesResponse>>(res => HandleViewAllLobbiesResponse(res));
            Receive<Returnable<InvalidSessionEvent>>(e => HandleInvalidSessionEvent(e));

            TraceableReceive<Returnable<JoinLobbyEvent>>((e, activity) => HandleJoinLobbyEvent(e, activity));
        }

        private void HandleJoinLobbyEvent(Returnable<JoinLobbyEvent> e, Activity? activity)
        {
            Log.Info($"Emitting JoinLobbyEvent");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbiesHub>();
                await hubProxy.NotifyJoinLobbyEvent(e.ToTraceable(activity));
            });
        }

        private void HandleInvalidSessionEvent(Returnable<InvalidSessionEvent> e)
        {
            Log.Info($"Emitting InvalidSessionEvent");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbiesHub>();
                await hubProxy.NotifyInvalidSessionEvent(e);
            });
        }

        private void HandleCreateLobbyEvent(Returnable<CreateLobbyEvent> e)
        {
            Log.Info($"Emitting CreateLobbyEvent");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbiesHub>();
                await hubProxy.NotifyCreateLobbyEvent(e);
            });
        }

        private void HandleViewAllLobbiesResponse(Returnable<ViewAllLobbiesResponse> res)
        {
            Log.Info($"Emitting ViewAllLobbiesResponse");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbiesHub>();
                await hubProxy.NotifyViewAllLobbiesResponse(res);
            });
        }

        protected override void PreStart()
        {
            base.PreStart();
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create<LobbyEmitterActor>();
        }
    }
}
