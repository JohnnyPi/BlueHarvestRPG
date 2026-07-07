using Game.Client.Input;

namespace Game.Client.UI;

public sealed class UiInputResult
{
    public bool InputConsumed { get; set; }
    public bool BlockWorldClick { get; set; }
    public bool RequestCloseTopScreen { get; set; }
    public bool RequestOpenPauseMenu { get; set; }
    public bool RequestToggleInventory { get; set; }
    public bool RequestToggleCharacterSheet { get; set; }
    public bool RequestOpenPauseFromHud { get; set; }

    public void Reset()
    {
        InputConsumed = false;
        BlockWorldClick = false;
        RequestCloseTopScreen = false;
        RequestOpenPauseMenu = false;
        RequestToggleInventory = false;
        RequestToggleCharacterSheet = false;
        RequestOpenPauseFromHud = false;
    }
}
