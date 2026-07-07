using Game.Content;
using Game.Content.Definitions;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Input;

public enum MouseButton
{
    LeftButton,
    RightButton,
    MiddleButton,
}

public sealed class InputFrame
{
    public HashSet<InputAction> Held { get; } = [];
    public HashSet<InputAction> Pressed { get; } = [];

    public void Clear()
    {
        Held.Clear();
        Pressed.Clear();
        WheelDelta = 0;
    }

    public int WheelDelta { get; set; }
    public int MouseX { get; set; }
    public int MouseY { get; set; }
}

public sealed class InputMapper
{
    private readonly Dictionary<InputAction, List<Keys>> _keyBindings = [];
    private readonly Dictionary<InputAction, List<MouseButton>> _mouseBindings = [];
    private readonly IInputSource _inputSource;

    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private readonly InputFrame _frame = new();

    public InputMapper(ControlsDefinition controls, IInputSource? inputSource = null)
    {
        _inputSource = inputSource ?? new MonoGameInputSource();

        foreach ((string actionName, ActionBinding binding) in controls.Actions)
        {
            if (!Enum.TryParse(actionName, out InputAction action))
            {
                continue;
            }

            if (binding.Keyboard.Count > 0)
            {
                _keyBindings[action] = ParseKeys(actionName, binding.Keyboard);
            }

            if (binding.Mouse.Count > 0)
            {
                _mouseBindings[action] = ParseMouseButtons(actionName, binding.Mouse);
            }
        }
    }

    public InputFrame Sample()
    {
        KeyboardState keyboard = _inputSource.GetKeyboardState();
        MouseState mouse = _inputSource.GetMouseState();

        _frame.Clear();
        _frame.WheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        _frame.MouseX = mouse.X;
        _frame.MouseY = mouse.Y;

        foreach ((InputAction action, List<Keys> keys) in _keyBindings)
        {
            foreach (Keys key in keys)
            {
                if (keyboard.IsKeyDown(key))
                {
                    _frame.Held.Add(action);
                }

                if (keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key))
                {
                    _frame.Pressed.Add(action);
                }
            }
        }

        foreach ((InputAction action, List<MouseButton> buttons) in _mouseBindings)
        {
            foreach (MouseButton button in buttons)
            {
                if (IsMouseDown(mouse, button))
                {
                    _frame.Held.Add(action);
                }

                if (IsMouseDown(mouse, button) && !IsMouseDown(_previousMouse, button))
                {
                    _frame.Pressed.Add(action);
                }
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        return _frame;
    }

    private static bool IsMouseDown(MouseState state, MouseButton button)
    {
        return button switch
        {
            MouseButton.LeftButton => state.LeftButton == ButtonState.Pressed,
            MouseButton.RightButton => state.RightButton == ButtonState.Pressed,
            MouseButton.MiddleButton => state.MiddleButton == ButtonState.Pressed,
            _ => false,
        };
    }

    private static List<Keys> ParseKeys(string action, IEnumerable<string> names)
    {
        var result = new List<Keys>();
        foreach (string name in names)
        {
            if (Enum.TryParse(name, ignoreCase: true, out Keys key))
            {
                result.Add(key);
            }
            else
            {
                throw new ContentLoadException($"Control action '{action}' references unknown key '{name}'.");
            }
        }

        return result;
    }

    private static List<MouseButton> ParseMouseButtons(string action, IEnumerable<string> names)
    {
        var result = new List<MouseButton>();
        foreach (string name in names)
        {
            if (Enum.TryParse(name, ignoreCase: true, out MouseButton button))
            {
                result.Add(button);
            }
            else
            {
                throw new ContentLoadException($"Control action '{action}' references unknown mouse button '{name}'.");
            }
        }

        return result;
    }
}
