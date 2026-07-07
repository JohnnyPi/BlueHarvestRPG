using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content.Definitions;

namespace Game.Content;

public static class BiomeRulesHash
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static uint Compute(BiomeRulesDefinition rules)
    {
        string json = JsonSerializer.Serialize(rules, Options);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return BitConverter.ToUInt32(hash, 0);
    }
}
