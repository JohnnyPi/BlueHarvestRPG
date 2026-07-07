namespace Game.Simulation.World;

public readonly record struct EdgeConnection(
    Direction Edge,
    int LocalOffset,
    ConnectionType Type,
    int Width)
{
    public bool Mirrors(EdgeConnection other)
    {
        return Edge.Opposite() == other.Edge &&
               LocalOffset == other.LocalOffset &&
               Type == other.Type &&
               Width == other.Width;
    }

    public ConnectionFlags ToFlag()
    {
        if (Type == ConnectionType.Road)
        {
            return Edge switch
            {
                Direction.North => ConnectionFlags.NorthRoad,
                Direction.East => ConnectionFlags.EastRoad,
                Direction.South => ConnectionFlags.SouthRoad,
                Direction.West => ConnectionFlags.WestRoad,
                _ => ConnectionFlags.None
            };
        }

        return Edge switch
        {
            Direction.North => ConnectionFlags.NorthRiver,
            Direction.East => ConnectionFlags.EastRiver,
            Direction.South => ConnectionFlags.SouthRiver,
            Direction.West => ConnectionFlags.WestRiver,
            _ => ConnectionFlags.None
        };
    }
}
