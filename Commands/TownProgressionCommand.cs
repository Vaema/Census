using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Census.Commands;

internal class TownProgressionCommand : ModCommand
{
    public override CommandType Type => CommandType.Console;

    public override string Command => "TownProgress";

    public override string Description => "View Town Progression";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (CensusSystem.instance?.realTownNPCsInfos is not { } townNPCInfos)
            return;

        HashSet<int> presentTownNPCs = [];
        foreach (NPC npc in Main.npc)
        {
            if (npc.active && npc.townNPC)
                presentTownNPCs.Add(npc.type);
        }

        foreach (TownNPCInfo townNPCInfo in townNPCInfos)
        {
            int type = townNPCInfo.Type;
            bool present = presentTownNPCs.Contains(type);
            bool spawnable = type >= 0 && type < Main.townNPCCanSpawn.Length && Main.townNPCCanSpawn[type];
            Console.ForegroundColor = present ? ConsoleColor.Green : (spawnable ? ConsoleColor.Yellow : ConsoleColor.Red);
            Console.WriteLine(Lang.GetNPCNameValue(type));
        }
        Console.ResetColor();
    }
}