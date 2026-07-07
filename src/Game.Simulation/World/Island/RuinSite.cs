namespace Game.Simulation.World.Island;

public enum RuinKind : byte
{
    AncientRuin,
    WarFortification
}

public sealed record RuinSite(
    RuinKind Kind,
    int GlobalOriginX,
    int GlobalOriginY,
    int Width,
    int Height);
