using Game.Client.Input;
using Game.Simulation.Rendering;

namespace Game.Client.UI;

public interface IUiScreen
{
    bool IsModal { get; }

    void HandleInput(InputFrame frame, UiInputResult result, RenderSnapshot snapshot, int viewportWidth, int viewportHeight);

    void Draw(UiPainter painter, UiThemeColors theme, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY);
}
