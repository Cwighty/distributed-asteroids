using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public class PlayerState
{
    const int MAX_MOMENTUM = 1000;
    const int ACCELERATION = 1;
    const int MAX_WIDTH = 3000;
    const int MAX_HEIGHT = 3000;
    
    public IActorRef UserSessionActor { get; set; }
    public string Username { get; set; }
    public int Health { get; set; }
    public int Score { get; set; }
    public bool IsAlive { get => Health > 0; }

    public int Momentum { get; set; } = 0;

    public Location Location { get; set; } = new Location(0, 0);
    public Heading Heading { get; set; } = new Heading(0);

    public Location CalculateNewPosition(double deltaTime)
    {
        var newX = Location.X + Momentum * Math.Cos(Heading.Angle) * deltaTime;
        var newY = Location.Y + Momentum * Math.Sin(Heading.Angle) * deltaTime;

        newX = newX % MAX_WIDTH;
        newY = newY % MAX_HEIGHT;
        Location = new Location(newX, newY);
        return Location;
    }
    public Heading CalculateNewHeading(double deltaTime)
    {
        var newAngle = (Momentum * deltaTime);
        return new Heading(newAngle);
    }

    public void IncreaseMomentum(int amount = ACCELERATION)
    {
        if (Momentum + amount > MAX_MOMENTUM)
            Momentum = MAX_MOMENTUM;
        else
            Momentum += amount;
    }
    public void DecreaseMomentum(int amount = ACCELERATION)
    {
        if (Momentum - amount < 0)
            Momentum = 0;
        else
            Momentum -= amount;
    }

    public override string ToString()
    {
        return UserSessionActor.Path.Name;
    }
}

public class LobbyStateActor : TraceActor
{
    public record BroadcastStateCommand();

    private readonly long lobbyId;
    private readonly IActorRef lobbyEmitter;
    private readonly string lobbyName;
    private long tick = 0;
    private LobbyState state = LobbyState.Joining;
    private long countdown = 10;

    private Dictionary<string, PlayerState> players = new();
    private int playerCount => players.Count;

    public LobbyStateActor(string lobbyName, long lobbyId, IActorRef lobbyEmitter)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;
        this.lobbyEmitter = lobbyEmitter;
        TraceableReceive<JoinLobbyCommand>((cmd, activity) => HandleJoinLobbyCommand(cmd, activity));
        TraceableReceive<LobbyStateQuery>((query, activity) => HandleLobbyStateQuery(query, activity));
        TraceableReceive<StartGameCommand>((cmd, activity) => HandleStartGameCommand(cmd, activity));
        TraceableReceive<BroadcastStateCommand>((_, activity) => BroadcastState(activity));

        TraceableReceive<SessionScoped<GameControlMessages.KeyDownCommand>>((msg, activity) => HandleKeyDownEvent(msg, activity));
        TraceableReceive<SessionScoped<GameControlMessages.KeyUpCommand>>((msg, activity) => HandleKeyUpEvent(msg, activity));

