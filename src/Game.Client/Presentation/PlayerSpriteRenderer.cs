using Game.Client.Debugging;
using Game.Content.Definitions;
using Game.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Presentation;

internal readonly record struct PlayerSpriteFrame(Texture2D Texture, Rectangle SourceRect);

internal sealed class PlayerSpriteRenderer
{
    private const string TilesetRoot = "Textures/Tilesets";

    private readonly PlayerSpriteSheet _sheet;
    private readonly Texture2D? _walkTexture;
    private readonly Texture2D? _idleTexture;

    public PlayerSpriteRenderer(ContentManager content, PlayerDefinition definition)
    {
        _sheet = new PlayerSpriteSheet(definition);

        if (!definition.Characters.TryGetValue(definition.DefaultCharacter, out PlayerCharacterDefinition? character))
        {
            DebugLog.Issue($"Player character '{definition.DefaultCharacter}' not found in player.yaml.");
            return;
        }

        _walkTexture = TryLoad(content, character.Texture);
        _idleTexture = character.IdleTexture is not null
            ? TryLoad(content, character.IdleTexture)
            : _walkTexture;

        ValidateTextureLayout(definition, _walkTexture, "walk");
        ValidateTextureLayout(definition, _idleTexture, "idle");
    }

    private static void ValidateTextureLayout(PlayerDefinition definition, Texture2D? texture, string label)
    {
        if (texture is null)
        {
            return;
        }

        int expectedWidth = definition.Columns * definition.FrameWidth;
        int expectedHeight = definition.Rows * definition.FrameHeight;
        if (texture.Width != expectedWidth || texture.Height != expectedHeight)
        {
            DebugLog.Issue(
                $"Player {label} texture '{texture.Name}' is {texture.Width}x{texture.Height}, " +
                $"expected {expectedWidth}x{expectedHeight} from player.yaml frame layout.");
        }
    }

    public PlayerSpriteSheet Sheet => _sheet;
    public bool HasTexture => _walkTexture is not null;

    public PlayerSpriteFrame GetWalkFrame(Direction facing, int column) =>
        new(_walkTexture!, _sheet.GetSourceRect(facing, column, PlayerSpriteRowLayout.Walk));

    public PlayerSpriteFrame GetIdleFrame(Direction facing) =>
        new(_idleTexture ?? _walkTexture!, _sheet.GetSourceRect(facing, 0, PlayerSpriteRowLayout.Idle));

    private static Texture2D? TryLoad(ContentManager content, string texturePath)
    {
        string stem = Path.ChangeExtension(texturePath, null)?.Replace('\\', '/') ?? texturePath;
        string assetName = $"{TilesetRoot}/{stem}";

        try
        {
            return content.Load<Texture2D>(assetName);
        }
        catch (ContentLoadException ex)
        {
            DebugLog.Issue($"Failed to load player texture '{assetName}': {ex.Message}");
            return null;
        }
    }
}
