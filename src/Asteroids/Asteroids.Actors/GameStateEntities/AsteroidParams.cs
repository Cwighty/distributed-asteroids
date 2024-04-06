namespace Asteroids.Shared.GameStateEntities;

public class AsteroidParams
{
    public int MaxHeight { get; set; } = 800;
    public int MaxWidth { get; set; } = 800;
    public double MaxSpeed { get; set; } = 1;
    public double MaxRotation { get; set; } = 1;
    public double MinSize { get; set; } = 40;
}
