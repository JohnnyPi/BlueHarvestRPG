using Game.Simulation.Session;

namespace Game.Client.UI;

public sealed class SelectionState
{
    public bool IsLocked { get; private set; }
    public int TileX { get; private set; }
    public int TileY { get; private set; }
    public GameViewMode ViewModeWhenLocked { get; private set; }

    public void Lock(int tileX, int tileY, GameViewMode viewMode)
    {
        IsLocked = true;
        TileX = tileX;
        TileY = tileY;
        ViewModeWhenLocked = viewMode;
    }

    public void Clear()
    {
        IsLocked = false;
    }
}
