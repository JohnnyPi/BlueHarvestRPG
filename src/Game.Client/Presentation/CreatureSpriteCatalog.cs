using Game.Client.Debugging;
using Game.Content.Definitions;
using Game.Simulation.Entities;
using Game.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Presentation;

internal readonly record struct CreatureSpriteEntry(
    Texture2D Texture,
    float TileWidth,
    float TileHeight);

internal sealed class CreatureSpriteCatalog
{
    private const string TilesetRoot = "Textures/Tilesets";

    private readonly Dictionary<string, CreatureSpriteEntry> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityKind, string> _kindBindings = [];

    public CreatureSpriteCatalog(ContentManager content, CreaturesDefinition definition)
    {
        foreach ((string kindKey, string creatureKey) in definition.KindBindings)
        {
            if (Enum.TryParse(kindKey, ignoreCase: true, out EntityKind kind))
            {
                _kindBindings[kind] = creatureKey;
            }
            else
            {
                DebugLog.Issue($"Unknown entity kind '{kindKey}' in creatures.yaml kindBindings.");
            }
        }

        foreach ((string creatureKey, CreatureSpriteDefinition creatureDefinition) in definition.Creatures)
        {
            Texture2D? texture = TryLoad(content, creatureDefinition.Texture);
            if (texture is null)
            {
                continue;
            }

            _sprites[creatureKey] = new CreatureSpriteEntry(
                texture,
                creatureDefinition.TileWidth,
                creatureDefinition.TileHeight);
        }
    }

    public bool TryGet(string? spriteId, out CreatureSpriteEntry entry)
    {
        if (string.IsNullOrWhiteSpace(spriteId))
        {
            entry = default;
            return false;
        }

        return _sprites.TryGetValue(spriteId, out entry);
    }

    public string? ResolveSpriteId(EntityKind kind, string? explicitSpriteId)
    {
        if (!string.IsNullOrWhiteSpace(explicitSpriteId))
        {
            return explicitSpriteId;
        }

        return _kindBindings.TryGetValue(kind, out string? creatureKey) ? creatureKey : null;
    }

    public static Rectangle ComputeDestination(
        Direction facing,
        int tileSize,
        int screenX,
        int screenY,
        float tileWidth,
        float tileHeight,
        out SpriteEffects effects)
    {
        int destW = Math.Max(1, (int)Math.Round(tileSize * tileWidth));
        int destH = Math.Max(1, (int)Math.Round(tileSize * tileHeight));
        int destY = screenY + tileSize - destH;

        effects = SpriteEffects.None;
        int destX = facing switch
        {
            Direction.West => screenX + tileSize - destW,
            Direction.East => screenX,
            _ => screenX + (tileSize - destW) / 2
        };

        if (facing == Direction.West)
        {
            effects = SpriteEffects.FlipHorizontally;
        }

        return new Rectangle(destX, destY, destW, destH);
    }

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
            DebugLog.Issue($"Failed to load creature texture '{assetName}': {ex.Message}");
            return null;
        }
    }
}
