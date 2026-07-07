namespace Game.Simulation.Input;

public enum GameIntent
{
    MoveNorth,
    MoveSouth,
    MoveWest,
    MoveEast,
    MoveNorthWest,
    MoveNorthEast,
    MoveSouthWest,
    MoveSouthEast,
    EnterCell,
    LeaveLocalMap,
    RemoveTerrain,
    SaveGame,
    MoveToSelected,
    EnterSelected,
    InspectSelected,
    HarvestAtSelected,
    RemoveTerrainAtSelected,
    TransitionBorderNorth,
    TransitionBorderEast,
    TransitionBorderSouth,
    TransitionBorderWest,
    Wait,
    None
}
