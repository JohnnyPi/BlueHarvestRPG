namespace Game.Simulation.Items;

public enum ItemId
{
    None = 0,
    Wood = 1,
    Berry = 2,
    Rope = 3
}

public readonly record struct ItemStack(ItemId ItemId, int Count);

public sealed class Inventory
{
    private readonly List<ItemStack> _stacks = [];

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    public void Add(ItemStack stack)
    {
        if (stack.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < _stacks.Count; i++)
        {
            if (_stacks[i].ItemId == stack.ItemId)
            {
                _stacks[i] = _stacks[i] with { Count = _stacks[i].Count + stack.Count };
                return;
            }
        }

        _stacks.Add(stack);
    }

    public bool TryRemove(ItemId itemId, int count)
    {
        for (int i = 0; i < _stacks.Count; i++)
        {
            if (_stacks[i].ItemId != itemId)
            {
                continue;
            }

            if (_stacks[i].Count < count)
            {
                return false;
            }

            int remaining = _stacks[i].Count - count;
            if (remaining == 0)
            {
                _stacks.RemoveAt(i);
            }
            else
            {
                _stacks[i] = _stacks[i] with { Count = remaining };
            }

            return true;
        }

        return false;
    }
}
