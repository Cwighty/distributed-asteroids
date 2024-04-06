using Akka.Actor;
using Akka.Event;
using Asteroids.Shared.Contracts;
using System.Diagnostics;

namespace Asteroids.Shared.Lobbies;

public record MomentumVector(double X, double Y);

public class MovementParameters
{
    public int MaxMomentum { get; set; } = 100;
    public int Acceleration { get; set; } = 1;
    public int MaxWidth { get; set; } = 800;
    public int MaxHeight { get; set; } = 800;
    public int TurnSpeed { get; set; } = 10;
}

public class PlayerState
{
    public PlayerState()
    {
        MovementParameters = new MovementParameters();
    }
    public PlayerState(MovementParameters movementParameters)
    {
        MovementParameters = movementParameters;
    }

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
    public MovementParameters MovementParameters { get; }

    public Location CalculateNewPosition(double deltaTime)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // Apply screen wrapping
        newX = (newX >= 0) ? newX % MovementParameters.MaxWidth : MovementParameters.MaxWidth + (newX % MovementParameters.MaxWidth);
        newY = (newY >= 0) ? newY % MovementParameters.MaxHeight : MovementParameters.MaxHeight + (newY % MovementParameters.MaxHeight);

        Location = new Location(newX, newY);
        return Location;
    }

    public Heading CalculateNewHeading(bool isTurningRight, double deltaTime)
    {
        double turnAdjustment = MovementParameters.TurnSpeed * deltaTime * (isTurningRight ? 1 : -1);
        double newAngle = (Heading.Angle + turnAdjustment) % 360;

        if (newAngle < 0) newAngle += 360;

        return new Heading(newAngle);
    }

    public MomentumVector ApplyThrust(double deltaTime)
    {
        double angleInRadians = Heading.Angle * (Math.PI / 180);
        var accelerationX = Math.Cos(angleInRadians) * MovementParameters.Acceleration * deltaTime;
        var accelerationY = Math.Sin(angleInRadians) * MovementParameters.Acceleration * deltaTime;

        // Update momentum vector based on the direction of thrust
        var momentumX = MomentumVector.X + accelerationX;
        var momentumY = MomentumVector.Y + accelerationY;

        // Clamp the maximum speed to prevent the ship from going too fast
        var totalMomentum = Math.Sqrt(momentumX * momentumX + momentumY * momentumY);
        if (totalMomentum > MovementParameters.MaxMomentum)
        {
            momentumX = (momentumX / totalMomentum) * MovementParameters.MaxMomentum;
            momentumY = (momentumY / totalMomentum) * MovementParameters.MaxMomentum;
        }

        MomentumVector = new MomentumVector(momentumX, momentumY);
        return MomentumVector;
    }

    public override string ToString()
    {
        return UserSessionActor.Path.Name;
    }
}


public class AsteroidParams
{
    public int MaxHeight { get; set; } = 800;
    public int MaxWidth { get; set; } = 800;
    public double MaxSpeed { get; set; } = 1;
    public double MaxRotation { get; set; } = 1;
    public double MinSize { get; set; } = 100;
}
public record AsteroidState
{
    private readonly AsteroidParams asteroidParams;

    public AsteroidState() { asteroidParams = new AsteroidParams(); }
    public AsteroidState(AsteroidParams asteroidParams)
    {
        this.asteroidParams = asteroidParams;
    }
    public required Location Location { get; set; }
    public required Heading Heading { get; set; }
    public required MomentumVector MomentumVector { get; set; }
    public double Size { get; set; } = 100;
    public double Rotation { get; set; } = 2;
    public bool IsAlive { get => Size > 3; }

    public List<AsteroidState> BreakInTwo()
    {
        var currentAsteroid = new AsteroidState
        {
            Location = Location,
            Heading = new Heading(Heading.Angle + 50),
            Size = Size / 2,
            Rotation = Rotation,
            MomentumVector = new MomentumVector(-MomentumVector.X / 2, -MomentumVector.Y / 2),
        };

        var newAsteroid = new AsteroidState
        {
            Location = Location,
            Heading = new Heading(Heading.Angle - 50),
            Size = Size / 2,
            Rotation = -Rotation,
            MomentumVector = new MomentumVector(-MomentumVector.X / 2, -MomentumVector.Y / 2),
        };

        return new List<AsteroidState> { currentAsteroid, newAsteroid };
    }

    public Location CalculateNewPosition(double deltaTime)
    {
        var newX = Location.X + MomentumVector.X * deltaTime;
        var newY = Location.Y + MomentumVector.Y * deltaTime;

        // Apply screen wrapping
        newX = (newX >= 0) ? newX % asteroidParams.MaxWidth : asteroidParams.MaxWidth + (newX % asteroidParams.MaxWidth);
        newY = (newY >= 0) ? newY % asteroidParams.MaxHeight : asteroidParams.MaxHeight + (newY % asteroidParams.MaxHeight);

        Location = new Location(newX, newY);
        return Location;
    }

