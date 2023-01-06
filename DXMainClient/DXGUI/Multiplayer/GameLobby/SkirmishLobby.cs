using ClientCore;
using ClientCore.Statistics;
using ClientGUI;
using DTAClient.Domain;
using DTAClient.Domain.Multiplayer;
using DTAClient.DXGUI.Generic;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    public class SkirmishLobby : GameLobbyBase, ISwitchable
    {
        private const string SETTINGS_PATH = "Client/SkirmishSettings.ini";

        public SkirmishLobby(WindowManager windowManager, TopBar topBar, MapLoader mapLoader, DiscordHandler discordHandler)
            : base(windowManager, "SkirmishLobby", mapLoader, false, discordHandler)
        {
            this.topBar = topBar;
        }

        public event EventHandler Exited;

        TopBar topBar;

        public override void Initialize()
        {
            base.Initialize();

            RandomSeed = new Random().Next();

            //InitPlayerOptionDropdowns(128, 98, 90, 48, 55, new Point(6, 24));
            InitPlayerOptionDropdowns();

            btnLeaveGame.Text = "主菜单";

            //MapPreviewBox.EnableContextMenu = true;

            ddPlayerSides[0].AddItem("观察者", AssetLoader.LoadTexture("观察者icon.png"));

            MapPreviewBox.LocalStartingLocationSelected += MapPreviewBox_LocalStartingLocationSelected;
            MapPreviewBox.StartingLocationApplied += MapPreviewBox_StartingLocationApplied;

            InitializeWindow();

            WindowManager.CenterControlOnScreen(this);

            LoadSettings();

            CheckDisallowedSides();

            CopyPlayerDataToUI();

            ProgramConstants.PlayerNameChanged += ProgramConstants_PlayerNameChanged;
            ddPlayerSides[0].SelectedIndexChanged += PlayerSideChanged;

            PlayerExtraOptionsPanel.SetIsHost(true);
        }

        protected override void ToggleFavoriteMap()
        {
            base.ToggleFavoriteMap();

            if (GameModeMap.IsFavorite)
                return;

            RefreshForFavoriteMapRemoved();
        }

        protected override void AddNotice(string message, Color color)
        {
            XNAMessageBox.Show(WindowManager, "消息", message);
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            base.OnEnabledChanged(sender, args);
            if (Enabled)
                UpdateDiscordPresence(true);
            else
                ResetDiscordPresence();
        }

        private void ProgramConstants_PlayerNameChanged(object sender, EventArgs e)
        {
            Players[0].Name = ProgramConstants.PLAYERNAME;
            CopyPlayerDataToUI();
        }

        private void MapPreviewBox_StartingLocationApplied(object sender, EventArgs e)
        {
            CopyPlayerDataToUI();
        }

        private void MapPreviewBox_LocalStartingLocationSelected(object sender, LocalStartingLocationEventArgs e)
        {
            Players[0].StartingLocation = e.StartingLocationIndex + 1;
            CopyPlayerDataToUI();
        }

        private string CheckGameValidity()
        {
            int totalPlayerCount = Players.Count(p => p.SideId < ddPlayerSides[0].Items.Count - 1)
                + AIPlayers.Count;

            if(GameMode == null)
            {
                return "请选择一张地图.";
            }

            if (GameMode.MultiplayerOnly)
            {
                return GameMode.UIName + " 只能在CnCNet和LAN上游玩.";
            }

            if (GameMode.MinPlayersOverride > -1 && totalPlayerCount < GameMode.MinPlayersOverride)
            {
                return GameMode.UIName + " 不能玩少于 " + GameMode.MinPlayersOverride + " 人.";
            }

            if (Map.MultiplayerOnly)
            {
                return "所选地图只能在CnCNet和LAN上游玩.";
            }

            if (totalPlayerCount < Map.MinPlayers)
            {
                return "所选的地图不能少于 " + Map.MinPlayers + " 人.";
            }

            if (Map.EnforceMaxPlayers)
            {
                if (totalPlayerCount > Map.MaxPlayers)
                {
                    return "所选的地图不能多于 " + Map.MaxPlayers + " 人.";
                }

                IEnumerable<PlayerInfo> concatList = Players.Concat(AIPlayers);

                foreach (PlayerInfo pInfo in concatList)
                {
                    if (pInfo.StartingLocation == 0)
                        continue;

                    if (concatList.Count(p => p.StartingLocation == pInfo.StartingLocation) > 1)
                    {
                        return "多个玩家不能在所选地图上共享相同的起始位置.";
                    }
                }
            }

            if (Map.IsCoop && Players[0].SideId == ddPlayerSides[0].Items.Count - 1)
            {
                return "合作任务不能观看。在这里，你必须表现出更多的努力才能胜利.";
            }

            var teamMappingsError = GetTeamMappingsError();
            if (!string.IsNullOrEmpty(teamMappingsError))
                return teamMappingsError;

            return null;
        }

        protected override void BtnLaunchGame_LeftClick(object sender, EventArgs e)
        {
            string error = CheckGameValidity();

            if (error == null)
            {
                     DelFile(GetDeleteFile());
                foreach (string folder in GetModeFile())
                  CopyDirectory(folder,"./");
                    SaveSettings();
                StartGame();
                return;
            }

            XNAMessageBox.Show(WindowManager, "不能开始游戏", error);
        }

        protected override void BtnLeaveGame_LeftClick(object sender, EventArgs e)
        {
            this.Enabled = false;
            this.Visible = false;

            Exited?.Invoke(this, EventArgs.Empty);

            topBar.RemovePrimarySwitchable(this);
            ResetDiscordPresence();
        }

        private void PlayerSideChanged(object sender, EventArgs e)
        {
            UpdateDiscordPresence();
        }

        protected override void UpdateDiscordPresence(bool resetTimer = false)
        {
            if (discordHandler == null || Map == null || GameMode == null || !Initialized)
                return;

            int playerIndex = Players.FindIndex(p => p.Name == ProgramConstants.PLAYERNAME);
            if (playerIndex >= MAX_PLAYER_COUNT || playerIndex < 0)
                return;

            XNAClientDropDown sideDropDown = ddPlayerSides[playerIndex];
            if (sideDropDown.SelectedItem == null)
                return;

            string side = sideDropDown.SelectedItem.Text;
            string currentState = ProgramConstants.IsInGame ? "In Game" : "Setting Up";

            discordHandler.UpdatePresence(
                Map.Name, GameMode.Name, currentState, side, resetTimer);
        }

        protected override bool AllowPlayerOptionsChange()
        {
            return true;
        }

        protected override int GetDefaultMapRankIndex(GameModeMap gameModeMap)
        {
            return StatisticsManager.Instance.GetSkirmishRankForDefaultMap(gameModeMap.Map.Name, gameModeMap.Map.MaxPlayers);
        }

        protected override void GameProcessExited()
        {
            base.GameProcessExited();

            DdGameModeMapFilter_SelectedIndexChanged(null, EventArgs.Empty); // Refresh ranks

            RandomSeed = new Random().Next();
        }

        public void Open()
        {
            topBar.AddPrimarySwitchable(this);
            Enable();
        }

        public void SwitchOn()
        {
            Enable();
        }

        public void SwitchOff()
        {
            Disable();
        }

        public string GetSwitchName()
        {
            return "遭遇战";
        }

        private void DelFile(List<string> deleteFile)
        {
          //  string resultDirectory = Environment.CurrentDirectory;//目录

            if (deleteFile != null)
            {
                for (int i = 0; i < deleteFile.Count; i++)
                {
                    try
                    {
                        File.Delete(deleteFile[i]);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }
        private void CopyDirectory(string sourceDirPath, string saveDirPath)
        {
            if (sourceDirPath != null)
            {

                if (!Directory.Exists(saveDirPath))
                {
                    Directory.CreateDirectory(saveDirPath);
                }
                string[] files = Directory.GetFiles(sourceDirPath);
                foreach (string file in files)
                {
                    string pFilePath = saveDirPath + "\\" + Path.GetFileName(file);
                    File.Copy(file, pFilePath, true);
                }
            }
        }
        /// <summary>
        /// Saves skirmish settings to an INI file on the file system.
        /// </summary>
        private void SaveSettings()
        {
      

            try
            {
         
                File.Delete(ProgramConstants.GamePath + SETTINGS_PATH);
                var skirmishSettingsIni = new IniFile(ProgramConstants.GamePath + SETTINGS_PATH);
      
                skirmishSettingsIni.SetStringValue("Player", "Info", Players[0].ToString());


                for (int i = 0; i < AIPlayers.Count; i++)
                {
                    skirmishSettingsIni.SetStringValue("AIPlayers", i.ToString(), AIPlayers[i].ToString());
                }

                skirmishSettingsIni.SetStringValue("Settings", "Map", Map.SHA1);
                skirmishSettingsIni.SetStringValue("Settings", "GameModeMapFilter", ddGameModeMapFilter.SelectedItem?.Text);

                if (ClientConfiguration.Instance.SaveSkirmishGameOptions)
                {
                    foreach (GameLobbyDropDown dd in DropDowns)
                    {

                        skirmishSettingsIni.SetStringValue("GameOptions", dd.Name, dd.UserSelectedIndex + "");

                    }

                    foreach (GameLobbyCheckBox cb in CheckBoxes)
                    {
                        skirmishSettingsIni.SetStringValue("GameOptions", cb.Name, cb.Checked.ToString());
                    }
                }

                skirmishSettingsIni.WriteIniFile();
            }
            catch (Exception ex)
            {
                Logger.Log("Saving skirmish settings failed! Reason: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads skirmish settings from an INI file on the file system.
        /// </summary>
        private void LoadSettings()
        {
            if (!File.Exists(ProgramConstants.GamePath + SETTINGS_PATH))
            {
                InitDefaultSettings();
                return;
            }

            var skirmishSettingsIni = new IniFile(ProgramConstants.GamePath + SETTINGS_PATH);

            string gameModeMapFilterName = skirmishSettingsIni.GetStringValue("Settings", "GameModeMapFilter", string.Empty);
            if (string.IsNullOrEmpty(gameModeMapFilterName))
                gameModeMapFilterName = skirmishSettingsIni.GetStringValue("Settings", "GameMode", string.Empty); // legacy

            var gameModeMapFilter = ddGameModeMapFilter.Items.Find(i => i.Text == gameModeMapFilterName)?.Tag as GameModeMapFilter;
            if (gameModeMapFilter == null || !gameModeMapFilter.Any())
                gameModeMapFilter = GetDefaultGameModeMapFilter();

            var gameModeMap = gameModeMapFilter.GetGameModeMaps().First();

            if (gameModeMap != null)
            {
                GameModeMap = gameModeMap;

                ddGameModeMapFilter.SelectedIndex = ddGameModeMapFilter.Items.FindIndex(i => i.Tag == gameModeMapFilter);

                string mapSHA1 = skirmishSettingsIni.GetStringValue("Settings", "Map", string.Empty);

                int gameModeMapIndex = gameModeMapFilter.GetGameModeMaps().FindIndex(gmm => gmm.Map.SHA1 == mapSHA1);

                if (gameModeMapIndex > -1)
                {
                    lbGameModeMapList.SelectedIndex = gameModeMapIndex;

                    while (gameModeMapIndex > lbGameModeMapList.LastIndex)
                        lbGameModeMapList.TopIndex++;
                }
            }
            else
                LoadDefaultGameModeMap();

            var player = PlayerInfo.FromString(skirmishSettingsIni.GetStringValue("Player", "Info", string.Empty));

            if (player == null)
            {
                Logger.Log("Failed to load human player information from skirmish settings!");
                InitDefaultSettings();
                return;
            }

            CheckLoadedPlayerVariableBounds(player);

            player.Name = ProgramConstants.PLAYERNAME;
            Players.Add(player);

            List<string> keys = skirmishSettingsIni.GetSectionKeys("AIPlayers");

            if (keys == null)
            {
                keys = new List<string>(); // No point skip parsing all settings if only AI info is missing.
                //Logger.Log("AI player information doesn't exist in skirmish settings!");
                //InitDefaultSettings();
                //return;
            }

            bool AIAllowed = !(Map.MultiplayerOnly || GameMode.MultiplayerOnly) || !(Map.HumanPlayersOnly || GameMode.HumanPlayersOnly);
            foreach (string key in keys)
            {
                if (!AIAllowed) break;
                var aiPlayer = PlayerInfo.FromString(skirmishSettingsIni.GetStringValue("AIPlayers", key, string.Empty));

                CheckLoadedPlayerVariableBounds(aiPlayer, true);

                if (aiPlayer == null)
                {
                    Logger.Log("Failed to load AI player information from skirmish settings!");
                    InitDefaultSettings();
                    return;
                }

                if (AIPlayers.Count < MAX_PLAYER_COUNT - 1)
                    AIPlayers.Add(aiPlayer);
            }

            if (ClientConfiguration.Instance.SaveSkirmishGameOptions)
            {
                foreach (GameLobbyDropDown dd in DropDowns)
                {
                    // Maybe we should build an union of the game mode and map
                    // forced options, we'd have less repetitive code that way

                    if (GameMode != null)
                    {
                        int gameModeMatchIndex = GameMode.ForcedDropDownValues.FindIndex(p => p.Key.Equals(dd.Name));
                        if (gameModeMatchIndex > -1)
                        {
                            Logger.Log("Dropdown '" + dd.Name + "' has forced value in gamemode - saved settings ignored.");
                            continue;
                        }
                    }

                    if (Map != null)
                    {
                        int gameModeMatchIndex = Map.ForcedDropDownValues.FindIndex(p => p.Key.Equals(dd.Name));
                        if (gameModeMatchIndex > -1)
                        {
                            Logger.Log("Dropdown '" + dd.Name + "' has forced value in map - saved settings ignored.");
                            continue;
                        }
                    }

                    dd.UserSelectedIndex = skirmishSettingsIni.GetIntValue("GameOptions", dd.Name, dd.UserSelectedIndex);

                    if (dd.UserSelectedIndex > -1 && dd.UserSelectedIndex < dd.Items.Count)
                        dd.SelectedIndex = dd.UserSelectedIndex;
                }

                foreach (GameLobbyCheckBox cb in CheckBoxes)
                {
                    if (GameMode != null)
                    {
                        int gameModeMatchIndex = GameMode.ForcedCheckBoxValues.FindIndex(p => p.Key.Equals(cb.Name));
                        if (gameModeMatchIndex > -1)
                        {
                            Logger.Log("Checkbox '" + cb.Name + "' has forced value in gamemode - saved settings ignored.");
                            continue;
                        }
                    }

                    if (Map != null)
                    {
                        int gameModeMatchIndex = Map.ForcedCheckBoxValues.FindIndex(p => p.Key.Equals(cb.Name));
                        if (gameModeMatchIndex > -1)
                        {
                            Logger.Log("Checkbox '" + cb.Name + "' has forced value in map - saved settings ignored.");
                            continue;
                        }
                    }

                    cb.Checked = skirmishSettingsIni.GetBooleanValue("GameOptions", cb.Name, cb.Checked);
                }
            }
        }

        /// <summary>
        /// Checks that a player's color, team and starting location
        /// don't exceed allowed bounds.
        /// </summary>
        /// <param name="pInfo">The PlayerInfo.</param>
        private void CheckLoadedPlayerVariableBounds(PlayerInfo pInfo, bool isAIPlayer = false)
        {
            int sideCount = SideCount + RandomSelectorCount;
            if (isAIPlayer) sideCount--;

            if (pInfo.SideId < 0 || pInfo.SideId > sideCount)
            {
                pInfo.SideId = 0;
            }

            if (pInfo.ColorId < 0 || pInfo.ColorId > MPColors.Count)
            {
                pInfo.ColorId = 0;
            }

            if (pInfo.TeamId < 0 || pInfo.TeamId >= ddPlayerTeams[0].Items.Count ||
                !Map.IsCoop && (Map.ForceNoTeams || GameMode.ForceNoTeams))
            {
                pInfo.TeamId = 0;
            }

            if (pInfo.StartingLocation < 0 || pInfo.StartingLocation > MAX_PLAYER_COUNT ||
                !Map.IsCoop && (Map.ForceRandomStartLocations || GameMode.ForceRandomStartLocations))
            {
                pInfo.StartingLocation = 0;
            }
        }

        private void InitDefaultSettings()
        {
            Players.Clear();
            AIPlayers.Clear();

            Players.Add(new PlayerInfo(ProgramConstants.PLAYERNAME, 0, 0, 0, 0));
            PlayerInfo aiPlayer = new PlayerInfo("Easy AI", 0, 0, 0, 0);
            aiPlayer.IsAI = true;
            aiPlayer.AILevel = 2;
            AIPlayers.Add(aiPlayer);

            LoadDefaultGameModeMap();
        }

        protected override void UpdateMapPreviewBoxEnabledStatus()
        {
            MapPreviewBox.EnableContextMenu = !((Map != null && Map.ForceRandomStartLocations) || (GameMode != null && GameMode.ForceRandomStartLocations) || GetPlayerExtraOptions().IsForceRandomStarts);
            MapPreviewBox.EnableStartLocationSelection = MapPreviewBox.EnableContextMenu;
        }
    }
}
