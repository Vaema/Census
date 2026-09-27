using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Census;

internal class TownNPCInfo
{
    public int Type { get; }
    public LocalizedText Conditions { get; }

    public TownNPCInfo(int type, LocalizedText conditions = null)
    {
        Type = type;
        Conditions = conditions ?? GetUnknownCondition(type);
    }

    private static LocalizedText GetUnknownCondition(int type)
    {
        if (type >= 0 && type < NPCID.Count)
        {
            string key = $"Mods.Census.SpawnConditions.{NPCID.Search.GetName(type)}";
            if (Language.Exists(key))
                return Language.GetText(key);
        }

        return Language.GetText("Mods.Census.SpawnConditions.Unknown");
    }

    internal TownNPCInfo(int type, string conditions)
    {
        Type = type;
        ModNPC modNPC = ModContent.GetModNPC(type);
        Conditions = CensusConfigClient.Instance?.DisableAutoLocalization == true || modNPC is null
            ? GetUnknownCondition(type)
            : modNPC.GetLocalization("Census.SpawnCondition", () => conditions);
    }

    public TownNPCInfo(ModNPC modNPC)
    {
        // No localization provided, use automatic.
        Type = modNPC.Type;

        // Default value is English. Code will register automatically unless disabled.
        Conditions = CensusConfigClient.Instance?.DisableAutoLocalization == true
            ? GetUnknownCondition(Type)
            : modNPC.GetLocalization("Census.SpawnCondition", () => "Conditions unknown");
    }
}