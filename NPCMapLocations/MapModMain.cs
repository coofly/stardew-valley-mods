﻿/*
NPC Map Locations Mod by Bouhm.
Shows NPC locations real-time on the map.
*/

using StardewValley;
using StardewValley.Quests;
using StardewModdingAPI;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace NPCMapLocations
{
    public class MapModMain : Mod
    {
        public static ISemanticVersion current = new SemanticVersion("1.4.6");
        public static ISemanticVersion latest = new SemanticVersion("1.4.6");
        public static IModHelper modHelper;
        public static MapModConfig config;
        // public static string saveFile;
        public static int customNpcId = 0;
        public static int menuOpen = 0;
        private static Dictionary<string, Dictionary<string, int>> customNPCs;
        private static Dictionary<string, NPCMarker> npcMarkers = new Dictionary<string, NPCMarker>();
        // NPC head crops, top left corner (0, y), width = 16, height = 15 
        public static Dictionary<string, int> spriteCrop;
        private static Dictionary<string, string> startingLocations;
        private static Dictionary<string, Double[]> locationVectors;
        private static Dictionary<string, string> indoorLocations;
        private static MapPageTooltips toolTips;
        private static string hoveredNPCNames;
        private static HashSet<string> birthdayNPCs;
        private static HashSet<string> questNPCs;
        private static HashSet<string> hiddenNPCs;
        private static Dictionary<string, string> npcNames = new Dictionary<string, string>();
        private bool[] showExtras = new Boolean[4];
        private bool loadComplete = false;
        private bool initialized = false;

        public override void Entry(IModHelper helper)
        {
            modHelper = helper;
            config = helper.ReadConfig<MapModConfig>();
            SaveEvents.AfterLoad += SaveEvents_AfterLoad;
            GameEvents.UpdateTick += GameEvents_UpdateTick;
            GraphicsEvents.OnPostRenderGuiEvent += GraphicsEvents_OnPostRenderGuiEvent;
            ControlEvents.KeyPressed += KeyboardInput_KeyDown;
        }

        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            // Task.Run(() => MapModVersionChecker.getNotification()).GetAwaiter().GetResult();
            // saveFile = Game1.player.name.Replace(" ", String.Empty) + "_" + Game1.uniqueIDForThisGame;
            spriteCrop = MapModConstants.spriteCrop;
            startingLocations = MapModConstants.startingLocations;
            locationVectors = MapModConstants.locationVectors;
            indoorLocations = MapModConstants.indoorLocations;
            customNPCs = config.customNPCs;
            loadComplete = true;
        }

        private void loadCustomMods()
        {
            var initializeCustomNPCs = 1;
            if (customNPCs != null && customNPCs.Count != 0)
            {
                initializeCustomNPCs = 0;
            }
            int id = 1;
            foreach (NPC npc in Utility.getAllCharacters())
            {
                id = loadCustomNPCs(npc, initializeCustomNPCs, id);
                loadNPCCrop(npc);
                loadCustomNames(npc);
            }
            config.customNPCs = customNPCs;
            modHelper.WriteConfig(config);
            initialized = true;
        }

        private int loadCustomNPCs(NPC npc, int initialize, int id)
        {
            if (initialize == 0)
            {
                int idx = 1;
                // Update save files for custom NPC installed or uninstalled (pseudo-persave config)
                foreach (KeyValuePair<string, Dictionary<string, int>> customNPC in customNPCs)
                {
                    // isInGame = 0;
                    if (npc.name.Equals(customNPC.Key))
                    {
                        // isInGame = 1;
                        customNpcId = idx;
                    }

                    /*
                    // Pseudo-persave config for custom NPC (since custom NPCs have to be installed to each save file)
                    // Works too unreliably; remove for now;
                    if (!customNPC.Value.ContainsKey(saveFile))
                    {
                        customNPC.Value.Add(saveFile, isInGame);
                    }
                    else
                    {
                        customNPC.Value[saveFile] = isInGame;
                    }
                    */
                    if (!customNPC.Value.ContainsKey("crop"))
                    {
                        customNPC.Value.Add("crop", 0);
                    }
                    if (!spriteCrop.ContainsKey(customNPC.Key))
                    {
                        spriteCrop.Add(customNPC.Key, customNPC.Value["crop"]);
                    }
                    idx++;
                }
            } 
            else
            {
                if (npc.Schedule != null && isCustomNPC(npc.name))
                {
                    if (!customNPCs.ContainsKey(npc.name))
                    {
                        var npcEntry = new Dictionary<string, int>();
                        npcEntry.Add("id", id);
                        npcEntry.Add("crop", 0);
                        /*
                        if (npc != null)
                        {
                            npcEntry.Add(saveFile, 1);
                        }
                        else
                        {
                            npcEntry.Add(saveFile, 0);
                        }
                        */
                        customNPCs.Add(npc.name, npcEntry);
                        spriteCrop.Add(npc.name, 0);
                        id++;
                    }
                }
            }
            return id;
        }

        private void loadCustomNames(NPC npc)
        {
            if (!npcNames.ContainsKey(npc.name))
            {
                var customName = npc.getName();
                if (string.IsNullOrEmpty(customName))
                {
                    customName = npc.name;
                }
                npcNames.Add(npc.name, customName);
            }
        }

        private void loadNPCCrop(NPC npc)
        {
            if (config.villagerCrop != null && config.villagerCrop.Count > 0)
            {
                foreach (KeyValuePair<string, int> villager in config.villagerCrop)
                {
                    if (npc.name.Equals(villager.Key))
                    {
                        spriteCrop[npc.name] = villager.Value;
                    }
                }
            }
        }

        // Open menu key
        private void KeyboardInput_KeyDown(object sender, EventArgsKeyPressed e)
        {
            if (Game1.hasLoadedGame && Game1.activeClickableMenu is GameMenu)
            {
                changeKey(e.KeyPressed.ToString(), (GameMenu)Game1.activeClickableMenu);
            }
        }

        private void changeKey(string key, GameMenu menu)
        {
            if (menu.currentTab != 3) { return; }
            if (key.Equals(config.menuKey))
            {
                Game1.activeClickableMenu = new MapModMenu(Game1.viewport.Width / 2 - (950 + IClickableMenu.borderWidth * 2) / 2, Game1.viewport.Height / 2 - (750 + IClickableMenu.borderWidth * 2) / 2, 900 + IClickableMenu.borderWidth * 2, 650 + IClickableMenu.borderWidth * 2, showExtras, customNPCs, npcNames);
                menuOpen = 1;
            }
        }

        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            if (!Game1.hasLoadedGame) { return; }
            if (loadComplete && !initialized)
            {
                loadCustomMods();
            }
            if (!(Game1.activeClickableMenu is GameMenu)) { return; }

            List<string> hoveredList = new List<String>();
            birthdayNPCs = new HashSet<string>();
            questNPCs = new HashSet<string>();
            hiddenNPCs = new HashSet<string>();
            hoveredNPCNames = "";

            foreach (NPC npc in Utility.getAllCharacters())
            {
                if (npc.Schedule != null || npc.isMarried() || npc.name.Equals("Sandy") || npc.name.Equals("Marlon") || npc.name.Equals("Wizard"))
                {
                    bool sameLocation = false;
                    showExtras[0] = Game1.player.mailReceived.Contains("ccVault");
                    showExtras[1] = Game1.stats.DaysPlayed >= 5u;
                    showExtras[2] = Game1.stats.DaysPlayed >= 5u;
                    showExtras[3] = Game1.year >= 2;

                    if (config.onlySameLocation)
                    {
                        string indoorLocationNPC;
                        string indoorLocationPlayer;
                        indoorLocations.TryGetValue(npc.currentLocation.name, out indoorLocationNPC);
                        indoorLocations.TryGetValue(Game1.player.currentLocation.name, out indoorLocationPlayer);
                        if (indoorLocationPlayer == null || indoorLocationNPC == null)
                        {
                            sameLocation = false;
                        }
                        else
                        {
                            sameLocation = indoorLocationNPC.Equals(indoorLocationPlayer);
                        }
                    }

                    if ((config.immersionOption == 2 && !Game1.player.hasTalkedToFriendToday(npc.name)) ||
                        (config.immersionOption == 3 && Game1.player.hasTalkedToFriendToday(npc.name)) ||
                        (config.onlySameLocation && !sameLocation) ||
                        (config.byHeartLevel && !(Game1.player.getFriendshipHeartLevelForNPC(npc.name) >= config.heartLevelMin && Game1.player.getFriendshipHeartLevelForNPC(npc.name) <= config.heartLevelMax)))
                    {
                        hiddenNPCs.Add(npc.name);
                    }


                    if (config.showHiddenVillagers ? showNPC(npc.name, showExtras) : (!hiddenNPCs.Contains(npc.name) && showNPC(npc.name, showExtras)))
                    {
                        int offsetX = 0;
                        int offsetY = 0;
                        int x = 0;
                        int y = 0;
                        int width = 0;
                        int height = 0;
                        double[] npcLocation;
                        string currentLocation;
                        // At the start of a new game, for some reason NPCs locations are null until 6:30 AM
                        if (npc.currentLocation == null)
                        {
                            currentLocation = startingLocations[npc.name];
                        }
                        else
                        {
                            currentLocation = npc.currentLocation.name;
                        }

                        locationVectors.TryGetValue(currentLocation, out npcLocation);
                        // Catch location error
                        if (npcLocation == null)
                        {
                            double[] unknown = { -5000, -5000, 0, 0 };
                            npcLocation = unknown;
                        }

                        double mapScaleX = npcLocation[2];
                        double mapScaleY = npcLocation[3];

                        // Partitioning large areas
                        // In addition to all the locations on the map, all of these values were meticulously calculated to make
                        // real-time tracking accurate. DO NOT MESS WITH THESE (UNLESS IMPROVEMENTS CAN BE MADE)

                        // Partitions for Town
                        if (currentLocation.Equals("Town"))
                        {
                            if (npc.getTileX() < 28 && npc.getTileY() < 58 && npc.getTileY() > 53)
                            {
                                offsetX = 5;
                                offsetY = -30;
                            }
                            else if (npc.getTileX() < 31 && npc.getTileX() > 26 && npc.getTileY() > 74 && npc.getTileY() < 90)
                            {
                                offsetX = 10;
                            }
                            else if (npc.getTileX() < 30 && npc.getTileY() > 89 && npc.getTileY() < 98)
                            {
                                offsetY = -5;
                                offsetX = 5;
                            }
                            else if (npc.getTileX() < 57 && npc.getTileY() > 98 && npc.getTileY() < 109)
                            {
                                offsetX = 30;
                            }
                            else if (npc.getTileX() < 78 && npc.getTileY() < 103 && npc.getTileY() > 40)
                            {
                                mapScaleX = 3.01;
                                mapScaleY = 2.94;
                                offsetY = -10;
                            }
                            else if (npc.getTileX() < 85 && npc.getTileY() < 43)
                            {
                                mapScaleX = 2.48;
                                mapScaleY = 2.52;
                                offsetX = -15;
                            }

                            else if (npc.getTileX() > 90 && npc.getTileY() < 41)
                            {
                                offsetX = -20;
                                offsetY = 25;
                            }
                            else if (npc.getTileX() > 77 && npc.getTileY() < 61)
                            {
                                mapScaleX = 3.21;
                                mapScaleY = 2.64;
                                offsetX = -3;
                                offsetY = -3;
                            }
                            else if (npc.getTileX() > 78 && npc.getTileY() > 60)
                            {
                                mapScaleX = 3.21;
                                mapScaleY = 3.34;
                                offsetX = -22;
                                offsetY = -35;
                            }
                        }

                        // Partitions for Forest ------------------------------------------------------------------------------------
                        else if (currentLocation.Equals("Forest"))
                        {
                            if (Game1.player.getTileX() < 20)
                            {
                                mapScaleX = 3.152;
                                mapScaleY = 1.82;
                                offsetX = 47;
                                offsetY = -35;
                            }
                            else if (npc.getTileX() < 66 && npc.getTileY() < 51)
                            {
                                mapScaleX = 3.152;
                                mapScaleY = 1.82;
                                offsetX = 50;
                                offsetY = -10;
                            }
                            else if (npc.getTileX() > 60 && npc.getTileX() < 90 && npc.getTileY() < 23)
                            {
                                mapScaleX = 2.152;
                                mapScaleY = 1.82;
                                offsetX = 110;
                            }
                            else if (npc.getTileX() < 74 && npc.getTileY() < 49)
                            {
                                mapScaleX = 3.152;
                                mapScaleY = 1.82;
                                offsetX = 30;
                            }
                            else if (npc.getTileX() < 120 && npc.getTileY() < 52)
                            {
                                mapScaleX = 3.2;
                                mapScaleY = 1.8;
                                offsetX = 15;
                                offsetY = -10;
                            }
                            else if (npc.getTileX() < 120 && npc.getTileY() < 101)
                            {
                                mapScaleX = 2.101;
                                mapScaleY = 2.208;
                            }
                        }

                        // Partitions for Beach ------------------------------------------------------------------------------------
                        else if (currentLocation.Equals("Beach"))
                        {
                            if (npc.getTileY() < 7)
                            {
                                offsetX = -50;
                                offsetY = 10;
                            }
                            else if (npc.getTileX() < 39 && npc.getTileY() < 22)
                            {
                                mapScaleX = 1.21;
                                mapScaleY = 2.33;
                                offsetX = -20;
                            }
                            else if (npc.getTileX() < 58 && npc.getTileX() > 28 && npc.getTileY() < 27)
                            {
                                mapScaleX = 1.11;
                                mapScaleY = 2.33;
                                offsetX = 15;
                            }

                            else if (npc.getTileX() < 58 && npc.getTileY() < 37)
                            {
                                mapScaleX = 2.745;
                                mapScaleY = 2.833;
                                offsetX = -20;
                            }
                        }

                        // Partitions for Mountain ------------------------------------------------------------------------------------
                        else if (currentLocation.Equals("Mountain"))
                        {
                            if (npc.getTileX() < 41 && npc.getTileY() < 16)
                            {
                                mapScaleX = 2.9;
                                mapScaleY = 2.46;
                                offsetX = -10;

                            }
                            else if (npc.getTileX() < 41 && npc.getTileY() < 41)
                            {
                                mapScaleX = 2.9;
                                mapScaleY = 1.825;
                            }
                            else if (npc.getTileX() < 61 && npc.getTileY() < 41)
                            {
                                mapScaleX = 2.5;
                                mapScaleY = 2.3;
                            }
                        }

                        x = (int)(((Game1.activeClickableMenu.xPositionOnScreen - 160) + (4 + npcLocation[0] + npc.getTileX() * mapScaleX + offsetX)));
                        y = (int)(((Game1.activeClickableMenu.yPositionOnScreen - 20) + (5 + npcLocation[1] + npc.getTileY() * mapScaleY + offsetY)));
                        width = 32;
                        height = 30;

                        if (npcMarkers.ContainsKey(npc.name))
                        {
                            npcMarkers[npc.name].position = new Rectangle(x, y, width, height);
                        }
                        else
                        {
                            npcMarkers.Add(npc.name, new NPCMarker(npc.sprite.Texture, new Rectangle(x, y, width, height)));
                        }

                        if (Game1.getMouseX() >= x + 2 && Game1.getMouseX() <= x - 2 + width && Game1.getMouseY() >= y + 2 && Game1.getMouseY() <= y - 2 + height)
                        {
                            if (npcNames.ContainsKey(npc.name))
                            {
                                hoveredList.Add(npcNames[npc.name]);
                            }
                        }

                        if (config.markQuests)
                        {
                            if (npc.isBirthday(Game1.currentSeason, Game1.dayOfMonth))
                            {
                                if (Game1.player.friendships.ContainsKey(npc.name) && Game1.player.friendships[npc.name][3] != 1)
                                {
                                    birthdayNPCs.Add(npc.name);
                                }
                                else
                                {
                                    birthdayNPCs.Add(npc.name);
                                }
                            }
                            foreach (Quest quest in Game1.player.questLog)
                            {
                                if (quest.accepted && quest.dailyQuest && !quest.completed)
                                {
                                    if (quest.questType == 3)
                                    {
                                        var current = (ItemDeliveryQuest)quest;
                                        if (current.target == npc.name)
                                        {
                                            questNPCs.Add(npc.name);
                                        }
                                    }
                                    else if (quest.questType == 4)
                                    {
                                        var current = (SlayMonsterQuest)quest;
                                        if (current.target == npc.name)
                                        {
                                            questNPCs.Add(npc.name);
                                        }
                                    }
                                    else if (quest.questType == 7)
                                    {
                                        var current = (FishingQuest)quest;
                                        if (current.target == npc.name)
                                        {
                                            questNPCs.Add(npc.name);
                                        }
                                    }
                                    else if (quest.questType == 10)
                                    {
                                        var current = (ResourceCollectionQuest)quest;
                                        if (current.target == npc.name)
                                        {
                                            questNPCs.Add(npc.name);
                                        }
                                    }
                                }
                            }
                        }
                        // Draw order
                        if (hiddenNPCs.Contains(npc.name))
                        {
                            npcMarkers[npc.name].layer = 4;
                            if (questNPCs.Contains(npc.name) || (birthdayNPCs.Contains(npc.name)))
                            {
                                npcMarkers[npc.name].layer = 3;
                            }
                        }
                        else
                        {
                            npcMarkers[npc.name].layer = 2;
                            if (questNPCs.Contains(npc.name) || (birthdayNPCs.Contains(npc.name)))
                            {
                                npcMarkers[npc.name].layer = 1;
                            }
                        }
                    }
                    else
                    {
                        npcMarkers.Remove(npc.name);
                    }
                }
            }

            if (hoveredList != null && hoveredList.Count > 0)
            {
                hoveredNPCNames = hoveredList[0];
                for (int i = 1; i < hoveredList.Count; i++)
                {
                    var lines = hoveredNPCNames.Split('\n');
                    if ((int)Game1.smallFont.MeasureString(lines[lines.Length - 1] + ", " + hoveredList[i]).X > (int)Game1.smallFont.MeasureString("Home of Robin, Demetrius, Sebastian & Maru").X)
                    {
                        hoveredNPCNames += ", " + Environment.NewLine;
                        hoveredNPCNames += hoveredList[i];
                    }
                    else
                    {
                        hoveredNPCNames += ", " + hoveredList[i];
                    }
                };
            }
            toolTips = new MapPageTooltips(hoveredNPCNames, npcNames, config.nameTooltipMode);
        }

        // Draw event (when Map Page is opened)
        private void GraphicsEvents_OnPostRenderGuiEvent(object sender, EventArgs e)
        {
            if (Game1.hasLoadedGame && Game1.activeClickableMenu is GameMenu)
            {
                drawMarkers((GameMenu)Game1.activeClickableMenu);
            }
        }

        private void GraphicsEvents_OnPreRenderGuiEvent(object sender, EventArgs e)
        {
        }

        // Actual draw event
        static void drawMarkers(GameMenu menu)
        {
            if (menu.currentTab == 3)
            {
                /*
                // Player testing
                int offsetX = 0;
                int offsetY = 0;
                int x = 0;
                int y = 0;
                int width = 0;
                int height = 0;
                double[] npcLocation;
                string currentLocation = Game1.player.currentLocation.name;

                locationVectors.TryGetValue(currentLocation, out npcLocation);
                if (npcLocation == null)
                {
                    double[] unknown = { -5000, -5000, 0, 0 };
                    npcLocation = unknown;
                }
                double mapScaleX = npcLocation[2];
                double mapScaleY = npcLocation[3];

                // Partitions for Town
                if (currentLocation.Equals("Town"))
                {
                    if (Game1.player.getTileX() < 28 && Game1.player.getTileY() < 58 && Game1.player.getTileY() > 53)
                    {
                        offsetX = 5;
                        offsetY = -30;
                    }
                    else if (Game1.player.getTileX() < 31 && Game1.player.getTileX() > 26 && Game1.player.getTileY() > 74 && Game1.player.getTileY() < 90)
                    {
                        offsetX = 10;
                    }
                    else if (Game1.player.getTileX() < 30 && Game1.player.getTileY() > 89 && Game1.player.getTileY() < 98)
                    {
                        offsetY = -5;
                        offsetX = 5;
                    }
                    else if (Game1.player.getTileX() < 57 && Game1.player.getTileY() > 98 && Game1.player.getTileY() < 109)
                    {
                        offsetX = 30;
                    }
                    else if (Game1.player.getTileX() < 78 && Game1.player.getTileY() < 103 && Game1.player.getTileY() > 40)
                    {
                        mapScaleX = 3.01;
                        mapScaleY = 2.94;
                        offsetY = -10;
                    }
                    else if (Game1.player.getTileX() < 85 && Game1.player.getTileY() < 43)
                    {
                        mapScaleX = 2.48;
                        mapScaleY = 2.52;
                        offsetX = -15;
                    }

                    else if (Game1.player.getTileX() > 90 && Game1.player.getTileY() < 41)
                    {
                        offsetX = -20;
                        offsetY = 25;
                    }
                    else if (Game1.player.getTileX() > 77 && Game1.player.getTileY() < 61)
                    {
                        mapScaleX = 3.21;
                        mapScaleY = 2.64;
                        offsetX = -3;
                        offsetY = -3;
                    }
                    else if (Game1.player.getTileX() > 78 && Game1.player.getTileY() > 60)
                    {
                        mapScaleX = 3.21;
                        mapScaleY = 3.34;
                        offsetX = -22;
                        offsetY = -35;
                    }
                }

                // Partitions for Forest ------------------------------------------------------------------------------------
                else if (currentLocation.Equals("Forest"))
                {
                    if (Game1.player.getTileX() < 20)
                    {
                        mapScaleX = 3.152;
                        mapScaleY = 1.82;
                        offsetX = 47;
                        offsetY = -35;
                    }
                    else if (Game1.player.getTileX() < 66 && Game1.player.getTileY() < 51)
                    {
                        mapScaleX = 3.152;
                        mapScaleY = 1.82;
                        offsetX = 50;
                        offsetY = -10;
                    }
                    else if (Game1.player.getTileX() > 60 && Game1.player.getTileX() < 90 && Game1.player.getTileY() < 23)
                    {
                        mapScaleX = 2.152;
                        mapScaleY = 1.82;
                        offsetX = 110;
                    }
                    else if (Game1.player.getTileX() < 74 && Game1.player.getTileY() < 49)
                    {
                        mapScaleX = 3.152;
                        mapScaleY = 1.82;
                        offsetX = 30;
                    }
                    else if (Game1.player.getTileX() < 120 && Game1.player.getTileY() < 52)
                    {
                        mapScaleX = 3.2;
                        mapScaleY = 1.8;
                        offsetX = 15;
                        offsetY = -10;
                    }
                    else if (Game1.player.getTileX() < 120 && Game1.player.getTileY() < 101)
                    {
                        mapScaleX = 2.101;
                        mapScaleY = 2.208;
                    }
                }

                // Partitions for Beach ------------------------------------------------------------------------------------
                else if (currentLocation.Equals("Beach"))
                {
                    if (Game1.player.getTileY() < 7)
                    {
                        offsetX = -50;
                        offsetY = 10;
                    }
                    else if (Game1.player.getTileX() < 39 && Game1.player.getTileY() < 22)
                    {
                        mapScaleX = 1.21;
                        mapScaleY = 2.33;
                        offsetX = -20;
                    }
                    else if (Game1.player.getTileX() < 58 && Game1.player.getTileX() > 28 && Game1.player.getTileY() < 27)
                    {
                        mapScaleX = 1.11;
                        mapScaleY = 2.33;
                        offsetX = 15;
                    }

                    else if (Game1.player.getTileX() < 58 && Game1.player.getTileY() < 37)
                    {
                        mapScaleX = 2.745;
                        mapScaleY = 2.833;
                        offsetX = -20;
                    }
                }

                // Partitions for Mountain ------------------------------------------------------------------------------------
                else if (currentLocation.Equals("Mountain"))
                {
                    if (Game1.player.getTileX() < 41 && Game1.player.getTileY() < 16)
                    {
                        mapScaleX = 2.9;
                        mapScaleY = 2.46;
                        offsetX = -10;

                    }
                    else if (Game1.player.getTileX() < 41 && Game1.player.getTileY() < 41)
                    {
                        mapScaleX = 2.9;
                        mapScaleY = 1.825;
                    }
                    else if (Game1.player.getTileX() < 61 && Game1.player.getTileY() < 41)
                    {
                        mapScaleX = 2.5;
                        mapScaleY = 2.3;
                    }
                }
                Log.Verbose(Game1.player.currentLocation.name + ", " + Game1.player.getTileX() + ", " + Game1.player.getTileY());
                x = (int)(((Game1.activeClickableMenu.xPositionOnScreen - 160) + (4 + npcLocation[0] + Game1.player.getTileX() * mapScaleX + offsetX)));
                y = (int)(((Game1.activeClickableMenu.yPositionOnScreen - 20) + (5 + npcLocation[1] + Game1.player.getTileY() * mapScaleY + offsetY)));
                width = 32;
                height = 30;
                Game1.spriteBatch.Draw(Game1.getCharacterFromName("Abigail").sprite.Texture, 
                                       new Rectangle(x, y, width, height), 
                                       new Rectangle?(new Rectangle(0, 3, 16, 15)), 
                                       Color.White);
                */

                if (config.showTravelingMerchant && (Game1.dayOfMonth == 5 || Game1.dayOfMonth == 7 || Game1.dayOfMonth == 12 || Game1.dayOfMonth == 14 || Game1.dayOfMonth == 19 || Game1.dayOfMonth == 21 || Game1.dayOfMonth == 26 || Game1.dayOfMonth == 28))
                {
                    Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(Game1.activeClickableMenu.xPositionOnScreen + 130, Game1.activeClickableMenu.yPositionOnScreen + 355), new Rectangle?(new Rectangle(191, 1410, 22, 21)), Color.White, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 1f);
                }
                var sortedMarkers = npcMarkers.ToList();
                sortedMarkers.Sort((y, x) => x.Value.layer.CompareTo(y.Value.layer));
                foreach (KeyValuePair<string, NPCMarker> npc in sortedMarkers)
                {
                    if (hiddenNPCs.Contains(npc.Key)) {
                        Game1.spriteBatch.Draw(npc.Value.marker, npc.Value.position, new Rectangle?(new Rectangle(0, spriteCrop[npc.Key], 16, 15)), Color.DimGray * 0.9f);
                        if (birthdayNPCs.Contains(npc.Key))
                        {
                            Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(npc.Value.position.X + 20, npc.Value.position.Y), new Rectangle?(new Rectangle(147, 412, 10, 11)), Color.DimGray * 0.9f, 0f, Vector2.Zero, 1.8f, SpriteEffects.None, 0f);
                        }
                        if (questNPCs.Contains(npc.Key))
                        {
                            Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(npc.Value.position.X + 22, npc.Value.position.Y - 3), new Rectangle?(new Rectangle(403, 496, 5, 14)), Color.DimGray * 0.9f, 0f, Vector2.Zero, 1.8f, SpriteEffects.None, 0f);
                        }

                    }
                    else
                    {
                        Game1.spriteBatch.Draw(npc.Value.marker, npc.Value.position, new Rectangle?(new Rectangle(0, spriteCrop[npc.Key], 16, 15)), Color.White * 0.9f);
                        if (birthdayNPCs.Contains(npc.Key))
                        {
                            Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(npc.Value.position.X + 20, npc.Value.position.Y), new Rectangle?(new Rectangle(147, 412, 10, 11)), Color.White, 0f, Vector2.Zero, 1.8f, SpriteEffects.None, 0f);
                        }
                        if (questNPCs.Contains(npc.Key))
                        {
                            Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(npc.Value.position.X + 22, npc.Value.position.Y - 3), new Rectangle?(new Rectangle(403, 496, 5, 14)), Color.White, 0f, Vector2.Zero, 1.8f, SpriteEffects.None, 0f);
                        }

                    }
                }
                toolTips.draw(Game1.spriteBatch);
                if (!Game1.options.hardwareCursor) {
                    Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2((float)Game1.getOldMouseX(), (float)Game1.getOldMouseY()), new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, (Game1.options.gamepadControls ? 44 : 0), 16, 16)), Color.White, 0f, Vector2.Zero, ((float)Game1.pixelZoom + Game1.dialogueButtonScale / 150f), SpriteEffects.None, 1f);
                }
            }
        }

        // Config show/hide 
        private static bool showNPC(string npc, bool[] showExtras)
        {
            if (npc.Equals("Abigail")) { return config.showAbigail; }
            if (npc.Equals("Alex")) { return config.showAlex; }
            if (npc.Equals("Caroline")) { return config.showCaroline; }
            if (npc.Equals("Clint")) { return config.showClint; }
            if (npc.Equals("Demetrius")) { return config.showDemetrius; }
            if (npc.Equals("Elliott")) { return config.showElliott; }
            if (npc.Equals("Emily")) { return config.showEmily; }
            if (npc.Equals("Evelyn")) { return config.showEvelyn; }
            if (npc.Equals("George")) { return config.showGeorge; }
            if (npc.Equals("Gus")) { return config.showGus; }
            if (npc.Equals("Haley")) { return config.showHaley; }
            if (npc.Equals("Harvey")) { return config.showHarvey; }
            if (npc.Equals("Jas")) { return config.showJas; }
            if (npc.Equals("Jodi")) { return config.showJodi; }
            if (npc.Equals("Kent")) { return config.showKent; }
            if (npc.Equals("Leah")) { return config.showLeah; }
            if (npc.Equals("Lewis")) { return config.showLewis; }
            if (npc.Equals("Linus")) { return config.showLinus; }
            if (npc.Equals("Marnie")) { return config.showMarnie; }
            if (npc.Equals("Maru")) { return config.showMaru; }
            if (npc.Equals("Pam")) { return config.showPam; }
            if (npc.Equals("Penny")) { return config.showPenny; }
            if (npc.Equals("Pierre")) { return config.showPierre; }
            if (npc.Equals("Robin")) { return config.showRobin; }
            if (npc.Equals("Sam")) { return config.showSam; }
            if (npc.Equals("Sebastian")) { return config.showSebastian; }
            if (npc.Equals("Shane")) { return config.showShane; }
            if (npc.Equals("Vincent")) { return config.showVincent; }
            if (npc.Equals("Willy")) { return config.showWilly; }
            if (npc.Equals("Sandy")) { return config.showSandy && showExtras[0]; }
            if (npc.Equals("Marlon")) { return config.showMarlon && showExtras[1]; }
            if (npc.Equals("Wizard")) { return config.showWizard && showExtras[2]; }
            foreach (KeyValuePair<string, Dictionary<string, int>> customNPC in customNPCs)
            {
                if (customNPC.Key.Equals(npc))
                {
                    switch (customNPC.Value["id"])
                    {
                        case 1:
                            return config.showCustomNPC1;
                        case 2:
                            return config.showCustomNPC2;
                        case 3:
                            return config.showCustomNPC3;
                        case 4:
                            return config.showCustomNPC4;
                        case 5:
                            return config.showCustomNPC5;
                    }
                }
            }

            return true;
        }

        // Only checks against existing villager names
        public static bool isCustomNPC(string npc)
        {
            return (!(
                npc.Equals("Abigail") ||
                npc.Equals("Alex") ||
                npc.Equals("Caroline") ||
                npc.Equals("Clint") ||
                npc.Equals("Demetrius") ||
                npc.Equals("Elliott") ||
                npc.Equals("Emily") ||
                npc.Equals("Evelyn") ||
                npc.Equals("George") ||
                npc.Equals("Gus") ||
                npc.Equals("Haley") ||
                npc.Equals("Harvey") ||
                npc.Equals("Jas") ||
                npc.Equals("Jodi") ||
                npc.Equals("Kent") ||
                npc.Equals("Leah") ||
                npc.Equals("Lewis") ||
                npc.Equals("Linus") ||
                npc.Equals("Marnie") ||
                npc.Equals("Maru") ||
                npc.Equals("Pam") ||
                npc.Equals("Penny") ||
                npc.Equals("Pierre") ||
                npc.Equals("Robin") ||
                npc.Equals("Sam") ||
                npc.Equals("Sebastian") ||
                npc.Equals("Shane") ||
                npc.Equals("Vincent") ||
                npc.Equals("Willy") ||
                npc.Equals("Sandy") ||
                npc.Equals("Marlon") ||
                npc.Equals("Wizard"))
            );
        }
    }

    // Class for NPC markers
    public class NPCMarker
    {
        public Texture2D marker;
        public Rectangle position;
        public int layer;

        public NPCMarker(Texture2D marker, Rectangle position)
        {
            this.marker = marker;
            this.position = position;
            this.layer = 4;
        }
    }

    // For drawing tooltips
    public class MapPageTooltips : IClickableMenu
    {
        private string hoverText = "";
        private Texture2D map;
        private int mapX;
        private int mapY;
        private List<ClickableComponent> points = new List<ClickableComponent>();
        private string names;
        private Dictionary<string, string> npcNames;
        private int nameTooltipMode;

        public MapPageTooltips(string names, Dictionary<string, string> npcNames, int nameTooltipMode)
        {
            this.nameTooltipMode = nameTooltipMode;
            this.names = names;
            this.npcNames = npcNames;
            this.map = Game1.content.Load<Texture2D>("LooseSprites\\map");
            Vector2 topLeftPositionForCenteringOnScreen = Utility.getTopLeftPositionForCenteringOnScreen(this.map.Bounds.Width * Game1.pixelZoom, 180 * Game1.pixelZoom, 0, 0);
            this.mapX = (int)topLeftPositionForCenteringOnScreen.X;
            this.mapY = (int)topLeftPositionForCenteringOnScreen.Y;
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX, this.mapY, 292, 152), Game1.player.mailReceived.Contains("ccVault") ? "卡利科沙漠" : "???"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 324, this.mapY + 252, 188, 132), Game1.player.farmName + "农庄"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 360, this.mapY + 96, 188, 132), "边远森林"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 516, this.mapY + 224, 76, 100), "公交车站"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 196, this.mapY + 352, 36, 76), "法师塔"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 420, this.mapY + 392, 76, 40), "玛妮的牧场" + Environment.NewLine + "通常营业时间：上午9点-下午6点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 452, this.mapY + 436, 32, 24), "莉亚的小屋"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 612, this.mapY + 396, 36, 52), "柳巷1号" + Environment.NewLine + "乔迪、肯特、山姆和文森特的家"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 652, this.mapY + 408, 40, 36), "柳巷2号" + Environment.NewLine + "艾米丽和海莉的家"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 672, this.mapY + 340, 44, 60), "城镇广场"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 680, this.mapY + 304, 16, 32), "哈维的诊所" + Environment.NewLine + "营业时间：上午9点-下午3点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 696, this.mapY + 296, 28, 40), string.Concat(new string[]
            {
                "皮埃尔的杂货店",
                Environment.NewLine,
                "皮埃尔和卡罗琳、阿比盖尔的家",
                Environment.NewLine,
                "营业时间：上午9点-晚上9点 (周三休息)"
            })));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 852, this.mapY + 388, 80, 36), "铁匠铺" + Environment.NewLine + "营业时间：上午9点-下午4点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 716, this.mapY + 352, 28, 40), "星之果实餐吧" + Environment.NewLine + "营业时间：中午12点-凌晨0点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 768, this.mapY + 388, 44, 56), "镇长的庄园"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 892, this.mapY + 416, 32, 28), "星露谷图书博物馆" + Environment.NewLine + "营业时间：上午8点-下午6点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 824, this.mapY + 564, 28, 20), "艾利欧特的小屋"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 696, this.mapY + 448, 24, 20), "下水道"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 724, this.mapY + 424, 40, 32), "墓地"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 780, this.mapY + 360, 24, 20), "拖车" + Environment.NewLine + "潘姆和潘妮的家"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 748, this.mapY + 316, 36, 36), "河路1号" + Environment.NewLine + "乔治、艾芙琳和亚历克斯的家"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 732, this.mapY + 148, 48, 32), string.Concat(new string[]
            {
                "木匠商店",
                Environment.NewLine,
                "罗宾，德米特里厄斯，塞巴斯蒂安和玛鲁的家",
                Environment.NewLine,
                "通常营业时间：上午9点-晚上8点"
            })));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 784, this.mapY + 128, 12, 16), "帐篷"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 880, this.mapY + 96, 16, 24), "矿场"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 900, this.mapY + 108, 32, 36), (Game1.stats.DaysPlayed >= 5u) ? ("冒险家公会" + Environment.NewLine + "营业时间：下午2点-晚上10点") : "???"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 968, this.mapY + 116, 88, 76), Game1.player.mailReceived.Contains("ccCraftsRoom") ? "采石场" : "???"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 872, this.mapY + 280, 52, 52), "Joja超市" + Environment.NewLine + "营业时间：上午9点-晚上11点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 844, this.mapY + 608, 36, 40), "鱼店" + Environment.NewLine + "营业时间：上午9点-下午5点"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 576, this.mapY + 60, 48, 36), Game1.isLocationAccessible("Railroad") ? ("温泉" + Environment.NewLine + "全天开放") : "???"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX, this.mapY + 272, 196, 176), Game1.player.mailReceived.Contains("beenToWoods") ? "秘密森林" : "???"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 260, this.mapY + 572, 20, 20), "被遗弃的房屋"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 692, this.mapY + 204, 44, 36), "社区中心"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 380, this.mapY + 596, 24, 32), "下水管道"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 644, this.mapY + 64, 16, 8), Game1.isLocationAccessible("Railroad") ? "铁路" : "???"));
            this.points.Add(new ClickableComponent(new Rectangle(this.mapX + 728, this.mapY + 652, 28, 28), "孤独的石头"));
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            foreach (ClickableComponent current in this.points)
            {
                string name = current.name;
                if (name == "Lonely Stone")
                {
                    Game1.playSound("stoneCrack");
                }
            }
            if (Game1.activeClickableMenu != null)
            {
                (Game1.activeClickableMenu as GameMenu).changeTab(0);
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
        }

        public override void performHoverAction(int x, int y)
        {
            this.hoverText = "";
            foreach (ClickableComponent current in this.points)
            {
                if (current.containsPoint(x, y))
                {
                    this.hoverText = current.name;
                    return;
                }
            }
        }

        // Draw location tooltips
        public override void draw(SpriteBatch b)
        {
            int x = Game1.getMouseX() + Game1.tileSize / 2;
            int y = Game1.getMouseY() + Game1.tileSize / 2;
            int offsetY = 0;
            this.performHoverAction(x - Game1.tileSize / 2, y - Game1.tileSize / 2);

            if (!this.hoverText.Equals(""))
            {
                int textLength = (int)Game1.smallFont.MeasureString(hoverText).X + Game1.tileSize / 2;
                foreach (KeyValuePair<string, string> customName in npcNames)
                {
                    this.hoverText = this.hoverText.Replace(customName.Key, customName.Value);
                }
                int width = Math.Max((int)Game1.smallFont.MeasureString(hoverText).X + Game1.tileSize / 2, textLength);
                int height = (int)Math.Max(60, Game1.smallFont.MeasureString(hoverText).Y + Game1.tileSize / 2);
                if (x + width > Game1.viewport.Width)
                {
                    x = Game1.viewport.Width - width;
                    y += Game1.tileSize / 4;
                }
                if (this.nameTooltipMode == 1)
                {
                    if (y + height > Game1.viewport.Height)
                    {
                        x += Game1.tileSize / 4;
                        y = Game1.viewport.Height - height;
                    }
                    offsetY = 4 - Game1.tileSize;
                }
                else if (this.nameTooltipMode == 2)
                {
                    if (y + height > Game1.viewport.Height)
                    {
                        x += Game1.tileSize / 4;
                        y = Game1.viewport.Height - height;
                    }
                    offsetY = height - 4;
                }
                else
                {
                    if (y + height > Game1.viewport.Height)
                    {
                        x += Game1.tileSize / 4;
                        y = Game1.viewport.Height - height;
                    }
                }

                drawNPCNames(Game1.spriteBatch, this.names, x, y, offsetY, height, this.nameTooltipMode);
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, width, height, Color.White, 1f, false);
                b.DrawString(Game1.smallFont, hoverText, new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)) + new Vector2(2f, 2f), Game1.textShadowColor);
                b.DrawString(Game1.smallFont, hoverText, new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)) + new Vector2(0f, 2f), Game1.textShadowColor);
                b.DrawString(Game1.smallFont, hoverText, new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)) + new Vector2(2f, 0f), Game1.textShadowColor);
                b.DrawString(Game1.smallFont, hoverText, new Vector2((float)(x + Game1.tileSize / 4), (float)(y + Game1.tileSize / 4 + 4)), Game1.textColor * 0.9f);
            }
            else
            {
                drawNPCNames(Game1.spriteBatch, this.names, x, y, offsetY, height, this.nameTooltipMode);
            }
        }

        // Draw NPC names in bottom left corner of map page
        public static void drawNPCNames(SpriteBatch b, string names, int x, int y, int offsetY, int relocate, int nameTooltipMode)
        {
            if (!(names.Equals("")))
            {
                var lines = names.Split('\n');
                int height = (int)Math.Max(60, Game1.smallFont.MeasureString(names).Y + Game1.tileSize / 2);
                int width = (int)Game1.smallFont.MeasureString(names).X + Game1.tileSize / 2;

                if (nameTooltipMode == 1)
                {
                    x = Game1.getOldMouseX() + Game1.tileSize / 2;
                    if (lines.Length > 1)
                    {
                        y += offsetY - ((int)Game1.smallFont.MeasureString(names).Y) + Game1.tileSize / 2;
                    }
                    else
                    {
                        y += offsetY;
                    }
                    // If going off screen on the right, move tooltip to below location tooltip so it can stay inside the screen
                    // without the cursor covering the tooltip
                    if (x + width > Game1.viewport.Width)
                    {
                        x = Game1.viewport.Width - width;
                        if (lines.Length > 1)
                        {
                            y += relocate - 8 + ((int)Game1.smallFont.MeasureString(names).Y) + Game1.tileSize / 2;
                        }
                        else
                        {
                            y += relocate - 8 + Game1.tileSize;
                        }
                    }
                }
                else if (nameTooltipMode == 2)
                {
                    y += offsetY;
                    if (x + width > Game1.viewport.Width)
                    {
                        x = Game1.viewport.Width - width;
                    }
                    // If going off screen on the bottom, move tooltip to above location tooltip so it stays visible
                    if (y + height > Game1.viewport.Height)
                    {
                        x = Game1.getOldMouseX() + Game1.tileSize / 2;
                        if (lines.Length > 1)
                        {
                            y += -relocate + 8 - ((int)Game1.smallFont.MeasureString(names).Y) + Game1.tileSize / 2;
                        }
                        else
                        {
                            y += -relocate + 8 - Game1.tileSize;
                        }
                    }
                }
                else
                {
                    x = Game1.activeClickableMenu.xPositionOnScreen - 145;
                    y = Game1.activeClickableMenu.yPositionOnScreen + 625 - height / 2;
                }

                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y, width, height, Color.White, 1f, true);
                Vector2 vector = new Vector2(x + (float)(Game1.tileSize / 4), y + (float)(Game1.tileSize / 4 + 4));
                b.DrawString(Game1.smallFont, names, vector + new Vector2(2f, 2f), Game1.textShadowColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                b.DrawString(Game1.smallFont, names, vector + new Vector2(0f, 2f), Game1.textShadowColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                b.DrawString(Game1.smallFont, names, vector + new Vector2(2f, 0f), Game1.textShadowColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                b.DrawString(Game1.smallFont, names, vector, Game1.textColor * 0.9f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }
    }
}