        TraceableReceive<Returnable<LobbyStateChangedEvent>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));
        TraceableReceive<Returnable<GameStateBroadcast>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));
    }

    private void HandleKeyDownEvent(SessionScoped<GameControlMessages.KeyDownCommand> msg, Activity? activity)
    {
        var player = GetPlayer(msg.SessionActorPath.Split("/").Last());

        switch (msg.Message.Key)
        {
            case GameControlMessages.Key.Up:
                player.IncreaseMomentum();
                break;
            case GameControlMessages.Key.Down:
                player.DecreaseMomentum();
                break;
            case GameControlMessages.Key.Left:
                player.Heading = player.CalculateNewHeading(1);
                break;
            case GameControlMessages.Key.Right:
                player.Heading = player.CalculateNewHeading(1);
                break;
        }
    }

    private void HandleKeyUpEvent(SessionScoped<GameControlMessages.KeyUpCommand> msg, Activity? activity)
    {
        throw new NotImplementedException();
    }

    private PlayerState GetPlayer(string playerName)
    {
        if (players.TryGetValue(playerName, out var player))
            return player;
        else
            throw new KeyNotFoundException($"Player {playerName} not found in lobby {lobbyName}");
    }

    private void HandleStartGameCommand(StartGameCommand cmd, Activity? activity)
    {
        if (state == LobbyState.Joining)
        {
            state = LobbyState.Countdown;
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand().ToTraceable(activity), Self);

        }
        else
        {
            Log.Warning($"Cannot start game in lobby {lobbyName} with state {state}");
        }
    }

    private void BroadcastState(Activity? activity)
    {
        var e = new GameStateBroadcast(GetGameStateOnTick());
        foreach (var kv in players)
            kv.Value.UserSessionActor.Tell(e.ToTraceable(activity));

        //Log.Info($"Broadcasted state of lobby {lobbyName}");
        if (state == LobbyState.Countdown)
        {
            countdown--;
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand().ToTraceable(null), Self);
            if (countdown == 0)
            {
                state = LobbyState.Playing;
                Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100), Self, new BroadcastStateCommand().ToTraceable(null), Self);
            }
        }

        if (state == LobbyState.Playing)
        {
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromMilliseconds(100), Self, new BroadcastStateCommand().ToTraceable(null), Self);
        }
    }

    private void HandleLobbyStateQuery(LobbyStateQuery query, Activity? activity)
    {
        var state = GetGameStateOnTick();
        var e = new LobbyStateChangedEvent(state);
        Sender.Tell(e.ToTraceable(activity));
    }

    private void HandleJoinLobbyCommand(JoinLobbyCommand cmd, Activity? activity)
    {
        var userSessionActor = Sender;
        if (players.ContainsKey(userSessionActor.Path.Name))
        {
            Log.Info($"Player {userSessionActor.Path.Name} is already in lobby {lobbyName}");
            userSessionActor.Tell(new JoinLobbyEvent(GetGameStateOnTick()).ToTraceable(activity));
            return;
        }

        var player = new PlayerState
        {
            UserSessionActor = userSessionActor,
            Username = cmd.UserActorPath.Split("_").Last(),
            Health = 100,
            Score = 0,
            Location = new Location(0, 0),
            Heading = new Heading(0)
        };

        players.Add(userSessionActor.Path.Name, player);
        Log.Info($"Player {userSessionActor.Path.Name} joined lobby {lobbyName}");

        userSessionActor.Tell(new JoinLobbyEvent(GetGameStateOnTick()).ToTraceable(activity));

        var e = new LobbyStateChangedEvent(GetGameStateOnTick());
        foreach (var kv in players)
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(2), kv.Value.UserSessionActor, e.ToTraceable(activity), Self);
    }

    private void EmitEvent<T>(Traceable<Returnable<T>> returnable)
    {
        Log.Info($"Lobby {lobbyName} received {returnable.Message.Message.GetType().Name}");
        lobbyEmitter.Forward(returnable);
    }

    public GameStateSnapshot GetGameStateOnTick()
    {
        return new GameStateSnapshot(new LobbyInfo(lobbyId, lobbyName, playerCount))
        {
            State = state,
            Tick = tick++,
            CountDown = countdown,
            Players = players.Values.Select(x => CalcualtePlayerStateOnTick(x)).ToArray()
        };
    }

    public PlayerStateSnapshot CalcualtePlayerStateOnTick(PlayerState state)
    {
        return new PlayerStateSnapshot() with
        {
            Heading = state.Heading,
            Health = state.Health,
            Location = state.CalculateNewPosition(1),
            Name = state.Username,
            IsAlive = state.IsAlive
        };
    }

    public PlayerStateSnapshot GetPlayerState(string playerName)
    {
        return new PlayerStateSnapshot(playerName);
    }

    public static Props Props(string lobbyName, long lobbyId, IActorRef lobbyEmitter)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId, lobbyEmitter);
    }
}
