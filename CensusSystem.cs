using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;
using Terraria.UI.Gamepad;

namespace Census;

internal class CensusSystem : ModSystem
{
    internal static CensusSystem instance;
    internal static bool calculated;

    private LocalizedText Next;

    public override void Load()
    {
        instance = this;
        calculated = false;
        Next = Language.GetText(Mod.GetLocalizationKey("Next"));
        IL_Main.UpdateTime_SpawnTownNPCs += Main_UpdateTime_SpawnTownNPCs;
    }

    /* For speeding up testing.
    private void On_Main_UpdateTime_SpawnTownNPCs(On_Main.orig_UpdateTime_SpawnTownNPCs orig) {
        orig();
        Main.checkForSpawns += 200;
    }
    */

    public override void Unload()
    {
        instance = null;
    }

    // Replace with On? Seems to run after everything anyway.
    private void Main_UpdateTime_SpawnTownNPCs(ILContext il)
    {
        var c = new ILCursor(il);
        c.GotoNext(i => i.MatchCall(typeof(NPCLoader), nameof(NPCLoader.CanTownNPCSpawn)));
        c.Index++;
        c.EmitDelegate(() =>
        {
            calculated = true;
            if (Main.dedServ)
            {
                var packet = Mod.GetPacket();
                packet.Write((byte)CensusMessageType.CensusInfo);
                packet.Write(WorldGen.prioritizedTownNPCType);
                packet.Write(Main.townNPCCanSpawn.Length);

                for (int i = 0; i < Main.townNPCCanSpawn.Length; i += 8)
                {
                    var bits = new BitsByte();
                    for (int j = 0; j < 8 && j + i < Main.townNPCCanSpawn.Length; j++)
                        bits[j] = Main.townNPCCanSpawn[j + i];

                    packet.Write(bits);
                }

                packet.Send();
            }
        });
    }

    public override void OnWorldLoad()
    {
        calculated = false;
    }

