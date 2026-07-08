using Game.IslandPreview;

ulong? seed = ParseSeed(args);
using var game = new IslandPreviewGame(seed);
game.Run();

static ulong? ParseSeed(string[] args)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--seed" && ulong.TryParse(args[i + 1], out ulong parsed))
        {
            return parsed;
        }
    }

    return null;
}
