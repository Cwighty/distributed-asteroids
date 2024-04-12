using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies
{
    public class LobbiesEmitterActor : EmittingActor
    {
        ILobbiesHub lobbiesHubProxy;
        public LobbiesEmitterActor() : base(LobbiesHub.HubUrl)
        {
            Receive<CreateLobbyEvent>(e => HandleCreateLobbyEvent(e));
            Receive<ViewAllLobbiesResponse>(res => HandleViewAllLobbiesResponse(res));
            Receive<Returnable<InvalidSessionEvent>>(e => HandleInvalidSessionEvent(e));

            TraceableReceive<Returnable<JoinLobbyEvent>>((e, activity) => HandleJoinLobbyEvent(e, activity));
        }

        private void HandleJoinLobbyEvent(Returnable<JoinLobbyEvent> e, Activity? activity)
        {
            ExecuteAndPipeToSelf(async () =>
            {
                lobbiesHubProxy = connection.ServerProxy<ILobbiesHub>();
                await lobbiesHubProxy.NotifyJoinLobbyEvent(e.ToTraceable(activity));
            });
        }

        private void HandleInvalidSessionEvent(Returnable<InvalidSessionEvent> e)
        {
            ExecuteAndPipeToSelf(async () =>
            {
                lobbiesHubProxy = connection.ServerProxy<ILobbiesHub>();
                await lobbiesHubProxy.NotifyInvalidSessionEvent(e);
            });
        }

        private void HandleCreateLobbyEvent(CreateLobbyEvent e)
        {
            ExecuteAndPipeToSelf(async () =>
            {
                lobbiesHubProxy = connection.ServerProxy<ILobbiesHub>();
                await lobbiesHubProxy.NotifyCreateLobbyEvent(e);
            });
        }

        private void HandleViewAllLobbiesResponse(ViewAllLobbiesResponse res)
        {
            ExecuteAndPipeToSelf(async () =>
            {
                lobbiesHubProxy = connection.ServerProxy<ILobbiesHub>();
                await lobbiesHubProxy.NotifyViewAllLobbiesResponse(res);
            });
        }

        protected override void PreStart()
        {
            base.PreStart();
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create<LobbiesEmitterActor>();
        }
    }
}
