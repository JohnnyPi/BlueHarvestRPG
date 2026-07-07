using Microsoft.Xna.Framework.Input;

namespace Game.Client.Input;

public interface IInputSource
{
    KeyboardState GetKeyboardState();

    MouseState GetMouseState();
}

public sealed class MonoGameInputSource : IInputSource
{
    public KeyboardState GetKeyboardState() => Keyboard.GetState();

    public MouseState GetMouseState() => Mouse.GetState();
}
