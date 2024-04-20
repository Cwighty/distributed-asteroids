using Asteroids.Shared.Lobbies;
using Serilog;

namespace Asteroids.Shared.GameStateEntities;

public class GameState
{
    public GameState()
    {
        GameParameters = GameParameters.Default;
        Countdown = GameParameters.CountdownSeconds;
    }

    public GameState(GameParameters gameParams)
    {
        GameParameters = gameParams;
        Countdown = gameParams.CountdownSeconds;
    }

    public GameParameters GameParameters { get; init; }

    private GameStatus _status = GameStatus.Joining;
    public GameStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            StatusChanged?.Invoke(this, value);
        }
    }

    public Dictionary<string, PlayerState> Players { get; set; } = new();
    public int PlayerCount => Players.Count;
    public List<AsteroidState> Asteroids { get; set; } = new();
    public List<BulletState> Bullets { get; set; } = new();

    public int Countdown { get; set; } = 3;
    public long TickCount { get; set; } = 0;

    public LobbyInfo Lobby { get; set; }

    public event EventHandler<GameStatus> StatusChanged;

    public void Tick()
    {
        if (Status == GameStatus.Joining) throw new InvalidOperationException("Game must be started before ticking");
        TickCount++;
        if (Status == GameStatus.Countdown)
        {
            Countdown--;
            if (Countdown == 0)
            {
                Status = GameStatus.Playing;
            }
        }
        if (Status == GameStatus.Playing)
        {
            PerformPlayerKeyActions();
            RandomlySpawnAsteroid();
            MovePlayers();
            MoveAsteroids();
            MoveBullets();
            CheckForCollisions();
            FilterDeadAsteroids();
            CheckForEndGame();
        }
    }

    private void CheckForEndGame()
    {
        if (Players.All(x => !x.Value.IsAlive))
        {
            Status = GameStatus.GameOver;
        }
    }

    private void FilterDeadAsteroids()
    {
        Asteroids = Asteroids.Where(x => x.IsAlive).ToList();
    }

    private void MoveAsteroids()
    {
        foreach (var asteroid in Asteroids)
        {
            asteroid.MoveToNextPosition(GameParameters);
            asteroid.Rotate();
        }
    }

    private void MovePlayers()
    {
        foreach (var player in Players.Values) player.MoveToNextPosition(GameParameters);
    }

    private void MoveBullets()
    {
        foreach (var bullet in Bullets) bullet.MoveToNextPosition(GameParameters);
    }

    public void JoinPlayer(PlayerState player)
    {
        Players.Add(player.UserSessionActorPath.Split("/").Last(), player);
    }

    public PlayerState GetPlayer(string playerName)
    {
        if (Players.TryGetValue(playerName, out var player))
            return player;
        else
            throw new KeyNotFoundException($"Player {playerName} not found");
    }


    public void StartGame()
    {
        if (Status != GameStatus.Joining) throw new InvalidOperationException("Game can only start when joining");
        if (Players.Count == 0) throw new InvalidOperationException("Game must have at least one player");
        Status = GameStatus.Countdown;
    }

    private void CheckForCollisions()
    {
        var newAsteroids = new List<AsteroidState>();
        var newBullets = new List<BulletState>(Bullets);

        if (Asteroids.Count == 0) return;

        foreach (var asteroid in Asteroids.Where(x => x.IsAlive))
        {
            bool asteroidCollided = false;
            bool asteroidImmuneFromOtherCollisions = false;
            if (asteroid.ImmunityTicks > 0)
            {
                asteroid.ImmunityTicks--;
                asteroidImmuneFromOtherCollisions = true;
            }

            if (!asteroidImmuneFromOtherCollisions) //prevent exponential splits
            {
                foreach (var otherAsteroid in Asteroids.Where(x => x.IsAlive))
                {
                    if (asteroid == otherAsteroid) continue;
                    if (asteroid.CollidedWith(otherAsteroid))
                    {
                        newAsteroids.Add(asteroid.Collide());
                        asteroidCollided = true;
                    }
                }
            }

            foreach (var bullet in Bullets)
            {
                if (asteroid.CollidedWith(bullet))
                {
                    newAsteroids.AddRange(asteroid.BreakInTwo(GameParameters));
                    newBullets.Remove(bullet);
                    asteroidCollided = true;
                }
            }

            foreach (var kv in Players.Where(x => x.Value.IsAlive))
            {
                var player = kv.Value;
                if (asteroid.CollidedWith(player))
                {
                    player.Damage((int)(GameParameters.AsteroidDamageScale * asteroid.Size));
                    newAsteroids.Add(asteroid.Collide());
                    asteroidCollided = true;
                }
            }

            if (!asteroidCollided)
            {
                newAsteroids.Add(asteroid);
            }
        }

        Bullets = newBullets.ToList();
        Asteroids = newAsteroids.Where(x => x.IsAlive).ToList();
    }

    private void RandomlySpawnAsteroid()
    {
        if (Asteroids.Count >= GameParameters.MaxAsteroids) return;
        if (new Random().NextDouble() < GameParameters.AsteroidSpawnRate)
        {
            var asteroid = new AsteroidState(GameParameters.AsteroidParameters)
            {
                Id = Guid.NewGuid(),
                Size = new Random().NextDouble() * GameParameters.MaxAsteroidSize,
                Location = GetRandomEdgeLocation(),
                Heading = new Heading(new Random().NextDouble() * 360),
                Rotation = new Random().NextDouble() * GameParameters.AsteroidParameters.MaxRotation,
                MomentumVector = new MomentumVector(new Random().NextDouble() * 10, new Random().NextDouble() * 10),
            };

            Asteroids.Add(asteroid);
        }
    }

    private void PerformPlayerKeyActions()
    {
        foreach (var kv in Players)
        {
            var player = kv.Value;
            // check each key state 
            if (player.KeyStates.TryGetValue(GameControlMessages.Key.Up, out var keyState) && keyState)
            {
                player.ApplyThrust();
            }
            if (player.KeyStates.TryGetValue(GameControlMessages.Key.Left, out keyState) && keyState)
            {
                player.RotateLeft();
            }
            if (player.KeyStates.TryGetValue(GameControlMessages.Key.Right, out keyState) && keyState)
            {
                player.RotateRight();
            }
            if (player.KeyStates.TryGetValue(GameControlMessages.Key.Space, out keyState) && keyState)
            {
                if (Bullets.Count > GameParameters.MaxBullets)
                {
                    Bullets.RemoveAt(0);
                }
                Bullets.Add(player.Shoot(GameParameters));
            }
        }
    }

    private Location GetRandomEdgeLocation()
    {
        var random = new Random();
        var edge = random.Next(4);
        double x, y;

        switch (edge)
        {
            case 0: // Top edge
                x = random.NextDouble() * 1000;
                y = 0;
                break;
            case 1: // Right edge
                x = 1000;
                y = random.NextDouble() * 1000;
                break;
            case 2: // Bottom edge
                x = random.NextDouble() * 1000;
                y = 1000;
                break;
            case 3: // Left edge
                x = 0;
                y = random.NextDouble() * 1000;
                break;
            default:
                throw new InvalidOperationException("Invalid edge value");
        }

        return new Location(x, y);
    }
}

