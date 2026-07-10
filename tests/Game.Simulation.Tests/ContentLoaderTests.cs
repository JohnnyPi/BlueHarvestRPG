using Game.Content;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class ContentLoaderTests
{
    [Fact]
    public void LoadAll_ParsesAllContentFiles()
    {
        var loader = new ContentLoader();
        GameContentBundle bundle = loader.LoadAll();

        Assert.True(bundle.Controls.Actions.ContainsKey("PanNorth"));
        Assert.Contains("W", bundle.Controls.Actions["PanNorth"].Keyboard);

        foreach (BiomeId biome in Enum.GetValues<BiomeId>())
        {
            Assert.True(bundle.BiomeColors.Biomes.ContainsKey(biome.ToString()));
        }

        foreach (TerrainId terrain in Enum.GetValues<TerrainId>())
        {
            Assert.True(bundle.TerrainColors.Terrain.ContainsKey(terrain.ToString()));
            Assert.True(bundle.Tiles.Terrain.ContainsKey(terrain.ToString()));
        }

        foreach (BiomeId biome in Enum.GetValues<BiomeId>())
        {
            Assert.True(bundle.Tiles.Biomes.ContainsKey(biome.ToString()));
        }

        Assert.True(bundle.ElevationShading.Enabled);
        Assert.Contains(nameof(BiomeId.Ocean), bundle.ElevationShading.ExcludedBiomes);
        Assert.True(bundle.ElevationShading.Biomes.ContainsKey(nameof(BiomeId.Beach)));

        Assert.NotEmpty(bundle.ContextMenus.Overworld);
        Assert.NotEmpty(bundle.ContextMenus.LocalMap);
    }

    [Fact]
    public void LoadAll_ThrowsWhenControlFileMissing()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "RougeContentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);

        try
        {
            var loader = new ContentLoader(emptyDir);
            Assert.Throws<ContentLoadException>(() => loader.LoadAll());
        }
        finally
        {
            Directory.Delete(emptyDir, recursive: true);
        }
    }

    [Fact]
    public void LoadAll_ThrowsOnUnknownMenuIntent()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RougeContentTests", Guid.NewGuid().ToString("N"));
        CopyValidContent(dir);

        string menuPath = Path.Combine(dir, "ui", "context_menus.yaml");
        File.WriteAllText(menuPath,
            "overworld:\n  - id: bad\n    label: Bad\n    intent: NotARealIntent\nlocalMap: []\n");

        try
        {
            var loader = new ContentLoader(dir);
            Assert.Throws<ContentLoadException>(() => loader.LoadAll());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadAll_ThrowsOnInvalidElevationShadingRange()
    {
        string dir = Path.Combine(Path.GetTempPath(), "RougeContentTests", Guid.NewGuid().ToString("N"));
        CopyValidContent(dir);

        string shadingPath = Path.Combine(dir, "presentation", "elevation_shading.yaml");
        File.WriteAllText(shadingPath,
            """
            enabled: true
            defaultProfile:
              darkestElevation: 0.8
              fullBrightnessElevation: 0.4
              maxDarkening: 0.2
            """);

        try
        {
            var loader = new ContentLoader(dir);
            Assert.Throws<ContentLoadException>(() => loader.LoadAll());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void CopyValidContent(string destination)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "content");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