    string hoverText = "";

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (Main.playerInventory && Main.EquipPage == 1)
        {
            int InventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (InventoryIndex != -1)
            {
                layers.Insert(InventoryIndex + 1, new LegacyGameInterfaceLayer(
                "Census: Census Hover",
                delegate
                {
                    if (!string.IsNullOrEmpty(hoverText))
                    {
                        if (Main.mouseItem.type == ItemID.None)
                        {
                            Vector2 vector = ChatManager.GetStringSize(FontAssets.MouseText.Value, hoverText, Vector2.One);
                            int x = Main.mouseX + 10;
                            int y = Main.mouseY + 10;
                            if (Main.ThickMouse)
                            {
                                x += 6;
                                y += 6;
                            }
                            if (x + vector.X + 4f > Main.screenWidth)
                                x = (int)(Main.screenWidth - vector.X - 4f);
                            if (y + vector.Y + 4f > Main.screenHeight)
                                y = (int)(Main.screenHeight - vector.Y - 4f);
                            Color baseColor = new(Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor);
                            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, hoverText, new Vector2(x, y), baseColor, 0f, Vector2.Zero, Vector2.One, -1f, 2f);
                        }
                    }
                    return true;
                },
                InterfaceScaleType.UI));

                layers.Insert(InventoryIndex, new LegacyGameInterfaceLayer(
                "Census: Census",
                delegate
                {
                    bool unknown = false;
                    int total = UILinkPointNavigator.Shortcuts.NPCS_IconsTotal + 0;
                    float oldInventoryScale = Main.inventoryScale;
                    Main.inventoryScale = 0.85f;

                    int mH = 0;
                    if (Main.mapEnabled)
                    {
                        if (!Main.mapFullscreen && Main.mapStyle == 1)
                            mH = 256;
                        if (mH + Main.instance.RecommendedEquipmentAreaPushUp > Main.screenHeight)
                            mH = Main.screenHeight - Main.instance.RecommendedEquipmentAreaPushUp;
                    }

                    List<TownNPCInfo> canSpawns = [];
                    List<TownNPCInfo> cantSpawns = [];

                    foreach (TownNPCInfo townNPCInfo in realTownNPCsInfos)
                    {
                        bool missing = !NPC.AnyNPCs(townNPCInfo.type);
                        if (missing)
                        {
                            if (WorldGen.prioritizedTownNPCType == townNPCInfo.type)
                                canSpawns.Insert(0, townNPCInfo);
                            else if (Main.townNPCCanSpawn[townNPCInfo.type])
                                canSpawns.Add(townNPCInfo);
                            else
                                cantSpawns.Add(townNPCInfo);
                        }
                    }

                    int drawCount = 0;
                    string text = "";
                    int rowOffsetY = 0;
                    int colOffsetX = 0;

                    var allNotSpawned = canSpawns.Concat(cantSpawns).ToList();
                    for (drawCount = 0; drawCount < total + allNotSpawned.Count; drawCount++)
                    {
                        int drawX = Main.screenWidth - 64 - 28 + colOffsetX;
                        int drawY = (int)(174 + mH + drawCount * 56 * Main.inventoryScale) + rowOffsetY;
                        Color white = new(100, 100, 100, 100);
                        if (drawY > Main.screenHeight - 80)
                        {
                            colOffsetX -= 48;
                            rowOffsetY -= drawY - (174 + mH);
                            drawX = Main.screenWidth - 64 - 28 + colOffsetX;
                            drawY = (int)(174 + mH + drawCount * 56 * Main.inventoryScale) + rowOffsetY;
                        }
                        if (drawCount < total)
                            continue;

                        TownNPCInfo t = allNotSpawned[drawCount - total];
                        int missingNPCType = t.type;
                        int i = NPC.TypeToDefaultHeadIndex(missingNPCType);
                        if (Main.mouseX >= drawX && Main.mouseX <= drawX + TextureAssets.InventoryBack.Value.Width * Main.inventoryScale && Main.mouseY >= drawY && Main.mouseY <= drawY + TextureAssets.InventoryBack.Value.Height * Main.inventoryScale)
                        {
                            Main.mouseText = true;
                            text = Lang.GetNPCNameValue(missingNPCType);
                            if (WorldGen.prioritizedTownNPCType == missingNPCType)
                                text += $"\n{Next.Value}";
                            else
                            {
                                if (!CensusConfigClient.Instance.DisableConditionsText)
                                {
                                    if (unknown)
                                        text += $" - Townspeople spawn during the day";
                                    else
                                        text += $" - {t.conditions.Value}";
                                }
                            }
                        }
                        Texture2D texture = TextureAssets.InventoryBack7.Value;
                        Color white2 = Main.inventoryBack;
                        Main.spriteBatch.Draw(texture, new Vector2(drawX, drawY), new Rectangle(0, 0, TextureAssets.InventoryBack.Value.Width, TextureAssets.InventoryBack.Value.Height), white2, 0f, default, Main.inventoryScale, SpriteEffects.None, 0f);
                        white = Color.White;
                        float scale = 1f;
                        float maxDimension = Math.Min(TextureAssets.NpcHead[i].Value.Width, TextureAssets.NpcHead[i].Value.Height);
                        if (maxDimension > 36f)
                            scale = 36f / maxDimension;
                        Main.spriteBatch.Draw(TextureAssets.NpcHead[i].Value, new Vector2(drawX + 26f * Main.inventoryScale, drawY + 26f * Main.inventoryScale), new Rectangle(0, 0, TextureAssets.NpcHead[i].Value.Width, TextureAssets.NpcHead[i].Value.Height), white, 0f, new Vector2(TextureAssets.NpcHead[i].Value.Width / 2, TextureAssets.NpcHead[i].Value.Height / 2), scale, SpriteEffects.None, 0f);

                        ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.ItemStack.Value, !calculated ? "?" : Main.townNPCCanSpawn[missingNPCType] ? "✓" : "X", new Vector2(drawX + 26f * Main.inventoryScale, drawY + 26f * Main.inventoryScale) + new Vector2(6f, 6f), !calculated ? Color.MediumPurple : Main.townNPCCanSpawn[missingNPCType] ? Color.LightGreen : Color.LightSalmon, 0f, Vector2.Zero, new Vector2(0.7f));
                    }
                    hoverText = text;

                    Main.inventoryScale = oldInventoryScale;
                    return true;
                },
                InterfaceScaleType.UI));

