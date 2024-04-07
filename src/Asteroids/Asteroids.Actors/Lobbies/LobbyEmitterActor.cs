using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Asteroids.Shared.Lobbies
{
    public class LobbyEmitterActor : EmittingActor
    {
        ILobbyHub hubProxy;
        public LobbyEmitterActor() : base(LobbyHub.HubUrl)
        {
            TraceableReceive<Returnable<LobbyStateChangedEvent>>((e, activity) => HandleLobbyStateChangedEvent(e, activity));
            TraceableReceive<Returnable<GameStateBroadcast>>((e, activity) => HandleGameStateBroadcast(e, activity));

            Receive<Exception>(e => Log.Error(e, "An error occurred in the LobbyEmitterActor"));
        }

        private void HandleGameStateBroadcast(Returnable<GameStateBroadcast> e, Activity? activity)
        {
            //Log.Info($"Emitting GameStateBroadcast");
            var serialize = JsonSerializer.Serialize(e);
            int sizeInBytes = Encoding.UTF8.GetBytes(serialize).Length;
            // log size of serialized object
            Log.Info($"Serialized GameStateBroadcast size: {sizeInBytes}");
            ExecuteAndPipeToSelf(async () =>
            {
                hubProxy = connection.ServerProxy<ILobbyHub>();
                await hubProxy.NotifyGameStateBroadcast(e.ToTraceable(activity));
            });
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
