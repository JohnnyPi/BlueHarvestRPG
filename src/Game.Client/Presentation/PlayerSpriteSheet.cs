using Game.Content.Definitions;
using Game.Simulation.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

internal enum PlayerSpriteRowLayout
{
    Walk,
    Idle
}

internal sealed class PlayerSpriteSheet
{
    private readonly PlayerDefinition _definition;

    public PlayerSpriteSheet(PlayerDefinition definition)
    {
        _definition = definition;
    }

    public int FrameWidth => _definition.FrameWidth;
    public int FrameHeight => _definition.FrameHeight;

    public static int GetRow(Direction facing, PlayerSpriteRowLayout layout) =>
        layout switch
        {
            PlayerSpriteRowLayout.Walk => facing switch
            {
                Direction.South => 0,
                Direction.West => 1,
                Direction.East => 2,
                Direction.North => 3,
                _ => 0
            },
            PlayerSpriteRowLayout.Idle => facing switch
            {
                Direction.South => 0,
                Direction.East => 1,
                Direction.West => 2,
                Direction.North => 3,
                _ => 0
            },
            _ => 0
        };

    public Rectangle GetSourceRect(Direction facing, int column, PlayerSpriteRowLayout layout)
    {
        int row = GetRow(facing, layout);
        int clampedColumn = Math.Clamp(column, 0, _definition.Columns - 1);
        return new Rectangle(
            clampedColumn * _definition.FrameWidth,
            row * _definition.FrameHeight,
            _definition.FrameWidth,
            _definition.FrameHeight);
    }
}
