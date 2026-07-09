using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
using Game.Simulation.Seeds;
using Game.Simulation.Session;

namespace Game.Simulation.Items;

public sealed class ItemUseResolver
{
    public bool TryUseItem(GameSession session, ItemId itemId, int targetX, int targetY)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        return itemId switch
        {
            ItemId.Berry => TryEatBerry(session),
            ItemId.Wood when session.Inventory.Stacks.Any(stack => stack.ItemId == ItemId.Rope && stack.Count > 0)
                => TryCraftSnare(session, targetX, targetY),
            ItemId.Wood => TryCraftFencePatch(session, targetX, targetY),
            ItemId.Rope => false,
            _ => false
        };
    }

    public bool TryPlaceDistraction(GameSession session, int targetX, int targetY)
    {
        if (session.ActiveLocalMap is null)
        {
            return false;
        }

        if (!session.Inventory.TryRemove(ItemId.Berry, 1) || !session.Inventory.TryRemove(ItemId.Wood, 1))
        {
            session.MessageLog.Add("Need 1 berry and 1 wood for a distraction.");
            return false;
        }

        var coord = new LocalCoord(targetX, targetY);
        if (!session.ActiveLocalMap.Contains(coord) || session.ActiveLocalMap.BlocksMovement(coord))
        {
            return false;
        }

        if (session.ActiveLocalMap.Entities.GetAt(coord) is not null)
        {
            return false;
        }

        session.ActiveLocalMap.Entities.Add(new Entity
        {
            Id = new EntityId(SeedUtility.Derive(
                session.Overworld.Seed,
                targetX,
                targetY,
                0xD157_0001)),
            Kind = EntityKind.NoiseLure,
            WorldPosition = session.PlayerWorldPosition,
            LocalPosition = coord,
            IsActive = true,
            BlocksMovement = false,
            MaxHealth = 1,
            Health = 1
        });

        NoiseEmitter.EmitCustom(session, coord, NoiseEmitter.DistractionNoise);
        session.MessageLog.Add("You set a noisy distraction.");
        session.MarkRenderDirty();
        return true;
    }

    private static bool TryEatBerry(GameSession session)
    {
        if (!session.Inventory.TryRemove(ItemId.Berry, 1))
        {
            return false;
        }

        Entity player = session.PlayerEntity;
        player.Health = Math.Min(player.MaxHealth, player.Health + 8);
        player.StatusEffects?.ClearMinor();
        session.RefreshPlayerVitals();
        session.MessageLog.Add("You eat a berry and feel a little stronger.");
        session.MarkRenderDirty();
        return true;
    }

    private static bool TryCraftSnare(GameSession session, int targetX, int targetY)
    {
        if (!session.Inventory.TryRemove(ItemId.Wood, 1) || !session.Inventory.TryRemove(ItemId.Rope, 1))
        {
            return false;
        }

        var coord = new LocalCoord(targetX, targetY);
        LocalMap map = session.ActiveLocalMap!;
        if (!map.Contains(coord) || map.BlocksMovement(coord))
        {
            return false;
        }

        if (map.Entities.GetAt(coord) is not null)
        {
            return false;
        }

        map.Entities.Add(new Entity
        {
            Id = new EntityId(SeedUtility.Derive(session.Overworld.Seed, targetX, targetY, 0x5100_0001u)),
            Kind = EntityKind.SnareTrap,
            WorldPosition = session.PlayerWorldPosition,
            LocalPosition = coord,
            IsActive = true,
            BlocksMovement = false,
            MaxHealth = 1,
            Health = 1
        });

        session.MessageLog.Add("You set a snare trap.");
        session.MarkRenderDirty();
        return true;
    }

    private static bool TryCraftFencePatch(GameSession session, int targetX, int targetY)
    {
        if (!session.Inventory.TryRemove(ItemId.Wood, 2))
        {
            session.MessageLog.Add("Need 2 wood to patch a fence.");
            return false;
        }

        LocalMap map = session.ActiveLocalMap!;
        var coord = new LocalCoord(targetX, targetY);
        if (!map.Contains(coord))
        {
            return false;
        }

        int index = map.GetIndex(coord.X, coord.Y);
        if (map.Terrain[index] != TerrainId.Fence)
        {
            session.MessageLog.Add("That tile is not a fence.");
            return false;
        }

        map.SetTerrain(coord.X, coord.Y, TerrainId.Grass, TileFlags.None);
        session.MessageLog.Add("You patch the fence with scavenged wood.");
        session.MarkRenderDirty();
        return true;
    }
}