    public Heading CalculateNewRotation(double deltaTime)
    {
        var newAngle = Heading.Angle + Rotation * deltaTime;
        if (newAngle < 0) newAngle += 360;

        return new Heading(newAngle);
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
    private long countdown = 3;

    private Dictionary<string, PlayerState> players = new();
    private List<AsteroidState> asteroids = new();

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
            UpdatePlayerKeyStates();
            CheckForCollisions();
            RandomlySpawnAsteroid();
        }

        var e = new GameStateBroadcast(GetGameSnapShot());
        foreach (var kv in players)
            kv.Value.UserSessionActor.Tell(e.ToTraceable(null));
    }

    private void CheckForCollisions()
    {
        var newAsteroids = new List<AsteroidState>();
        foreach (var asteroid in asteroids)
        {
            bool collided = false;
            foreach (var otherAsteroid in asteroids)
            {
                if (asteroid == otherAsteroid) continue;
                var distance = Math.Pow(asteroid.Location.X - otherAsteroid.Location.X, 2) + Math.Pow(asteroid.Location.Y - otherAsteroid.Location.Y, 2);
                if (distance < Math.Pow(asteroid.Size + otherAsteroid.Size, 2))
                {
                    Log.Info($"Asteroid {asteroid} collided with asteroid {otherAsteroid}");
                    newAsteroids.AddRange(asteroid.BreakInTwo());
                    collided = true;
                }
            }
            foreach (var kv in players)
            {
                var player = kv.Value;
                var distance = Math.Pow(asteroid.Location.X - player.Location.X, 2) + Math.Pow(asteroid.Location.Y - player.Location.Y, 2);
                if (distance < Math.Pow(asteroid.Size, 2))
                {
                    Log.Info($"Player {player} collided with asteroid");
                    player.Health = 0;
                    newAsteroids.AddRange(asteroid.BreakInTwo());
                    collided = true;
                }
            }
            if (!collided)
            {
                newAsteroids.Add(asteroid);
            }
        }
    }

    private void RandomlySpawnAsteroid()
    {
        if (asteroids.Count > 5) return;
        if (new Random().NextDouble() < 0.35)
        {
            Log.Info("Spawning new asteroid");
            var asteroid = new AsteroidState
            {
                Size = new Random().NextDouble() * 200,
                Location = new Location(
                    X: new Random().NextDouble() * 1000,
                    Y: new Random().NextDouble() * 1000),
                Heading = new Heading(new Random().NextDouble() * 360),
                MomentumVector = new MomentumVector(new Random().NextDouble() * 10, new Random().NextDouble() * 10),
            };
            asteroids.Add(asteroid);
        }
    }

    private void UpdatePlayerKeyStates()
    {
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

    private void StartBroadcastOnSchedule()
    {
        if (timerEnabled)
            Timers.StartPeriodicTimer(nameof(BroadcastStateCommand), new BroadcastStateCommand(), TimeSpan.FromSeconds(.5), TimeSpan.FromSeconds(TickInterval));
    }

    private void HandleLobbyStateQuery(LobbyStateQuery query, Activity? activity)
    {
        var state = GetGameSnapShot();
        var e = new LobbyStateChangedEvent(state);
        Sender.Tell(e.ToTraceable(activity));
    }

    private void HandleJoinLobbyCommand(JoinLobbyCommand cmd, Activity? activity)
    {
        var userSessionActor = Sender;
        if (players.ContainsKey(userSessionActor.Path.Name))
        {
            Log.Info($"Player {userSessionActor.Path.Name} is already in lobby {lobbyName}");
            userSessionActor.Tell(new JoinLobbyEvent(GetGameSnapShot()).ToTraceable(activity));
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

        userSessionActor.Tell(new JoinLobbyEvent(GetGameSnapShot()).ToTraceable(activity));

        var e = new LobbyStateChangedEvent(GetGameSnapShot());
        foreach (var kv in players)
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), kv.Value.UserSessionActor, e.ToTraceable(activity), Self);
    }

    private void EmitEvent<T>(Traceable<Returnable<T>> returnable)
    {
        Log.Info($"Lobby {lobbyName} received {returnable.Message.Message.GetType().Name}");
        lobbyEmitter.Forward(returnable);
    }

    public GameStateSnapshot GetGameSnapShot()
    {
        return new GameStateSnapshot(new LobbyInfo(lobbyId, lobbyName, playerCount))
        {
            State = status,
            Tick = tick++,
            CountDown = countdown,
            Players = players.Values.Where(x => x.IsAlive).Select(x => CalcualtePlayerStateOnTick(x)).ToList(),
            Asteroids = asteroids.Where(x => x.IsAlive).Select(x => CalculateAsteroidStateOnTick(x)).ToList()
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

    public AsteroidSnapshot CalculateAsteroidStateOnTick(AsteroidState asteroidState)
    {
        return new AsteroidSnapshot
        {
            Location = asteroidState.CalculateNewPosition(1),
            Heading = asteroidState.Heading,
            Size = asteroidState.Size,
            IsAlive = asteroidState.IsAlive,
        };
    }


    public static Props Props(string lobbyName, long lobbyId, IActorRef lobbyEmitter, bool timerEnabled = true)
    {
        return Akka.Actor.Props.Create<LobbyStateActor>(lobbyName, lobbyId, lobbyEmitter, timerEnabled);
    }
}
