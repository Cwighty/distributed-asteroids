using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public record MomentumVector(double X, double Y);

public class PlayerState
{
    const int MAX_MOMENTUM = 50;
    const int ACCELERATION = 1;
    const int MAX_WIDTH = 1000;
    const int MAX_HEIGHT = 1000;
    const int TURN_SPEED = 10;

    public IActorRef UserSessionActor { get; set; }
    public string Username { get; set; }
    public int Health { get; set; }
    public int Score { get; set; }
    public bool IsAlive { get => Health > 0; }

    public Dictionary<GameControlMessages.Key, bool> KeyStates { get; set; } = new Dictionary<GameControlMessages.Key, bool>() {
        { GameControlMessages.Key.Up, false },
        { GameControlMessages.Key.Down, false },
        { GameControlMessages.Key.Left, false },
        { GameControlMessages.Key.Right, false }
        };

    public Location Location { get; set; } = new Location(0, 0);
    public Heading Heading { get; set; } = new Heading(0);

    public MomentumVector MomentumVector { get; set; } = new MomentumVector(0, 0);

    public Location CalculateNewPosition(double deltaTime)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // Apply screen wrapping
        newX = (newX >= 0) ? newX % MAX_WIDTH : MAX_WIDTH + (newX % MAX_WIDTH);
        newY = (newY >= 0) ? newY % MAX_HEIGHT : MAX_HEIGHT + (newY % MAX_HEIGHT);

        Location = new Location(newX, newY);
        return Location;
    }

    public Heading CalculateNewHeading(bool isTurningRight, double deltaTime)
    {
        double turnAdjustment = TURN_SPEED * deltaTime * (isTurningRight ? 1 : -1);
        double newAngle = (Heading.Angle + turnAdjustment) % 360;

        if (newAngle < 0) newAngle += 360; // Normalize the angle to be between 0-360 degrees

        return new Heading(newAngle);
    }

    public void ApplyThrust(double deltaTime)
    {
        double angleInRadians = Heading.Angle * (Math.PI / 180);
        var accelerationX = Math.Cos(angleInRadians) * ACCELERATION * deltaTime;
        var accelerationY = Math.Sin(angleInRadians) * ACCELERATION * deltaTime;

        // Update momentum vector based on the direction of thrust
        // This assumes you have a way to track X and Y components of momentum separately
        var momentumX = MomentumVector.X + accelerationX;
        var momentumY = MomentumVector.Y + accelerationY;

        // Optional: Clamp the maximum speed to prevent the ship from going too fast
        var totalMomentum = Math.Sqrt(momentumX * momentumX + momentumY * momentumY);
        if (totalMomentum > MAX_MOMENTUM)
        {
            momentumX = (momentumX / totalMomentum) * MAX_MOMENTUM;
            momentumY = (momentumY / totalMomentum) * MAX_MOMENTUM;
        }

        MomentumVector = new MomentumVector(momentumX, momentumY);
    }

    public override string ToString()
    {
        return UserSessionActor.Path.Name;
    }
}

public class LobbyStateActor : TraceActor, IWithTimers
{
    public record BroadcastStateCommand();
    public record RecoverStateCommand(LobbyState Status, Dictionary<string, PlayerState> Players, string LobbyName, long LobbyId, long Tick, IActorRef LobbyEmitter);

    public ITimerScheduler Timers { get; set; }
    public double TickInterval { get; set; } = .1;

    private long lobbyId;
    private IActorRef lobbyEmitter;
    private bool timerEnabled;
    private string lobbyName;
    private long tick = 0;
    private LobbyState status = LobbyState.Joining;
    private long countdown = 10;

    private Dictionary<string, PlayerState> players = new();
    private int playerCount => players.Count;

    public LobbyStateActor(string lobbyName, long lobbyId, IActorRef lobbyEmitter, bool timerEnabled = true)
    {
        this.lobbyName = lobbyName;
        this.lobbyId = lobbyId;
        this.lobbyEmitter = lobbyEmitter;
        this.timerEnabled = timerEnabled;
        TraceableReceive<JoinLobbyCommand>((cmd, activity) => HandleJoinLobbyCommand(cmd, activity));
        TraceableReceive<LobbyStateQuery>((query, activity) => HandleLobbyStateQuery(query, activity));
        TraceableReceive<StartGameCommand>((cmd, activity) => HandleStartGameCommand(cmd, activity));

        TraceableReceive<SessionScoped<GameControlMessages.UpdateKeyStatesCommand>>((msg, activity) => HandleUpdateKeyStatesCommand(msg, activity));

        TraceableReceive<Returnable<LobbyStateChangedEvent>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));
        TraceableReceive<Returnable<GameStateBroadcast>>((msg, activity) => EmitEvent(msg.ToTraceable(activity)));

        Receive<BroadcastStateCommand>((_) => GameTick());
        Receive<RecoverStateCommand>(cmd => HandleRecoverStateCommand(cmd));
    }

    private void HandleRecoverStateCommand(RecoverStateCommand cmd)
    {
        status = cmd.Status;
        players = cmd.Players;
        tick = cmd.Tick;
        lobbyName = cmd.LobbyName;
        lobbyId = cmd.LobbyId;
        lobbyEmitter = cmd.LobbyEmitter;
    }

    private void HandleUpdateKeyStatesCommand(SessionScoped<GameControlMessages.UpdateKeyStatesCommand> msg, Activity? activity)
    {
        Log.Info($"Received key states from {msg.SessionActorPath}");
        var player = GetPlayer(msg.SessionActorPath.Split("/").Last());
        player.KeyStates = msg.Message.KeyStates;
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
        if (status == LobbyState.Joining)
        {
            status = LobbyState.Countdown;
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand(), Self);

        }
        else
        {
            Log.Warning($"Cannot start game in lobby {lobbyName} with state {status}");
        }
    }

    private void GameTick()
    {
        if (status == LobbyState.Countdown)
        {
            countdown--;
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, new BroadcastStateCommand(), Self);
            if (countdown == 0)
            {
                status = LobbyState.Playing;
                StartBroadcastOnSchedule();
            }
        }

        if (status == LobbyState.Playing)
        {
            Log.Info($"Lobby {lobbyName} is playing: tick {tick}");

            foreach (var kv in players)
            {
                var player = kv.Value;
                // check each key state 
                if (player.KeyStates.TryGetValue(GameControlMessages.Key.Up, out var keyState) && keyState)
                {
                    Log.Info($"Applying thrust to {player}");
                    player.ApplyThrust(1);
                }
                if (player.KeyStates.TryGetValue(GameControlMessages.Key.Left, out keyState) && keyState)
                {
                    player.Heading = player.CalculateNewHeading(false, 1);
                }
                if (player.KeyStates.TryGetValue(GameControlMessages.Key.Right, out keyState) && keyState)
                {
                    player.Heading = player.CalculateNewHeading(true, 1);
                }
            }
        }

        var e = new GameStateBroadcast(GetGameStateOnTick());
        foreach (var kv in players)
            kv.Value.UserSessionActor.Tell(e.ToTraceable(null));

    }

    private void StartBroadcastOnSchedule()
    {
        if (timerEnabled)
            Timers.StartPeriodicTimer(nameof(BroadcastStateCommand), new BroadcastStateCommand(), TimeSpan.FromSeconds(.5), TimeSpan.FromSeconds(TickInterval));
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
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), kv.Value.UserSessionActor, e.ToTraceable(activity), Self);
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
            State = status,
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

    public static Props Props(string lobbyName, long lobbyId, IActorRef lobbyEmitter, bool timerEnabled = true)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId, lobbyEmitter, timerEnabled);
    }
}
