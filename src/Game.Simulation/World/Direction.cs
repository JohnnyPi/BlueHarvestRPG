namespace Game.Simulation.World;

public enum Direction : byte
{
    North,
    East,
    South,
    West
}

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            Direction.West => Direction.East,
            _ => direction
        };
    }
}

public static class DirectionResolver
{
    public static bool TryFromDelta(int deltaX, int deltaY, out Direction direction)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            direction = default;
            return false;
        }

        if (Math.Abs(deltaY) >= Math.Abs(deltaX))
        {
            direction = deltaY < 0 ? Direction.North : Direction.South;
        }
        else
        {
            direction = deltaX > 0 ? Direction.East : Direction.West;
        }

        return true;
    }
}
