using Game.Simulation.Rendering;

namespace Game.Client.UI;

public sealed class UiScreenStack
{
    private readonly Stack<IUiScreen> _screens = new();

    public bool HasModalScreen => _screens.Count > 0;

    public bool BlocksSimulationInput => HasModalScreen && _screens.Peek().IsModal;

    public IUiScreen? Top => _screens.Count > 0 ? _screens.Peek() : null;

    public void Push(IUiScreen screen) => _screens.Push(screen);

    public void Pop()
    {
        if (_screens.Count > 0)
        {
            _screens.Pop();
        }
    }

    public bool Contains<T>() where T : IUiScreen
    {
        return _screens.Any(screen => screen is T);
    }

    public void PopIf<T>() where T : IUiScreen
    {
        if (Top is T)
        {
            Pop();
        }
    }

    public void Draw(UiPainter painter, UiThemeColors theme, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        foreach (IUiScreen screen in _screens.Reverse())
        {
            screen.Draw(painter, theme, snapshot, viewportWidth, viewportHeight, mouseX, mouseY);
        }
    }
}
