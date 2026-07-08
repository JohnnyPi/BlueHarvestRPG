using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class RuinStampPass : IGenerationPass
{
    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is null)
        {
            return;
        }

        foreach (RuinSite site in context.IslandPlan.RuinSites)
        {
            if (!CoordinateMath.OverlapsCell(
                    site.GlobalOriginX,
                    site.GlobalOriginY,
                    site.Width,
                    site.Height,
                    context.WorldCoordinate))
            {
                continue;
            }

            StructureStampHelper.StampRect(
                map,
                context.WorldCoordinate,
                site.GlobalOriginX,
                site.GlobalOriginY,
                site.Width,
                site.Height,
                (m, localX, localY, withinX, withinY) =>
                {
                    bool isPerimeter =
                        withinX == 0 || withinY == 0 ||
                        withinX == site.Width - 1 || withinY == site.Height - 1;

                    if (site.Kind == RuinKind.WarFortification)
                    {
                        bool isEntry = withinX == site.Width / 2 && withinY == site.Height - 1;
                        if (isPerimeter && !isEntry)
                        {
                            m.SetTerrain(localX, localY, TerrainId.Wall, TileFlags.BlocksMovement | TileFlags.BlocksVision);
                        }
                        else if (isEntry)
                        {
                            m.SetTerrain(localX, localY, TerrainId.Door, TileFlags.None);
                        }
                        else
                        {
                            m.SetTerrain(localX, localY, TerrainId.RuinStone, TileFlags.None);
                        }

                        return;
                    }

                    bool isRuinEntry = withinX == site.Width / 2 && withinY == site.Height - 1;
                    bool broken = (withinX + withinY) % 3 == 0;
                    if (isPerimeter && !broken && !isRuinEntry)
                    {
                        m.SetTerrain(localX, localY, TerrainId.RuinStone, TileFlags.BlocksMovement);
                    }
                    else
                    {
                        m.SetTerrain(localX, localY, TerrainId.RuinStone, TileFlags.None);
                    }
                });
        }
    }
}