                layers.Insert(InventoryIndex, new LegacyGameInterfaceLayer(
                "Census: Census Arrows",
                delegate
                {
                    if (UILinkPointNavigator.Shortcuts.NPCS_LastHovered > -1 && CensusConfigClient.Instance.ShowLocatingArrow)
                    {
                        var npc = Main.npc[UILinkPointNavigator.Shortcuts.NPCS_LastHovered];
                        var headIndex = NPC.TypeToDefaultHeadIndex(npc.type); // If NPCS_LastHovered is 0, it could also be the housing query button.
                        Vector2 playerCenter = Main.LocalPlayer.Center + new Vector2(0, Main.LocalPlayer.gfxOffY);
                        var vector = npc.Center - playerCenter;
                        var distance = vector.Length();
                        if (headIndex > -1 && distance > 40)
                        {
                            var headTexture = TextureAssets.NpcHead[headIndex].Value;
                            var offset = Vector2.Normalize(vector) * Math.Min(70, distance - 20);
                            float rotation = vector.ToRotation() + (float)(3 * Math.PI / 4);
                            var drawPosition = playerCenter - Main.screenPosition + offset;
                            float fade = Math.Min(1f, (distance - 20) / 70);
                            Main.spriteBatch.Draw(TextureAssets.Cursors[0].Value, drawPosition, null, CensusConfigClient.Instance.ArrowColor * fade, rotation, TextureAssets.Cursors[1].Value.Size() / 2, new Vector2(1.5f), SpriteEffects.None, 0);

                            drawPosition -= Vector2.Normalize(vector) * 20;

                            Main.spriteBatch.Draw(headTexture, drawPosition, null, Color.White * fade, 0, headTexture.Size() / 2, Vector2.One, npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
                        }
                    }
                    return true;
                },
                InterfaceScaleType.Game));
            }
        }
    }

    internal List<TownNPCInfo> realTownNPCsInfos;
    readonly List<TownNPCInfo> modTownNPCsInfos = [];
    public override void PostAddRecipes()
    {
        // By this point, all NPCs of all mods are loaded.
        realTownNPCsInfos =
        [
            new(NPCID.Guide),
            new(NPCID.Merchant),
            new(NPCID.Nurse),
            new(NPCID.Demolitionist),
            new(NPCID.DyeTrader),
            new(NPCID.Angler),
            new(NPCID.BestiaryGirl),
            new(NPCID.Dryad),
            new(NPCID.Painter),
            new(NPCID.Golfer),
            new(NPCID.ArmsDealer),
            new(NPCID.DD2Bartender),
            new(NPCID.Stylist),
            new(NPCID.GoblinTinkerer),
            new(NPCID.WitchDoctor),
            new(NPCID.Clothier),
            new(NPCID.Mechanic),
            new(NPCID.PartyGirl),
            new(NPCID.Wizard),
            new(NPCID.TaxCollector),
            new(NPCID.Truffle),
            new(NPCID.Pirate),
            new(NPCID.Steampunker),
            new(NPCID.Cyborg),
            new(NPCID.SantaClaus),
            new(NPCID.Princess),
            new(NPCID.TownCat),
            new(NPCID.TownDog),
            new(NPCID.TownBunny),
            new(NPCID.TownSlimeBlue),
            new(NPCID.TownSlimeGreen),
            new(NPCID.TownSlimeOld),
            new(NPCID.TownSlimePurple),
            new(NPCID.TownSlimeRainbow),
            new(NPCID.TownSlimeRed),
            new(NPCID.TownSlimeYellow),
            new(NPCID.TownSlimeCopper),
        ];

        foreach (ModNPC npc in ModContent.GetContent<ModNPC>())
        {
            if (npc.NPC.townNPC && NPC.TypeToDefaultHeadIndex(npc.NPC.type) >= 0 && !npc.TownNPCStayingHomeless)
            {
                var modSuppliedTownNPC = modTownNPCsInfos.FirstOrDefault(x => x.type == npc.NPC.type);
                if (modSuppliedTownNPC != null)
                    realTownNPCsInfos.Add(modSuppliedTownNPC);
                else
                    realTownNPCsInfos.Add(new TownNPCInfo(npc));
            }
        }
    }

    internal void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var msgType = (CensusMessageType)reader.ReadByte();
        switch (msgType)
        {
            case CensusMessageType.CensusInfo:
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    return;

                WorldGen.prioritizedTownNPCType = reader.ReadInt32();
                int count = reader.ReadInt32();
                if (count != Main.townNPCCanSpawn.Length)
                    Mod.Logger.Error("Census: Somehow Main.townNPCCanSpawn.Length incorrect");

                for (int i = 0; i < Main.townNPCCanSpawn.Length; i += 8)
                {
                    BitsByte bits = reader.ReadByte();
                    for (int j = 0; j < 8 && j + i < Main.townNPCCanSpawn.Length; j++)
                        Main.townNPCCanSpawn[j + i] = bits[j];
                }

                calculated = true;
                break;
            default:
                Mod.Logger.Warn("Ceusus: Unknown Message type: " + msgType);
                break;
        }
    }

    public object Call(params object[] args)
    {
        try
        {
            // Where should other mods call? They could call at end of Load?
            string message = args[0] as string;
            if (message == "TownNPCCondition")
            {
                int type = Convert.ToInt32(args[1]);
                if (args.Length >= 3 && args[2] is string conditionString)
                {
                    modTownNPCsInfos.Add(new TownNPCInfo(type, conditionString));
                    throw new Exception($"Call Error: The 2nd parameter of TownNPCCondition is now LocalizedText and is optional. Also, localization is now automatic, keys will appear in your hjson files. This TownNPCCondition Mod.Call is only needed if using LocalizedText.WithFormatArgs");
                }

                LocalizedText condition = args[2] as LocalizedText;
                modTownNPCsInfos.Add(new TownNPCInfo(type, condition));
                return "Success";
            }
            else
                Mod.Logger.Error("Census Call Error: Unknown Message: " + message);
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Census Call Error: " + e.StackTrace + e.Message);
        }
        return "Failure";
    }
}