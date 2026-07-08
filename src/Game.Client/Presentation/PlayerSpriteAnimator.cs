using Game.Content.Definitions;
using Game.Simulation.Rendering;
using Game.Simulation.World;

namespace Game.Client.Presentation;

internal sealed class PlayerSpriteAnimator
{
    private static readonly int[] WalkColumns = [0, 1, 2, 3];

    private readonly PlayerSpriteRenderer _renderer;
    private readonly float _walkFrameDurationSeconds;
    private readonly float _stepAnimationDurationSeconds;

    private int _lastPlayerX = -1;
    private int _lastPlayerY = -1;
    private Direction _facing = Direction.South;
    private bool _isWalking;
    private float _stepTimer;
    private float _frameTimer;
    private int _walkFrameIndex;

    public PlayerSpriteAnimator(PlayerSpriteRenderer renderer, PlayerDefinition definition)
    {
        _renderer = renderer;
        _walkFrameDurationSeconds = definition.WalkFrameDurationMs / 1000f;
        _stepAnimationDurationSeconds = definition.StepAnimationDurationMs / 1000f;
    }

    public void Update(float deltaSeconds, RenderSnapshot snapshot)
    {
        _facing = snapshot.PlayerFacing;

        if (snapshot.PlayerX != _lastPlayerX || snapshot.PlayerY != _lastPlayerY)
        {
            _lastPlayerX = snapshot.PlayerX;
            _lastPlayerY = snapshot.PlayerY;
            _isWalking = true;
            _stepTimer = _stepAnimationDurationSeconds;
            _frameTimer = 0f;
            _walkFrameIndex = 0;
        }

        if (!_isWalking)
        {
            return;
        }

        _stepTimer -= deltaSeconds;
        if (_stepTimer <= 0f)
        {
            _isWalking = false;
            return;
        }

        _frameTimer += deltaSeconds;
        if (_frameTimer >= _walkFrameDurationSeconds)
        {
            _frameTimer -= _walkFrameDurationSeconds;
            _walkFrameIndex = (_walkFrameIndex + 1) % WalkColumns.Length;
        }
    }

    public PlayerSpriteFrame GetCurrentFrame()
    {
        if (_isWalking)
        {
            return _renderer.GetWalkFrame(_facing, WalkColumns[_walkFrameIndex]);
        }

        return _renderer.GetIdleFrame(_facing);
    }
}