public record GameStateSnapshot
{
    public GameStateSnapshot() { }
    public GameStateSnapshot(LobbyInfo lobby)
    {
        Lobby = lobby;
    }

    public long Tick { get; init; } = -1;
    public long Countdown { get; init; } = 10;
    public LobbyInfo Lobby { get; init; }
    public GameStatus Status { get; init; } = GameStatus.Joining;
    public List<PlayerStateSnapshot> Players { get; init; } = new List<PlayerStateSnapshot>();
    public List<AsteroidSnapshot> Asteroids { get; init; } = new List<AsteroidSnapshot>();
    public List<BulletSnapshot> Bullets { get; init; } = new List<BulletSnapshot>();
}


public static class GameStateExtensions
{
    public static GameStateSnapshot ToSnapshot(this GameState state)
    {
        return new GameStateSnapshot
        {
            Tick = state.TickCount,
            Status = state.Status,
            Players = state.Players.Values.Select(p => p.ToSnapshot()).ToList(),
            Asteroids = state.Asteroids.Select(a => a.ToSnapshot()).ToList(),
            Bullets = state.Bullets.Select(b => b.ToSnapshot()).ToList(),
            Countdown = state.Countdown,
            Lobby = state.Lobby with { PlayerCount = state.PlayerCount }
        };
    }
}
