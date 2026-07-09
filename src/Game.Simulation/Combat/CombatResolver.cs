using Game.Simulation.AI;
using Game.Simulation.Character;
using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
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

        CombatResult result = ResolveHit(session, attacker, defender);
        if (result.Missed)
        {
            session.MessageLog.Add($"{attacker.Kind} misses {defender.Kind}.");
            NoiseEmitter.EmitCombat(session, attacker.LocalPosition);
            session.MarkRenderDirty();
            return true;
        }

        defender.Health = Math.Max(0, defender.Health - result.Damage);
        string critText = result.Critical ? " (critical!)" : string.Empty;
        session.MessageLog.Add($"{attacker.Kind} hits {defender.Kind} for {result.Damage}{critText}.");

        NoiseEmitter.EmitCombat(session, attacker.LocalPosition);

        if (result.Damage >= 6 && defender.IsAlive)
        {
            defender.StatusEffects ??= new StatusEffectList();
            defender.StatusEffects.Add(new StatusEffect
            {
                Kind = StatusEffectKind.Bleeding,
                RemainingTurns = 3
            });

            if (result.Critical)
            {
                defender.StatusEffects.Add(new StatusEffect
                {
                    Kind = StatusEffectKind.Limping,
                    RemainingTurns = 4
                });
            }
        }

        if (attacker.Kind is EntityKind.Raptor or EntityKind.Dilophosaur)
        {
            session.FinaleThreats.Record(FinaleThreatId.RaptorPack);
        }

        if (defender.Kind is EntityKind.Raptor or EntityKind.Dilophosaur && attacker.Id == EntityId.Player)
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
            session.QuestLog.Advance("first_kill", 1, 1, session);

            if (defender.Kind is EntityKind.Raptor or EntityKind.Dilophosaur)
            {
                ExperienceService.GrantExperience(session, 50, "predator kill");
            }
        }

        session.MarkRenderDirty();
        return true;
    }

    private static CombatResult ResolveHit(GameSession session, Entity attacker, Entity defender)
    {
        int attackerPower = GetAttackPower(session, attacker);
        int defenderArmor = GetArmor(defender);
        int baseDamage = Math.Max(1, attackerPower - defenderArmor);

        ulong rollSeed = SeedUtility.Derive(
            session.Overworld.Seed,
            attacker.LocalPosition.X,
            attacker.LocalPosition.Y,
            (uint)(defender.LocalPosition.X * 31 + defender.LocalPosition.Y + session.WorldTime));

        int variance = (int)(rollSeed % 41) - 20;
        int damage = Math.Max(1, baseDamage + baseDamage * variance / 100);

        int hitChance = 80 + GetAgility(session, attacker) * 2 - GetAgility(session, defender) * 2;
        bool missed = (int)(rollSeed % 100) >= hitChance;

        bool critical = !missed && (int)(rollSeed % 100) < 5 + GetStrength(session, attacker);
        if (critical)
        {
            damage = (int)Math.Round(damage * 1.5);
        }

        return new CombatResult(missed, damage, critical);
    }

    private static int GetAttackPower(GameSession session, Entity attacker)
    {
        if (attacker.Id == EntityId.Player)
        {
            return DefaultAttackDamage + GetStrength(session, attacker);
        }

        return attacker.Kind switch
        {
            EntityKind.Raptor => 10,
            EntityKind.Dilophosaur => 8,
            EntityKind.Herbivore => 2,
            _ => DefaultAttackDamage
        };
    }

    private static int GetArmor(Entity defender)
    {
        return defender.Kind switch
        {
            EntityKind.Raptor => 2,
            EntityKind.Dilophosaur => 1,
            EntityKind.Herbivore => 0,
            _ => 0
        };
    }

    private static int GetStrength(GameSession session, Entity entity)
    {
        if (entity.Id != EntityId.Player)
        {
            return 0;
        }

        return session.CharacterProgress.Attributes.TryGetValue("strength", out int value) ? value - 10 : 0;
    }

    private static int GetAgility(GameSession session, Entity entity)
    {
        if (entity.Id != EntityId.Player)
        {
            return 0;
        }

        return session.CharacterProgress.Attributes.TryGetValue("agility", out int value) ? value - 10 : 0;
    }

    private readonly record struct CombatResult(bool Missed, int Damage, bool Critical);
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

        NoiseEmitter.EmitHarvest(session, coord);
        session.QuestLog.Advance("gather_wood", 2, 5, session);
        session.MarkRenderDirty();
        return true;
    }
}
