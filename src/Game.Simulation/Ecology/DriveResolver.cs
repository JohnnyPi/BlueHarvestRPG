using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
using Game.Simulation.Session;

namespace Game.Simulation.Ecology;

public static class DriveResolver
{
    public static CreatureDrive Resolve(Entity entity, GameSession session, LocalMap map)
    {
        entity.Drive ??= new CreatureDriveState();
        PerceptionState? perception = entity.Perception;

        if (entity.Kind == EntityKind.Herbivore)
        {
            if (perception?.Awareness is AwarenessLevel.Engaged or AwarenessLevel.Tracking)
            {
                entity.Drive.ActiveDrive = CreatureDrive.Flee;
                return CreatureDrive.Flee;
            }

            entity.Drive.ActiveDrive = CreatureDrive.Herd;
            return CreatureDrive.Herd;
        }

        if (entity.Kind is EntityKind.Raptor or EntityKind.Dilophosaur)
        {
            Entity? prey = FindVisiblePrey(entity, session, map);
            if (prey is not null &&
                perception?.Awareness is AwarenessLevel.Tracking or AwarenessLevel.Engaged)
            {
                entity.Drive.HuntTargetId = prey.Id.Value;
                entity.Drive.ActiveDrive = CreatureDrive.Hunger;
                return CreatureDrive.Hunger;
            }

            entity.Drive.HuntTargetId = null;
            entity.Drive.ActiveDrive = CreatureDrive.Patrol;
            return CreatureDrive.Patrol;
        }

        entity.Drive.ActiveDrive = CreatureDrive.Patrol;
        return CreatureDrive.Patrol;
    }

    private static Entity? FindVisiblePrey(Entity predator, GameSession session, LocalMap map)
    {
        PerceptionProfile profile = PerceptionProfile.ForKind(predator.Kind);

        foreach (Entity candidate in map.Entities.All)
        {
            if (!candidate.IsActive || candidate.Id == predator.Id)
            {
                continue;
            }

            if (candidate.Kind != EntityKind.Herbivore)
            {
                continue;
            }

            if (PerceptionSystem.CanSeeTarget(map, predator.LocalPosition, candidate.LocalPosition, profile.SightRadius))
            {
                return candidate;
            }
        }

        return null;
    }
}
