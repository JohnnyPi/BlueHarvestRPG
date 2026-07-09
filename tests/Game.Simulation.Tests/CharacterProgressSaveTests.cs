using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Persistence.Serialization;
using Game.Simulation;
using Game.Simulation.Character;
using Game.Simulation.Session;

namespace Game.Simulation.Tests;

public sealed class CharacterProgressSaveTests
{
    [Fact]
    public void Save_and_load_round_trips_character_progress()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "blueharvest-tests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);

        var world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(99UL);
        var repository = new InMemoryLocalMapRepository(world, new LocalMapGenerator());
        var progress = CharacterProgress.CreateFromDefaults(1, 0, [("strength", 10), ("vitality", 10)]);
        progress.Level = 3;
        progress.Experience = 120;
        progress.Attributes["strength"] = 14;

        var session = new GameSession(world, repository, progress);
        saveManager.Save(world, session, repository, biomeRulesHash: 0);

        bool loaded = saveManager.TryLoad(
            "autosave",
            new LocalMapGenerator(),
            TestSaveDefaults.Island,
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules,
            currentBiomeRulesHash: 0,
            out _,
            out GameSession loadedSession,
            out _,
            out _,
            out string? failureReason);

        Assert.True(loaded, failureReason);
        Assert.Equal(3, loadedSession.CharacterProgress.Level);
        Assert.Equal(120, loadedSession.CharacterProgress.Experience);
        Assert.Equal(14, loadedSession.CharacterProgress.Attributes["strength"]);
    }

    [Fact]
    public void Format_version_is_eight()
    {
        Assert.Equal(10, WorldSaveData.FormatVersion);
    }
}
