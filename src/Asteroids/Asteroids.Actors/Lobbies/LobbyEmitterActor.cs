using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Asteroids.Shared.Lobbies
{
    public class LobbyEmitterActor : EmittingActor
    {
        ILobbyHub hubProxy;
        public LobbyEmitterActor() : base(LobbyHub.HubUrl)
        {
            Receive<Returnable<CreateLobbyEvent>>(e => HandleCreateLobbyEvent(e));
            Receive<Returnable<ViewAllLobbiesResponse>>(res => HandleViewAllLobbiesResponse(res));
            Receive<Returnable<InvalidSessionEvent>>(e => HandleInvalidSessionEvent(e));
        }

        private void HandleInvalidSessionEvent(Returnable<InvalidSessionEvent> e)
        {
            Log.Info($"Emitting InvalidSessionEvent");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbyHub>();
                await hubProxy.NotifyInvalidSessionEvent(e);
            });
        }

        private void HandleCreateLobbyEvent(Returnable<CreateLobbyEvent> e)
        {
            Log.Info($"Emitting CreateLobbyEvent");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbyHub>();
                await hubProxy.NotifyCreateLobbyEvent(e);
            });
        }

        private void HandleViewAllLobbiesResponse(Returnable<ViewAllLobbiesResponse> res)
        {
            Log.Info($"Emitting ViewAllLobbiesResponse");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbyHub>();
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
