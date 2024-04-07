namespace Asteroids.Shared.Lobbies;

public record MomentumVector(double X, double Y);

public static class VectorExtensions
{
    public static MomentumVector Rotate(this MomentumVector vector, double degrees)
    {
        double radians = degrees * Math.PI / 180.0; // Convert degrees to radians
        double cosTheta = Math.Cos(radians);
        double sinTheta = Math.Sin(radians);

        double newX = vector.X * cosTheta - vector.Y * sinTheta;
        double newY = vector.X * sinTheta + vector.Y * cosTheta;

        return new MomentumVector(newX, newY);
    }

    public static MomentumVector Scale(this MomentumVector vector, double factor)
    {
        return new MomentumVector(vector.X * factor, vector.Y * factor);
    }

    public static MomentumVector Normalize(this MomentumVector vector)
    {
        double length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        return new MomentumVector(vector.X / length, vector.Y / length);
    }
}