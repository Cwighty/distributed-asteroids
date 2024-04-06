namespace Asteroids.Shared.GameStateEntities;

public class PlayerParameters
{
    public int MaxMomentum { get; set; } = 100;
    public int Acceleration { get; set; } = 1;
    public int MaxWidth { get; set; } = 800;
    public int MaxHeight { get; set; } = 800;
    public int TurnSpeed { get; set; } = 10;
}
