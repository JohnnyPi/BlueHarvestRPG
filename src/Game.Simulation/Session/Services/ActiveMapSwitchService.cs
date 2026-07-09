using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Session.Services;

public sealed class ActiveMapSwitchService
{
    public void SwitchTo(
        GameSession session,
        LocalMap destination,
        LocalCoord landing,
        bool markSurfaceLocalChanges)
    {
        if (session.ActiveLocalMap is not null)
        {
            session.RefreshPlayerVitals();
            session.LocalMapRepository.Store(session.ActiveLocalMap);
            session.ActiveLocalMap.Entities.Remove(EntityId.Player);

            if (markSurfaceLocalChanges && session.ActiveLocalMap.IsSurface)
            {
                ref WorldCell cell = ref session.Overworld.GetCell(session.ActiveLocalMap.WorldPosition);
                cell.HasLocalChanges = true;
            }
        }

        session.ActiveLocalMap = destination;
        session.PlayerLocalPosition = landing;
        session.SyncPlayerEntityPosition();
        session.EnsurePlayerEntity();
        session.MarkVisibilityDirty();
    }
}
