using Game.Client.Input;
using Game.Simulation.Rendering;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;

namespace Game.Client.UI;

public sealed class RunEndScreen : IUiScreen
{
    public const int Padding = 16;
    public const int LineHeight = 20;
    public const int ButtonHeight = 28;
    public const int PanelWidth = 520;

    private readonly Action _onQuit;

    private int _panelX;
    private int _panelY;
    private int _panelHeight;
    private int _buttonY;

    public RunEndScreen(Action onQuit)
    {
        _onQuit = onQuit;
    }

    public bool IsModal => true;

    public void HandleInput(InputFrame frame, UiInputResult result, RenderSnapshot snapshot, int viewportWidth, int viewportHeight)
    {
        Layout(snapshot, viewportWidth, viewportHeight);

        if (frame.Pressed.Contains(InputAction.ConfirmMenu))
        {
            if (IsOverQuitButton(frame.MouseX, frame.MouseY))
            {
                _onQuit();
                result.InputConsumed = true;
            }
        }
    }

    public void Draw(UiPainter painter, UiThemeColors theme, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        Layout(snapshot, viewportWidth, viewportHeight);

        painter.DrawRect(0, 0, viewportWidth, viewportHeight, theme.PanelBackground with { A = 180 });
        painter.DrawRect(_panelX, _panelY, PanelWidth, _panelHeight, theme.PanelBackground);
        painter.DrawBorder(_panelX, _panelY, PanelWidth, _panelHeight, theme.PanelBorder, 2);

        string title = snapshot.RunEndTitle ?? (snapshot.RunOutcome == RunOutcome.Escaped ? "Escaped" : "Run Over");
        painter.DrawString(title, _panelX + Padding, _panelY + Padding, theme.TextPrimary);

        int y = _panelY + Padding + 28;
        foreach (string line in BuildBodyLines(snapshot))
        {
            painter.DrawString(line, _panelX + Padding, y, theme.TextSecondary);
            y += LineHeight;
        }

        bool hover = IsOverQuitButton(mouseX, mouseY);
        int buttonX = _panelX + Padding;
        int buttonWidth = PanelWidth - Padding * 2;
        painter.DrawRect(buttonX, _buttonY, buttonWidth, ButtonHeight, hover ? theme.PanelAccent : theme.PanelDivider);
        painter.DrawString("Quit to Desktop", buttonX + 12, _buttonY + 6, theme.TextPrimary);
    }

    private void Layout(RenderSnapshot snapshot, int viewportWidth, int viewportHeight)
    {
        int lineCount = BuildBodyLines(snapshot).Count;
        _panelHeight = Padding * 2 + 28 + lineCount * LineHeight + ButtonHeight + 24;
        _panelX = (viewportWidth - PanelWidth) / 2;
        _panelY = (viewportHeight - _panelHeight) / 2;
        _buttonY = _panelY + _panelHeight - ButtonHeight - Padding;
    }

    private static List<string> BuildBodyLines(RenderSnapshot snapshot)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(snapshot.RunEndSummary))
        {
            lines.AddRange(WrapText(snapshot.RunEndSummary!, 62));
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ScenarioMission))
        {
            lines.Add($"Mission: {snapshot.ScenarioMission}");
        }

        string mysteryStatus = snapshot.EscapeEnding == EscapeEndingKind.Resolved
            ? "Mystery: solved"
            : snapshot.RunOutcome == RunOutcome.Escaped
                ? "Mystery: unanswered"
                : "Mystery: lost";

        lines.Add(mysteryStatus);
        return lines;
    }

    private static IEnumerable<string> WrapText(string text, int maxChars)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            yield break;
        }

        string current = words[0];
        for (int i = 1; i < words.Length; i++)
        {
            string candidate = $"{current} {words[i]}";
            if (candidate.Length > maxChars)
            {
                yield return current;
                current = words[i];
            }
            else
            {
                current = candidate;
            }
        }

        yield return current;
    }

    private bool IsOverQuitButton(int x, int y)
    {
        return x >= _panelX + Padding &&
               x <= _panelX + PanelWidth - Padding &&
               y >= _buttonY &&
               y <= _buttonY + ButtonHeight;
    }
}
