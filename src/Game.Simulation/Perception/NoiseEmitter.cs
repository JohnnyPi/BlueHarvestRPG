using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;

namespace Game.Simulation.Perception;

public static class NoiseEmitter
{
    public const byte WalkNoise = 1;
    public const byte HarvestNoise = 3;
    public const byte CombatNoise = 5;
    public const byte SprintNoise = 4;
    public const byte DistractionNoise = 6;

    public static void EmitWalk(GameSession session, LocalCoord position, float multiplier = 1f)
    {
        Emit(session, position, ScaleNoise(WalkNoise, multiplier));
    }

    public static void EmitHarvest(GameSession session, LocalCoord position)
    {
        Emit(session, position, HarvestNoise);
    }

    public static void EmitCombat(GameSession session, LocalCoord position)
    {
        Emit(session, position, CombatNoise);
    }

    public static void EmitSprint(GameSession session, LocalCoord position, float multiplier = 1f)
    {
        Emit(session, position, ScaleNoise(SprintNoise, multiplier));
    }

    public static void EmitCustom(GameSession session, LocalCoord position, byte strength)
    {
        Emit(session, position, strength);
    }

    private static void Emit(GameSession session, LocalCoord position, byte strength)
    {
        if (session.ActiveLocalMap is null || strength == 0)
        {
            return;
        }

        session.ActiveLocalMap.Noise.Emit(
            session.ActiveLocalMap,
            position,
            strength,
            session.WorldTime);
    }

    private static byte ScaleNoise(byte baseNoise, float multiplier)
    {
        int scaled = (int)Math.Round(baseNoise * multiplier);
        return (byte)Math.Clamp(scaled, 0, 255);
    }
}
