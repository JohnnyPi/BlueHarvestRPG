using Game.Simulation.Entities;
using Game.Simulation.Session;

namespace Game.Simulation.Combat;

public enum StatusEffectKind
{
    Bleeding,
    Limping
}

public sealed class StatusEffect
{
    public StatusEffectKind Kind { get; init; }

    public int RemainingTurns { get; set; }
}

public sealed class StatusEffectList
{
    private readonly List<StatusEffect> _effects = [];

    public IReadOnlyList<StatusEffect> Effects => _effects;

    public void Add(StatusEffect effect)
    {
        StatusEffect? existing = _effects.FirstOrDefault(entry => entry.Kind == effect.Kind);
        if (existing is not null)
        {
            existing.RemainingTurns = Math.Max(existing.RemainingTurns, effect.RemainingTurns);
            return;
        }

        _effects.Add(effect);
    }

    public bool Has(StatusEffectKind kind)
    {
        return _effects.Any(entry => entry.Kind == kind);
    }

    public void Remove(StatusEffectKind kind)
    {
        _effects.RemoveAll(entry => entry.Kind == kind);
    }

    public void ClearMinor()
    {
        Remove(StatusEffectKind.Bleeding);
        Remove(StatusEffectKind.Limping);
    }

    public void Tick(GameSession session, Entity entity)
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = _effects[i];
            if (effect.Kind == StatusEffectKind.Bleeding && entity.IsAlive)
            {
                entity.Health = Math.Max(0, entity.Health - 1);
                if (entity.Id == EntityId.Player)
                {
                    session.RefreshPlayerVitals();
                }
            }

            effect.RemainingTurns--;
            if (effect.RemainingTurns <= 0)
            {
                _effects.RemoveAt(i);
            }
        }
    }
}
