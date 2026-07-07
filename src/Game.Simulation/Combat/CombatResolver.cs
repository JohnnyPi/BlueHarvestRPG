using Game.Simulation.AI;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Scenarios;
using Game.Simulation.Seeds;
using Game.Simulation.Session;

namespace Game.Simulation.Combat;

public sealed class CombatResolver
{
    public const int DefaultAttackDamage = 8;

    public bool TryAttack(GameSession session, Entity attacker, Entity defender)
    {
        if (!defender.IsActive || !defender.IsAlive)
        {
            return false;
        }

        if (defender.MaxHealth <= 0)
        {
            return false;
        }

        defender.Health = Math.Max(0, defender.Health - DefaultAttackDamage);
        session.MessageLog.Add($"{attacker.Kind} hits {defender.Kind} for {DefaultAttackDamage}.");

        if (attacker.Kind == EntityKind.Raptor)
        {
            session.FinaleThreats.Record(FinaleThreatId.RaptorPack);
        }

        if (defender.Kind == EntityKind.Raptor && attacker.Id == EntityId.Player)
        {
            RaptorBehavior.OnDamaged(session, defender);
        }

        if (defender.Id == EntityId.Player)
        {
            session.RefreshPlayerVitals();
        }

        if (defender.Id == EntityId.Player && defender.Health <= 0)
        {
            EscapeVictoryResolver.MarkPlayerDead(session);
            session.MarkRenderDirty();
            return true;
        }

        if (defender.Health <= 0)
        {
            defender.IsActive = false;
            if (session.ActiveLocalMap is not null)
            {
                session.ActiveLocalMap.Entities.Remove(defender.Id);
            }

            session.MessageLog.Add($"{defender.Kind} is defeated.");
            session.QuestLog.Advance("first_kill", 1, 1);
        }

        session.MarkRenderDirty();
        return true;
    }
}

public sealed class InteractionResolver
{
    public bool TryHarvest(GameSession session, int x, int y)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        var coord = new LocalCoord(x, y);
        Entity? tree = session.ActiveLocalMap.Entities.GetAt(coord);
        if (tree is null || tree.Kind != EntityKind.HarvestableTree || !tree.IsActive)
        {
            return false;
        }

        session.ActiveLocalMap.Entities.Remove(tree.Id);
        session.Inventory.Add(new ItemStack(ItemId.Wood, 2));

        ulong berryRoll = SeedUtility.Derive(
            session.Overworld.Seed,
            session.PlayerWorldPosition.X,
            session.PlayerWorldPosition.Y,
            (uint)(coord.X * 17 + coord.Y));
        if ((berryRoll & 1) == 0)
        {
            session.Inventory.Add(new ItemStack(ItemId.Berry, 1));
            session.MessageLog.Add("Harvested wood and berries from tree.");
        }
        else
        {
            session.MessageLog.Add("Harvested wood from tree.");
        }

        session.QuestLog.Advance("gather_wood", 2, 5);
        session.MarkRenderDirty();
        return true;
    }
}
