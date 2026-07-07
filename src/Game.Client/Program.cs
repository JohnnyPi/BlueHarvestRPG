using Game.Client.Debugging;

try
{
    DebugMode.EnableFromArgs(args);
    using var game = new Game.Client.BlueHarvestGame();
    game.Run();
}
catch (Exception ex)
{
    DebugLog.Initialize();
    DebugLog.Exception(ex, "Unhandled exception");
    throw;
}
