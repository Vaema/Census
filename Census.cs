using System.IO;
using Terraria.ModLoader;

namespace Census;

internal class Census : Mod
{
    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        CensusSystem.instance.HandlePacket(reader, whoAmI);
    }

    public override object Call(params object[] args)
    {
        if (CensusSystem.instance == null)
        {
            Logger.Error("Call was called before CensusSystem class loaded. Make sure to only use Call during or after Mod.PostSetupContent.");
            return "Failure";
        }
        return CensusSystem.instance.Call(args);
    }
}

enum CensusMessageType : byte
{
    CensusInfo
}