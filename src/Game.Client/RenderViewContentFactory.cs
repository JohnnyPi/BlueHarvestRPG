using Game.Content;
using Game.Content.Definitions;
using Game.Simulation.Items;
using Game.Simulation.Rendering;

namespace Game.Client;

public static class RenderViewContentFactory
{
    public static RenderViewContent Create(GameContentBundle bundle)
    {
        var quests = bundle.Quests.Quests.ToDictionary(
            quest => quest.Id,
            quest => (quest.Title, quest.Objective, quest.Target));

        var itemNames = new Dictionary<int, string>();
        foreach (ItemDefinition item in bundle.Items.Items)
        {
            if (Enum.TryParse(item.Id, ignoreCase: true, out ItemId itemId))
            {
                itemNames[(int)itemId] = item.Name;
            }
        }

        var attributes = bundle.CharacterDefaults.Attributes
            .Select(attribute => (attribute.Id, attribute.Name))
            .ToList();

        return new RenderViewContent
        {
            Quests = quests,
            ItemDisplayNames = itemNames,
            AttributeDefinitions = attributes
        };
    }
}
