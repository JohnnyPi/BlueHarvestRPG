using Game.Client.Input;
using Game.Content.Definitions;
using Microsoft.Xna.Framework;

namespace Game.Client.Presentation.Camera;

public sealed class CameraController
{
    private readonly CameraDefinition _definition;

    public float Zoom { get; private set; }
    public Vector2 Offset { get; private set; }

    public int BaseCellSize => _definition.BaseCellSize;
    public float CellSize => _definition.BaseCellSize * Zoom;

    public CameraController(CameraDefinition definition)
    {
        _definition = definition;
        Zoom = 1f;
        Offset = Vector2.Zero;
    }

    public void Update(InputFrame frame, float deltaSeconds)
    {
        float pan = _definition.PanSpeed * deltaSeconds / Zoom;
        var move = Vector2.Zero;

        if (frame.Held.Contains(InputAction.PanNorth))
        {
            move.Y -= pan;
        }

        if (frame.Held.Contains(InputAction.PanSouth))
        {
            move.Y += pan;
        }

        if (frame.Held.Contains(InputAction.PanWest))
        {
            move.X -= pan;
        }

        if (frame.Held.Contains(InputAction.PanEast))
        {
            move.X += pan;
        }

        Offset += move;

        if (frame.WheelDelta != 0)
        {
            ApplyZoom(frame.WheelDelta > 0 ? 1 : -1, new Vector2(frame.MouseX, frame.MouseY));
        }
    }

    private void ApplyZoom(int direction, Vector2 focus)
    {
        Vector2 worldBefore = ScreenToWorld(focus);

        float factor = direction > 0 ? _definition.ZoomStep : 1f / _definition.ZoomStep;
        Zoom = Math.Clamp(Zoom * factor, _definition.MinZoom, _definition.MaxZoom);

        Vector2 worldAfter = ScreenToWorld(focus);
        Offset += (worldBefore - worldAfter) * CellSize;
    }

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        return (screen + Offset) / CellSize;
    }

    public Vector2 WorldToScreen(float worldX, float worldY)
    {
        return new Vector2(worldX * CellSize - Offset.X, worldY * CellSize - Offset.Y);
    }

    public void CenterOn(float worldX, float worldY, int viewportWidth, int viewportHeight)
    {
        Offset = new Vector2(worldX * CellSize - viewportWidth / 2f, worldY * CellSize - viewportHeight / 2f);
    }
}
