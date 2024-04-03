using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies
{
    public class LobbyEmitterActor : EmittingActor
    {
        ILobbyHub hubProxy;
        public LobbyEmitterActor() : base(LobbyHub.HubUrl)
        {
            TraceableReceive<Returnable<LobbyStateChangedEvent>>((e, activity) => HandleLobbyStateChangedEvent(e, activity));

            Receive<Exception> (e => Log.Error(e, "An error occurred in the LobbyEmitterActor"));
        }

        private void HandleLobbyStateChangedEvent(Returnable<LobbyStateChangedEvent> e, Activity? activity)
        {
            Log.Info($"Emitting LobbyStateChangedEvent");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbyHub>();
                await hubProxy.NotifyLobbyStateEvent(e.ToTraceable(activity));
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
