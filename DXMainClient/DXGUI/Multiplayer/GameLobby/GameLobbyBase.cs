﻿using ClientCore;
using ClientCore.Statistics;
using ClientGUI;
using DTAClient.Domain;
using DTAClient.Domain.Multiplayer;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.Online.EventArguments;
using Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    /// <summary>
    /// A generic base for all game lobbies (Skirmish, LAN and CnCNet).
    /// Contains the common logic for parsing game options and handling player info.
    /// </summary>
    ///
    public abstract class GameLobbyBase : XNAWindow
    {

        protected const int MAX_PLAYER_COUNT = 8;
        protected const int PLAYER_OPTION_VERTICAL_MARGIN = 12;
        protected const int PLAYER_OPTION_HORIZONTAL_MARGIN = 3;
        protected const int PLAYER_OPTION_CAPTION_Y = 6;
        private const int DROP_DOWN_HEIGHT = 21;
        protected const string BTN_LAUNCH_GAME = "开始游戏";
        protected const string BTN_LAUNCH_READY = "我准备好了";
        protected const string BTN_LAUNCH_NOT_READY = "未准备";

        private const string FavoriteMapsLabel = "收藏的地图";

        private const int RANK_NONE = 0;
        private const int RANK_EASY = 1;
        private const int RANK_MEDIUM = 2;
        private const int RANK_HARD = 3;

        /// <summary>
        /// Creates a new instance of the game lobby base.
        /// </summary>
        /// <param name="windowManager"></param>
        /// <param name="iniName">The name of the lobby in GameOptions.ini.</param>
        /// <param name="mapLoader"></param>
        /// <param name="isMultiplayer"></param>
        /// <param name="discordHandler"></param>
        public GameLobbyBase(
            WindowManager windowManager,
            string iniName,
            MapLoader mapLoader,
            bool isMultiplayer,
            DiscordHandler discordHandler
        ) : base(windowManager)
        {
            _iniSectionName = iniName;
            MapLoader = mapLoader;
            this.isMultiplayer = isMultiplayer;
            this.discordHandler = discordHandler;
        }

        private string _iniSectionName;

        protected XNAPanel PlayerOptionsPanel;

        protected XNAPanel GameOptionsPanel;

        protected List<MultiplayerColor> MPColors;

        protected List<GameLobbyCheckBox> CheckBoxes = new List<GameLobbyCheckBox>();
        protected List<GameLobbyDropDown> DropDowns = new List<GameLobbyDropDown>();

        protected DiscordHandler discordHandler;

        protected MapLoader MapLoader;
        /// <summary>
        /// The list of multiplayer game mode maps.
        /// Each is an instance of a map for a specific game mode.
        /// </summary>
        protected GameModeMapCollection GameModeMaps => MapLoader.GameModeMaps;

        protected GameModeMapFilter gameModeMapFilter;

        private GameModeMap _gameModeMap;

        /// <summary>
        /// The currently selected game mode.
        /// </summary>
        protected GameModeMap GameModeMap
        {
            get => _gameModeMap;
            set
            {
                var oldGameModeMap = _gameModeMap;
                _gameModeMap = value;
                if (value != null && oldGameModeMap != value)
                    UpdateDiscordPresence();
            }
        }
        protected Map Map => GameModeMap?.Map;
        protected GameMode GameMode => GameModeMap?.GameMode;

        protected XNAClientDropDown[] ddPlayerNames;
        protected XNAClientDropDown[] ddPlayerSides;
        protected XNAClientDropDown[] ddPlayerColors;
        protected XNAClientDropDown[] ddPlayerStarts;
        protected XNAClientDropDown[] ddPlayerTeams;

        protected XNAClientButton btnPlayerExtraOptionsOpen;
        protected PlayerExtraOptionsPanel PlayerExtraOptionsPanel;

        protected XNALabel lblName;
        protected XNALabel lblSide;
        protected XNALabel lblColor;
        protected XNALabel lblStart;
        protected XNALabel lblTeam;

        protected XNAClientButton btnLeaveGame;
        protected GameLaunchButton btnLaunchGame;
        protected XNAClientButton btnPickRandomMap;
        protected XNALabel lblMapName;
        protected XNALabel lblMapAuthor;
        protected XNALabel lblGameMode;
        protected XNALabel lblMapSize;

        protected XNAClientButton btnAginLoadMaps;

        protected MapPreviewBox MapPreviewBox;

        protected XNAMultiColumnListBox lbGameModeMapList;
        protected XNAClientDropDown ddGameModeMapFilter;
        protected XNALabel lblGameModeSelect;
        protected XNAContextMenu mapContextMenu;
        private XNAContextMenuItem toggleFavoriteItem;

        protected XNASuggestionTextBox tbMapSearch;

        protected List<PlayerInfo> Players = new List<PlayerInfo>();
        protected List<PlayerInfo> AIPlayers = new List<PlayerInfo>();

        protected virtual PlayerInfo FindLocalPlayer() => Players.Find(p => p.Name == ProgramConstants.PLAYERNAME);

        protected bool PlayerUpdatingInProgress { get; set; }

        protected Texture2D[] RankTextures;

        /// <summary>
        /// The seed used for randomizing player options.
        /// </summary>
        protected int RandomSeed { get; set; }

        /// <summary>
        /// An unique identifier for this game.
        /// </summary>
        protected int UniqueGameID { get; set; }
        protected int SideCount { get; private set; }
        protected int RandomSelectorCount { get; private set; } = 1;

        protected List<int[]> RandomSelectors = new List<int[]>();

        public List<int> DisallowedPlayerSides = new List<int>();

        private readonly bool isMultiplayer = false;

        private MatchStatistics matchStatistics;

        private bool disableGameOptionUpdateBroadcast = false;

        protected EventHandler<MultiplayerNameRightClickedEventArgs> MultiplayerNameRightClicked;

        /// <summary>
        /// If set, the client will remove all starting waypoints from the map
        /// before launching it.
        /// </summary>
        protected bool RemoveStartingLocations { get; set; } = false;
        protected IniFile GameOptionsIni { get; private set; }

        protected XNAClientButton BtnSaveLoadGameOptions { get; set; }

        private XNAContextMenu loadSaveGameOptionsMenu { get; set; }

        private LoadOrSaveGameOptionPresetWindow loadOrSaveGameOptionPresetWindow;

        private GetRandomMap randomMap;

        public XNAClientButton btnRandomMap;

        public override void Initialize()
        {
            Name = _iniSectionName;
            //if (WindowManager.RenderResolutionY < 800)
            //    ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);
            //else
            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX - 60, WindowManager.RenderResolutionY - 32);
            WindowManager.CenterControlOnScreen(this);
            BackgroundTexture = AssetLoader.LoadTexture("gamelobbybg.png");

            RankTextures = new Texture2D[4]
            {
                AssetLoader.LoadTexture("rankNone.png"),
                AssetLoader.LoadTexture("rankEasy.png"),
                AssetLoader.LoadTexture("rankNormal.png"),
                AssetLoader.LoadTexture("rankHard.png")
            };

            MPColors = MultiplayerColor.LoadColors();

            GameOptionsIni = new IniFile(ProgramConstants.GetBaseResourcePath() + "GameOptions.ini");

            InitializeGameOptionsPanel();

            PlayerOptionsPanel = new XNAPanel(WindowManager);
            PlayerOptionsPanel.Name = "PlayerOptionsPanel";
            PlayerOptionsPanel.ClientRectangle = new Rectangle(GameOptionsPanel.X - 401, 12, 200, GameOptionsPanel.Height);
            PlayerOptionsPanel.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 192), 1, 1);
            PlayerOptionsPanel.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            InitializePlayerExtraOptionsPanel();

            btnAginLoadMaps = new XNAClientButton(WindowManager);
            btnAginLoadMaps.Name = "AginLoadMaps";
            btnAginLoadMaps.ClientRectangle = new Rectangle(10, 10,UIDesignConstants.BUTTON_WIDTH_133-20, UIDesignConstants.BUTTON_HEIGHT);
            btnAginLoadMaps.Text = "刷新地图列表";
            btnAginLoadMaps.IdleTexture = AssetLoader.LoadTexture("133pxtab.png");
            btnAginLoadMaps.HoverTexture = AssetLoader.LoadTexture("133pxtab_c.png");
            btnAginLoadMaps.LeftClick += BtnAginLoadMaps_LeftClick;

            btnLeaveGame = new XNAClientButton(WindowManager);
            btnLeaveGame.Name = "btnLeaveGame";
            btnLeaveGame.ClientRectangle = new Rectangle(Width - 143, Height - 28, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLeaveGame.Text = "离开游戏";
            btnLeaveGame.LeftClick += BtnLeaveGame_LeftClick;

            btnLaunchGame = new GameLaunchButton(WindowManager, RankTextures);
            btnLaunchGame.Name = "btnLaunchGame";
            btnLaunchGame.ClientRectangle = new Rectangle(12, btnLeaveGame.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnLaunchGame.Text = "开始游戏";
            btnLaunchGame.LeftClick += BtnLaunchGame_LeftClick;

            btnRandomMap = new XNAClientButton(WindowManager);
            btnRandomMap.Name = "btnRandomMap";
            btnRandomMap.ClientRectangle= new Rectangle(200, Height-3, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnRandomMap.Text = "生成随机地图";
            btnRandomMap.LeftClick += btnRandomMap_LeftClick;

            MapPreviewBox = new MapPreviewBox(WindowManager, Players, AIPlayers, MPColors,
                GameOptionsIni.GetStringValue("General", "Sides", String.Empty).Split(','),
                GameOptionsIni);
            
            MapPreviewBox.Name = "MapPreviewBox";
            MapPreviewBox.ClientRectangle = new Rectangle(PlayerOptionsPanel.X,
                PlayerOptionsPanel.Bottom + 6,
                GameOptionsPanel.Right - PlayerOptionsPanel.X,
                Height - PlayerOptionsPanel.Bottom - 65);
            MapPreviewBox.FontIndex = 1;
            MapPreviewBox.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            MapPreviewBox.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            MapPreviewBox.ToggleFavorite += MapPreviewBox_ToggleFavorite;

            lblMapName = new XNALabel(WindowManager);
            lblMapName.Name = "lblMapName";
            lblMapName.ClientRectangle = new Rectangle(MapPreviewBox.X,
                MapPreviewBox.Bottom + 3, 0, 0);
            lblMapName.FontIndex = 1;
            lblMapName.Text = "地图:";

          //  lblMapAuthor = new XNALabel(WindowManager);
         //   lblMapAuthor.Name = "lblMapAuthor";
        //    lblMapAuthor.ClientRectangle = new Rectangle(MapPreviewBox.Right,
        //        lblMapName.Y, 0, 0);
        //    lblMapAuthor.FontIndex = 1;
        //    lblMapAuthor.Text = "作者 ";

            lblGameMode = new XNALabel(WindowManager);
            lblGameMode.Name = "lblGameMode";
            lblGameMode.ClientRectangle = new Rectangle(lblMapName.X,
                lblMapName.Bottom + 3, 0, 0);
            lblGameMode.FontIndex = 1;
            lblGameMode.Text = "游戏模式:";

            lblMapSize = new XNALabel(WindowManager);
            lblMapSize.Name = "lblMapSize";
            lblMapSize.ClientRectangle = new Rectangle(lblGameMode.ClientRectangle.X,
                lblGameMode.ClientRectangle.Bottom + 3, 0, 0);
            lblMapSize.FontIndex = 1;
            lblMapSize.Text = "大小: ";

            lblMapSize.Visible = false;

            lbGameModeMapList = new XNAMultiColumnListBox(WindowManager);
            lbGameModeMapList.Name = "lbMapList";  // keep as lbMapList for legacy INI compatibility
            lbGameModeMapList.ClientRectangle = new Rectangle(btnLaunchGame.X, GameOptionsPanel.Y + 23,
                MapPreviewBox.X - btnLaunchGame.X - 6,
                MapPreviewBox.Bottom - 23 - GameOptionsPanel.Y);
            lbGameModeMapList.SelectedIndexChanged += LbGameModeMapList_SelectedIndexChanged;
            lbGameModeMapList.RightClick += LbGameModeMapList_RightClick;
            lbGameModeMapList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbGameModeMapList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 192), 1, 1);
            lbGameModeMapList.LineHeight = 25;
            lbGameModeMapList.DrawListBoxBorders = true;
            lbGameModeMapList.AllowKeyboardInput = true;
            lbGameModeMapList.AllowRightClickUnselect = false;

            mapContextMenu = new XNAContextMenu(WindowManager);
            mapContextMenu.Name = nameof(mapContextMenu);
            mapContextMenu.Width = 100;
            mapContextMenu.AddItem("删除地图", DeleteMapConfirmation, null, CanDeleteMap);
            toggleFavoriteItem = new XNAContextMenuItem
            {
                Text = "收藏",
                SelectAction = ToggleFavoriteMap
            };
            mapContextMenu.AddItem(toggleFavoriteItem);
            AddChild(mapContextMenu);

            XNAPanel rankHeader = new XNAPanel(WindowManager);
            rankHeader.BackgroundTexture = AssetLoader.LoadTexture("rank.png");
            rankHeader.ClientRectangle = new Rectangle(0, 0, rankHeader.BackgroundTexture.Width,
                19);

            XNAListBox rankListBox = new XNAListBox(WindowManager);
            rankListBox.TextBorderDistance = 2;

            lbGameModeMapList.AddColumn(rankHeader, rankListBox);

            lbGameModeMapList.AddColumn("地图名", lbGameModeMapList.Width - RankTextures[1].Width - 3);

            ddGameModeMapFilter = new XNAClientDropDown(WindowManager);
            ddGameModeMapFilter.Name = "ddGameMode"; // keep as ddGameMode for legacy INI compatibility
            ddGameModeMapFilter.ClientRectangle = new Rectangle(lbGameModeMapList.Right - 150, GameOptionsPanel.Y, 150, 21);
            ddGameModeMapFilter.SelectedIndexChanged += DdGameModeMapFilter_SelectedIndexChanged;

            ddGameModeMapFilter.AddItem(CreateGameFilterItem(FavoriteMapsLabel, new GameModeMapFilter(GetFavoriteGameModeMaps)));
            foreach (GameMode gm in GameModeMaps.GameModes)
                ddGameModeMapFilter.AddItem(CreateGameFilterItem(gm.UIName, new GameModeMapFilter(GetGameModeMaps(gm))));

            lblGameModeSelect = new XNALabel(WindowManager);
            lblGameModeSelect.Name = "lblGameModeSelect";
            lblGameModeSelect.ClientRectangle = new Rectangle(lbGameModeMapList.X, ddGameModeMapFilter.Y + 2, 0, 0);
            lblGameModeSelect.FontIndex = 1;
            lblGameModeSelect.Text = "游戏模式:";

            tbMapSearch = new XNASuggestionTextBox(WindowManager);
            tbMapSearch.Name = "tbMapSearch";
            tbMapSearch.ClientRectangle = new Rectangle(lbGameModeMapList.X,
                lbGameModeMapList.Bottom + 3, lbGameModeMapList.Width, 21);
            tbMapSearch.Suggestion = "搜索地图...";
            tbMapSearch.MaximumTextLength = 64;
            tbMapSearch.InputReceived += TbMapSearch_InputReceived;

            btnPickRandomMap = new XNAClientButton(WindowManager);
            btnPickRandomMap.Name = "btnPickRandomMap";
            btnPickRandomMap.ClientRectangle = new Rectangle(btnLaunchGame.Right + 157, btnLaunchGame.Y, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnPickRandomMap.Text = "随机选一个";
            btnPickRandomMap.LeftClick += BtnPickRandomMap_LeftClick;
            btnPickRandomMap.Disable();

            randomMap = new GetRandomMap(WindowManager,MapLoader);
            AddAndInitializeWithControl(WindowManager, randomMap);
            randomMap.Disable();
            randomMap.EnabledChanged += randomMap_EnabledChanged;

            AddChild(lblMapName);
            //AddChild(lblMapAuthor);
            AddChild(lblGameMode);
            AddChild(lblMapSize);
            AddChild(MapPreviewBox);

            AddChild(btnAginLoadMaps);
            AddChild(lbGameModeMapList);
            AddChild(tbMapSearch);
            AddChild(lblGameModeSelect);
            AddChild(ddGameModeMapFilter);

            AddChild(GameOptionsPanel);
            AddChild(BtnSaveLoadGameOptions);
            AddChild(loadSaveGameOptionsMenu);
            AddChild(loadOrSaveGameOptionPresetWindow);

            AddChild(btnRandomMap);
            string[] checkBoxes = GameOptionsIni.GetStringValue(_iniSectionName, "CheckBoxes", String.Empty).Split(',');

            foreach (string chkName in checkBoxes)
            {
                GameLobbyCheckBox chkBox = new GameLobbyCheckBox(WindowManager);
                chkBox.Name = chkName;
                AddChild(chkBox);
                chkBox.GetAttributes(GameOptionsIni);
                CheckBoxes.Add(chkBox);
                chkBox.CheckedChanged += ChkBox_CheckedChanged;
            }

            string[] labels = GameOptionsIni.GetStringValue(_iniSectionName, "Labels", String.Empty).Split(',');

            foreach (string labelName in labels)
            {
                XNALabel label = new XNALabel(WindowManager);
                label.Name = labelName;
                AddChild(label);
                label.GetAttributes(GameOptionsIni);
            }

            string[] dropDowns = GameOptionsIni.GetStringValue(_iniSectionName, "DropDowns", String.Empty).Split(',');

            foreach (string ddName in dropDowns)
            {
                GameLobbyDropDown dropdown = new GameLobbyDropDown(WindowManager);
                dropdown.Name = ddName;
                AddChild(dropdown);
                dropdown.GetAttributes(GameOptionsIni);
                DropDowns.Add(dropdown);
                dropdown.SelectedIndexChanged += Dropdown_SelectedIndexChanged;
            }

            AddChild(PlayerOptionsPanel);
            AddChild(PlayerExtraOptionsPanel);
            AddChild(btnLaunchGame);
            AddChild(btnLeaveGame);
            AddChild(btnPickRandomMap);
        }

        private static XNADropDownItem CreateGameFilterItem(string text, GameModeMapFilter filter)
        {
            return new XNADropDownItem
            {
                Text = text,
                Tag = filter
            };
        }

        protected bool IsFavoriteMapsSelected() => ddGameModeMapFilter.SelectedItem?.Text == FavoriteMapsLabel;

        private List<GameModeMap> GetFavoriteGameModeMaps() =>
            GameModeMaps.Where(gmm => gmm.IsFavorite).ToList();

        private Func<List<GameModeMap>> GetGameModeMaps(GameMode gm) => () =>
            GameModeMaps.Where(gmm => gmm.GameMode == gm).ToList();

        private void InitializePlayerExtraOptionsPanel()
        {
            PlayerExtraOptionsPanel = new PlayerExtraOptionsPanel(WindowManager);
            PlayerExtraOptionsPanel.ClientRectangle = new Rectangle(PlayerOptionsPanel.X, PlayerOptionsPanel.Y, PlayerOptionsPanel.Width, PlayerOptionsPanel.Height);
            PlayerExtraOptionsPanel.OptionsChanged += PlayerExtraOptions_OptionsChanged;
        }

        private void RefreshBthPlayerExtraOptionsOpenTexture()
        {
            var texture = GetPlayerExtraOptions().IsDefault() ? "comboBoxArrow.png" : "comboBoxArrow-highlight.png";
            btnPlayerExtraOptionsOpen.IdleTexture = AssetLoader.LoadTexture(texture);
            btnPlayerExtraOptionsOpen.HoverTexture = AssetLoader.LoadTexture(texture);
        }

        public static void AddAndInitializeWithControl(WindowManager wm, XNAControl control)
        {
            var dp = new DarkeningPanel(wm);
            wm.AddAndInitializeControl(dp);
            dp.AddChild(control);
        }

        private void InitializeGameOptionsPanel()
        {
            GameOptionsPanel = new XNAPanel(WindowManager);
            GameOptionsPanel.Name = nameof(GameOptionsPanel);
            GameOptionsPanel.ClientRectangle = new Rectangle(Width - 411, 12, 399, 289);
            GameOptionsPanel.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 192), 1, 1);
            GameOptionsPanel.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;

            loadOrSaveGameOptionPresetWindow = new LoadOrSaveGameOptionPresetWindow(WindowManager);
            loadOrSaveGameOptionPresetWindow.Name = nameof(loadOrSaveGameOptionPresetWindow);
            loadOrSaveGameOptionPresetWindow.PresetLoaded += (sender, s) => HandleGameOptionPresetLoadCommand(s);
            loadOrSaveGameOptionPresetWindow.PresetSaved += (sender, s) => HandleGameOptionPresetSaveCommand(s);
            loadOrSaveGameOptionPresetWindow.Disable();
            var loadConfigMenuItem = new XNAContextMenuItem()
            {
                Text = "载入",
                SelectAction = () => loadOrSaveGameOptionPresetWindow.Show(true)
            };
            var saveConfigMenuItem = new XNAContextMenuItem()
            {
                Text = "保存",
                SelectAction = () => loadOrSaveGameOptionPresetWindow.Show(false)
            };

            loadSaveGameOptionsMenu = new XNAContextMenu(WindowManager);
            loadSaveGameOptionsMenu.Name = nameof(loadSaveGameOptionsMenu);
            loadSaveGameOptionsMenu.ClientRectangle = new Rectangle(0, 0, 75, 0);
            loadSaveGameOptionsMenu.Items.Add(loadConfigMenuItem);
            loadSaveGameOptionsMenu.Items.Add(saveConfigMenuItem);

            BtnSaveLoadGameOptions = new XNAClientButton(WindowManager);
            BtnSaveLoadGameOptions.Name = nameof(BtnSaveLoadGameOptions);
            BtnSaveLoadGameOptions.ClientRectangle = new Rectangle(Width - 12, 14, 18, 22);
            BtnSaveLoadGameOptions.IdleTexture = AssetLoader.LoadTexture("comboBoxArrow.png");
            BtnSaveLoadGameOptions.HoverTexture = AssetLoader.LoadTexture("comboBoxArrow.png");
            BtnSaveLoadGameOptions.LeftClick += (sender, args) =>
            {
                loadSaveGameOptionsMenu.Open(new Point(BtnSaveLoadGameOptions.X - 74, BtnSaveLoadGameOptions.Y));
            };
        }
        private void BtnAginLoadMaps_LeftClick(object sender, EventArgs e)
        {
           // MapPreviewBox.GameModeMap.Map.ClearstartingLocations();
            MapLoader.AgainLoadMaps();
            
            ddGameModeMapFilter.Items.Clear();

            ddGameModeMapFilter.AddItem(CreateGameFilterItem(FavoriteMapsLabel, new GameModeMapFilter(GetFavoriteGameModeMaps)));
            foreach (GameMode gm in GameModeMaps.GameModes)
            {
                ddGameModeMapFilter.AddItem(CreateGameFilterItem(gm.UIName, new GameModeMapFilter(GetGameModeMaps(gm))));
                Logger.Log(gm.UIName);
            }

            MapPreviewBox.UpdateMap();
            int i = ddGameModeMapFilter.SelectedIndex;
            ddGameModeMapFilter.SelectedIndex = 0;
            ddGameModeMapFilter.SelectedIndex = i;
            //ChangeMap(GameModeMap)
        }

        private void randomMap_EnabledChanged(object sender, EventArgs e)
        {
            if (randomMap.Enabled == false&& randomMap.GetIsSave())
            {

                btnAginLoadMaps.OnLeftClick();
                
                ddGameModeMapFilter.SelectedIndex = ddGameModeMapFilter.Items.FindIndex(d => d.Text == "Standard");
                lbGameModeMapList.SelectedIndex = lbGameModeMapList.ItemCount - 1;

            }
                
        }
        protected void HandleGameOptionPresetSaveCommand(GameOptionPresetEventArgs e) => HandleGameOptionPresetSaveCommand(e.PresetName);

        protected void HandleGameOptionPresetSaveCommand(string presetName)
        {
            string error = AddGameOptionPreset(presetName);
            if (!string.IsNullOrEmpty(error))
                AddNotice(error);
        }

        protected void HandleGameOptionPresetLoadCommand(GameOptionPresetEventArgs e) => HandleGameOptionPresetLoadCommand(e.PresetName);

        protected void HandleGameOptionPresetLoadCommand(string presetName)
        {
            if (LoadGameOptionPreset(presetName))
                AddNotice("游戏选项预设成功加载.");
            else
                AddNotice($"预设 {presetName} 未找到!");
        }

        protected void AddNotice(string message) => AddNotice(message, Color.White);

        protected abstract void AddNotice(string message, Color color);

        private void BtnPickRandomMap_LeftClick(object sender, EventArgs e) => PickRandomMap();

        private void btnRandomMap_LeftClick(object sender, EventArgs e)
        {
           
            randomMap.Enable();
        }
        private void TbMapSearch_InputReceived(object sender, EventArgs e) => ListMaps();

        private void Dropdown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (disableGameOptionUpdateBroadcast)
                return;

            var dd = (GameLobbyDropDown)sender;
            dd.HostSelectedIndex = dd.SelectedIndex;
            OnGameOptionChanged();
        }

        private void ChkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (disableGameOptionUpdateBroadcast)
                return;

            var checkBox = (GameLobbyCheckBox)sender;
            checkBox.HostChecked = checkBox.Checked;
            OnGameOptionChanged();
        }

        /// <summary>
        /// Initializes the underlying window class.
        /// </summary>
        protected void InitializeWindow()
        {
            base.Initialize();
          //  lblMapAuthor.X = MapPreviewBox.Right - lblMapAuthor.Width;
           // lblMapAuthor.TextAnchor = LabelTextAnchorInfo.LEFT;
          //  lblMapAuthor.AnchorPoint = new Vector2(MapPreviewBox.Right, lblMapAuthor.Y);
        }

        protected virtual void OnGameOptionChanged()
        {
            CheckDisallowedSides();

            btnLaunchGame.SetRank(GetRank());
        }

        protected void DdGameModeMapFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            gameModeMapFilter = ddGameModeMapFilter.SelectedItem.Tag as GameModeMapFilter;

            tbMapSearch.Text = string.Empty;
            tbMapSearch.OnSelectedChanged();

            ListMaps();

            if (lbGameModeMapList.SelectedIndex == -1)
                lbGameModeMapList.SelectedIndex = 0; // Select default GameModeMap
            else
                ChangeMap(GameModeMap);
        }

        protected void BtnPlayerExtraOptions_LeftClick(object sender, EventArgs e) => PlayerExtraOptionsPanel.Enable();

        protected void ApplyPlayerExtraOptions(string sender, string message)
        {
            var playerExtraOptions = PlayerExtraOptions.FromMessage(message);

            if (playerExtraOptions.IsForceRandomSides != PlayerExtraOptionsPanel.IsForcedRandomSides())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomSides, "side selection");

            if (playerExtraOptions.IsForceRandomColors != PlayerExtraOptionsPanel.IsForcedRandomColors())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomColors, "color selection");

            if (playerExtraOptions.IsForceRandomStarts != PlayerExtraOptionsPanel.IsForcedRandomStarts())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomStarts, "start selection");

            if (playerExtraOptions.IsForceRandomTeams != PlayerExtraOptionsPanel.IsForcedRandomTeams())
                AddPlayerExtraOptionForcedNotice(playerExtraOptions.IsForceRandomTeams, "team selection");

            if (playerExtraOptions.IsUseTeamStartMappings != PlayerExtraOptionsPanel.IsUseTeamStartMappings())
                AddPlayerExtraOptionForcedNotice(!playerExtraOptions.IsUseTeamStartMappings, "auto ally");

            SetPlayerExtraOptions(playerExtraOptions);
            UpdateMapPreviewBoxEnabledStatus();
        }

        private void AddPlayerExtraOptionForcedNotice(bool disabled, string type)
            => AddNotice($"The game host has {(disabled ? "disabled" : "enabled")} {type}");

        protected void ListMaps()
        {
            lbGameModeMapList.SelectedIndexChanged -= LbGameModeMapList_SelectedIndexChanged;

            lbGameModeMapList.ClearItems();
            lbGameModeMapList.SetTopIndex(0);

            lbGameModeMapList.SelectedIndex = -1;

            int mapIndex = -1;
            int skippedMapsCount = 0;

            var gameModeMaps = gameModeMapFilter.GetGameModeMaps();
            var isFavoriteMapsSelected = IsFavoriteMapsSelected();
            var maps = gameModeMaps.OrderBy(gmm => gmm.Map.Name).ToList();

            for (int i = 0; i < maps.Count; i++)
            {
                var gameModeMap = maps[i];
                if (tbMapSearch.Text != tbMapSearch.Suggestion)
                {
                    if (!gameModeMap.Map.Name.ToUpper().Contains(tbMapSearch.Text.ToUpper()))
                    {
                        skippedMapsCount++;
                        continue;
                    }
                }

                XNAListBoxItem rankItem = new XNAListBoxItem();
                if (gameModeMap.Map.IsCoop)
                {
                    if (StatisticsManager.Instance.HasBeatCoOpMap(gameModeMap.Map.Name, gameModeMap.GameMode.UIName))
                        rankItem.Texture = RankTextures[Math.Abs(2 - gameModeMap.GameMode.CoopDifficultyLevel) + 1];
                    else
                        rankItem.Texture = RankTextures[0];
                }
                else
                    rankItem.Texture = RankTextures[GetDefaultMapRankIndex(gameModeMap) + 1];

                XNAListBoxItem mapNameItem = new XNAListBoxItem();
                var mapNameText = gameModeMap.Map.Name;
                if (isFavoriteMapsSelected)
                    mapNameText += $" - {gameModeMap.GameMode.UIName}";

                mapNameItem.Text = Renderer.GetSafeString(mapNameText, lbGameModeMapList.FontIndex);

                if ((gameModeMap.Map.MultiplayerOnly || gameModeMap.GameMode.MultiplayerOnly) && !isMultiplayer)
                    mapNameItem.TextColor = UISettings.ActiveSettings.DisabledItemColor;
                mapNameItem.Tag = gameModeMap;

                XNAListBoxItem[] mapInfoArray = {
                    rankItem,
                    mapNameItem,
                };

                lbGameModeMapList.AddItem(mapInfoArray);

                if (gameModeMap == GameModeMap)
                    mapIndex = i - skippedMapsCount;
            }

            if (mapIndex > -1)
            {
                lbGameModeMapList.SelectedIndex = mapIndex;
                while (mapIndex > lbGameModeMapList.LastIndex)
                    lbGameModeMapList.TopIndex++;
            }

            lbGameModeMapList.SelectedIndexChanged += LbGameModeMapList_SelectedIndexChanged;
        }

        protected abstract int GetDefaultMapRankIndex(GameModeMap gameModeMap);

        private void LbGameModeMapList_RightClick(object sender, EventArgs e)
        {
            if (lbGameModeMapList.HoveredIndex < 0 || lbGameModeMapList.HoveredIndex >= lbGameModeMapList.ItemCount)
                return;

            lbGameModeMapList.SelectedIndex = lbGameModeMapList.HoveredIndex;

            if (!mapContextMenu.Items.Any(i => i.VisibilityChecker == null || i.VisibilityChecker()))
                return;

            toggleFavoriteItem.Text = GameModeMap.IsFavorite ? "取消收藏" : "收藏";

            mapContextMenu.Open(GetCursorPoint());
        }

        private bool CanDeleteMap()
        {
            return Map != null && !Map.Official && !isMultiplayer;
        }

        private void DeleteMapConfirmation()
        {
            if (Map == null)
                return;

            var messageBox = XNAMessageBox.ShowYesNoDialog(WindowManager, "删除确认",
                "你真的要删除自定义地图 \"" + Map.Name + "\"?");
            messageBox.YesClickedAction = DeleteSelectedMap;
        }

        private void MapPreviewBox_ToggleFavorite(object sender, EventArgs e) =>
            ToggleFavoriteMap();

        protected virtual void ToggleFavoriteMap()
        {
            GameModeMap.IsFavorite = UserINISettings.Instance.ToggleFavoriteMap(Map.Name, GameMode.Name, GameModeMap.IsFavorite);
            MapPreviewBox.RefreshFavoriteBtn();
        }

        protected void RefreshForFavoriteMapRemoved()
        {
            if (!gameModeMapFilter.GetGameModeMaps().Any())
            {
                LoadDefaultGameModeMap();
                return;
            }

            ListMaps();
            if (IsFavoriteMapsSelected())
                lbGameModeMapList.SelectedIndex = 0; // the map was removed while viewing favorites
        }

        private void DeleteSelectedMap(XNAMessageBox messageBox)
        {
            try
            {
                MapLoader.DeleteCustomMap(GameModeMap);

                tbMapSearch.Text = string.Empty;
                if (GameMode.Maps.Count == 0)
                {
                    // this will trigger another GameMode to be selected
                    GameModeMap = GameModeMaps.Find(gm => gm.GameMode.Maps.Count > 0);
                }
                else
                {
                    // this will trigger another Map to be selected
                    lbGameModeMapList.SelectedIndex = lbGameModeMapList.SelectedIndex == 0 ? 1 : lbGameModeMapList.SelectedIndex - 1;
                }

                ListMaps();
                ChangeMap(GameModeMap);
            }
            catch (IOException ex)
            {
                Logger.Log($"Deleting map {Map.BaseFilePath} failed! Message: {ex.Message}");
                XNAMessageBox.Show(WindowManager, "删除地图失败", "删除地图失败!原因: " + ex.Message);
            }
        }

        private void LbGameModeMapList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbGameModeMapList.SelectedIndex < 0 || lbGameModeMapList.SelectedIndex >= lbGameModeMapList.ItemCount)
            {
                ChangeMap(null);
                return;
            }

            XNAListBoxItem item = lbGameModeMapList.GetItem(1, lbGameModeMapList.SelectedIndex);

            GameModeMap = (GameModeMap)item.Tag;
            
            ChangeMap(GameModeMap);
        }

        private void PickRandomMap()
        {
            int totalPlayerCount = Players.Count(p => p.SideId < ddPlayerSides[0].Items.Count - 1)
                   + AIPlayers.Count;
            List<Map> maps = GetMapList(totalPlayerCount);
            if (maps==null||maps.Count < 1)
                return;

            int random = new Random().Next(0, maps.Count);
            GameModeMap = GameModeMaps.Find(gmm => gmm.GameMode == GameMode && gmm.Map == maps[random]);

            Logger.Log("PickRandomMap: Rolled " + random + " out of " + maps.Count + ". Picked map: " + Map.Name);

            ChangeMap(GameModeMap);
            tbMapSearch.Text = string.Empty;
            tbMapSearch.OnSelectedChanged();
            ListMaps();
        }

        private List<Map> GetMapList(int playerCount)
        {
            if (GameMode != null)
            {
                List<Map> mapList = new List<Map>(GameMode.Maps.Where(x => x.MaxPlayers == playerCount));
                if (mapList.Count < 1 && playerCount <= MAX_PLAYER_COUNT)
                    return GetMapList(playerCount + 1);
                else
                    return mapList;
            }
            else
                return null;
        }

        /// <summary>
        /// Refreshes the map selection UI to match the currently selected map
        /// and game mode.
        /// </summary>
        protected void RefreshMapSelectionUI()
        {
            if (GameMode == null)
                return;

            int gameModeMapFilterIndex = ddGameModeMapFilter.Items.FindIndex(i => i.Text == GameMode.UIName);

            if (gameModeMapFilterIndex == -1)
                return;

            if (ddGameModeMapFilter.SelectedIndex == gameModeMapFilterIndex)
                DdGameModeMapFilter_SelectedIndexChanged(this, EventArgs.Empty);

            ddGameModeMapFilter.SelectedIndex = gameModeMapFilterIndex;
        }

        /// <summary>
        /// Initializes the player option drop-down controls.
        /// </summary>
        protected void InitPlayerOptionDropdowns()
        {
            ddPlayerNames = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerSides = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerColors = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerStarts = new XNAClientDropDown[MAX_PLAYER_COUNT];
            ddPlayerTeams = new XNAClientDropDown[MAX_PLAYER_COUNT];

            int playerOptionVecticalMargin = GameOptionsIni.GetIntValue(Name, "PlayerOptionVerticalMargin", PLAYER_OPTION_VERTICAL_MARGIN);
            int playerOptionHorizontalMargin = GameOptionsIni.GetIntValue(Name, "PlayerOptionHorizontalMargin", PLAYER_OPTION_HORIZONTAL_MARGIN);
            int playerOptionCaptionLocationY = GameOptionsIni.GetIntValue(Name, "PlayerOptionCaptionLocationY", PLAYER_OPTION_CAPTION_Y);
            int playerNameWidth = GameOptionsIni.GetIntValue(Name, "PlayerNameWidth", 136);
            int sideWidth = GameOptionsIni.GetIntValue(Name, "SideWidth", 91);
            int colorWidth = GameOptionsIni.GetIntValue(Name, "ColorWidth", 79);
            int startWidth = GameOptionsIni.GetIntValue(Name, "StartWidth", 49);
            int teamWidth = GameOptionsIni.GetIntValue(Name, "TeamWidth", 46);
            int locationX = GameOptionsIni.GetIntValue(Name, "PlayerOptionLocationX", 25);
            int locationY = GameOptionsIni.GetIntValue(Name, "PlayerOptionLocationY", 24);

            // InitPlayerOptionDropdowns(136, 91, 79, 49, 46, new Point(25, 24));

            string[] sides = ClientConfiguration.Instance.Sides.Split(',');
            SideCount = sides.Length;

            List<string> selectorNames = new List<string>();
            GetRandomSelectors(selectorNames, RandomSelectors);
            RandomSelectorCount = RandomSelectors.Count + 1;
            MapPreviewBox.RandomSelectorCount = RandomSelectorCount;

            string randomColor = GameOptionsIni.GetStringValue("General", "RandomColor", "255,255,255");

            for (int i = MAX_PLAYER_COUNT - 1; i > -1; i--)
            {
                var ddPlayerName = new XNAClientDropDown(WindowManager);
                ddPlayerName.Name = "ddPlayerName" + i;
                ddPlayerName.ClientRectangle = new Rectangle(locationX,
                    locationY + (DROP_DOWN_HEIGHT + playerOptionVecticalMargin) * i,
                    playerNameWidth, DROP_DOWN_HEIGHT);
                ddPlayerName.AddItem(String.Empty);
                ProgramConstants.AI_PLAYER_NAMES.ForEach(ddPlayerName.AddItem);
                ddPlayerName.AllowDropDown = true;
                ddPlayerName.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerName.RightClick += MultiplayerName_RightClick;
                ddPlayerName.Tag = true;

                var ddPlayerSide = new XNAClientDropDown(WindowManager);
                ddPlayerSide.Name = "ddPlayerSide" + i;
                ddPlayerSide.ClientRectangle = new Rectangle(
                    ddPlayerName.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, sideWidth, DROP_DOWN_HEIGHT);
                ddPlayerSide.AddItem("随机", LoadTextureOrNull("随机icon.png"));
                foreach (string randomSelector in selectorNames)
                    //ddPlayerSide.AddItem(randomSelector, LoadTextureOrNull(randomSelector + "icon.png"));

                    ddPlayerSide.AddItem(randomSelector.L10N($"UI:Side:{randomSelector}"), LoadTextureOrNull(randomSelector + "icon.png"));

                
                foreach (string sideName in sides)
                    //ddPlayerSide.AddItem(sideName, LoadTextureOrNull(sideName + "icon.png"));
                    
                    ddPlayerSide.AddItem(sideName.L10N($"UI:Side:{sideName}"), LoadTextureOrNull(sideName + "icon.png"));
                ddPlayerSide.AllowDropDown = false;
                ddPlayerSide.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerSide.Tag = true;

                var ddPlayerColor = new XNAClientDropDown(WindowManager);
                ddPlayerColor.Name = "ddPlayerColor" + i;
                ddPlayerColor.ClientRectangle = new Rectangle(
                    ddPlayerSide.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, colorWidth, DROP_DOWN_HEIGHT);
                ddPlayerColor.AddItem("随机", AssetLoader.GetColorFromString(randomColor));
                foreach (MultiplayerColor mpColor in MPColors)
                    ddPlayerColor.AddItem(mpColor.Name, mpColor.XnaColor);
                ddPlayerColor.AllowDropDown = false;
                ddPlayerColor.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerColor.Tag = false;

                var ddPlayerTeam = new XNAClientDropDown(WindowManager);
                ddPlayerTeam.Name = "ddPlayerTeam" + i;
                ddPlayerTeam.ClientRectangle = new Rectangle(
                    ddPlayerColor.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, teamWidth, DROP_DOWN_HEIGHT);
                ddPlayerTeam.AddItem("-");
                ProgramConstants.TEAMS.ForEach(ddPlayerTeam.AddItem);
                ddPlayerTeam.AllowDropDown = false;
                ddPlayerTeam.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerTeam.Tag = true;

                var ddPlayerStart = new XNAClientDropDown(WindowManager);
                ddPlayerStart.Name = "ddPlayerStart" + i;
                ddPlayerStart.ClientRectangle = new Rectangle(
                    ddPlayerTeam.Right + playerOptionHorizontalMargin,
                    ddPlayerName.Y, startWidth, DROP_DOWN_HEIGHT);
                for (int j = 1; j < 9; j++)
                    ddPlayerStart.AddItem(j.ToString());
                ddPlayerStart.AllowDropDown = false;
                ddPlayerStart.SelectedIndexChanged += CopyPlayerDataFromUI;
                ddPlayerStart.Visible = false;
                ddPlayerStart.Enabled = false;
                ddPlayerStart.Tag = true;

                ddPlayerNames[i] = ddPlayerName;
                ddPlayerSides[i] = ddPlayerSide;
                ddPlayerColors[i] = ddPlayerColor;
                ddPlayerStarts[i] = ddPlayerStart;
                ddPlayerTeams[i] = ddPlayerTeam;

                PlayerOptionsPanel.AddChild(ddPlayerName);
                PlayerOptionsPanel.AddChild(ddPlayerSide);
                PlayerOptionsPanel.AddChild(ddPlayerColor);
                PlayerOptionsPanel.AddChild(ddPlayerStart);
                PlayerOptionsPanel.AddChild(ddPlayerTeam);
            }

            btnPlayerExtraOptionsOpen = new XNAClientButton(WindowManager);
            btnPlayerExtraOptionsOpen.Name = nameof(btnPlayerExtraOptionsOpen);
            btnPlayerExtraOptionsOpen.ClientRectangle = new Rectangle(0, 0, 0, 0);
            btnPlayerExtraOptionsOpen.IdleTexture = AssetLoader.LoadTexture("comboBoxArrow.png");
            btnPlayerExtraOptionsOpen.HoverTexture = AssetLoader.LoadTexture("comboBoxArrow.png");
            btnPlayerExtraOptionsOpen.LeftClick += BtnPlayerExtraOptions_LeftClick;
            btnPlayerExtraOptionsOpen.Visible = false;

            lblName = new XNALabel(WindowManager);
            lblName.Name = "lblName";
            lblName.Text = "玩家";
            lblName.FontIndex = 1;
            lblName.ClientRectangle = new Rectangle(ddPlayerNames[0].X, playerOptionCaptionLocationY, 0, 0);

            lblSide = new XNALabel(WindowManager);
            lblSide.Name = "lblSide";
            lblSide.Text = "国家";
            lblSide.FontIndex = 1;
            lblSide.ClientRectangle = new Rectangle(ddPlayerSides[0].X, playerOptionCaptionLocationY, 0, 0);

            lblColor = new XNALabel(WindowManager);
            lblColor.Name = "lblColor";
            lblColor.Text = "颜色";
            lblColor.FontIndex = 1;
            lblColor.ClientRectangle = new Rectangle(ddPlayerColors[0].X, playerOptionCaptionLocationY, 0, 0);

            lblStart = new XNALabel(WindowManager);
            lblStart.Name = "lblStart";
            lblStart.Text = "位置";
            lblStart.FontIndex = 1;
            lblStart.ClientRectangle = new Rectangle(ddPlayerStarts[0].X, playerOptionCaptionLocationY, 0, 0);
            lblStart.Visible = false;

            lblTeam = new XNALabel(WindowManager);
            lblTeam.Name = "lblTeam";
            lblTeam.Text = "小队";
            lblTeam.FontIndex = 1;
            lblTeam.ClientRectangle = new Rectangle(ddPlayerTeams[0].X, playerOptionCaptionLocationY, 0, 0);

            PlayerOptionsPanel.AddChild(btnPlayerExtraOptionsOpen);
            PlayerOptionsPanel.AddChild(lblName);
            PlayerOptionsPanel.AddChild(lblSide);
            PlayerOptionsPanel.AddChild(lblColor);
            PlayerOptionsPanel.AddChild(lblStart);
            PlayerOptionsPanel.AddChild(lblTeam);

            CheckDisallowedSides();
        }

        protected virtual void PlayerExtraOptions_OptionsChanged(object sender, EventArgs e)
        {
            var playerExtraOptions = GetPlayerExtraOptions();

            for (int i = 0; i < ddPlayerSides.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerSides[i], i, !playerExtraOptions.IsForceRandomSides);

            for (int i = 0; i < ddPlayerTeams.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerTeams[i], i, !playerExtraOptions.IsForceRandomTeams);

            for (int i = 0; i < ddPlayerColors.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerColors[i], i, !playerExtraOptions.IsForceRandomColors);

            for (int i = 0; i < ddPlayerStarts.Length; i++)
                EnablePlayerOptionDropDown(ddPlayerStarts[i], i, !playerExtraOptions.IsForceRandomStarts);

            UpdateMapPreviewBoxEnabledStatus();
            RefreshBthPlayerExtraOptionsOpenTexture();
        }

        private void EnablePlayerOptionDropDown(XNAClientDropDown clientDropDown, int playerIndex, bool enable)
        {
            var pInfo = GetPlayerInfoForIndex(playerIndex);
            var allowOtherPlayerOptionsChange = AllowPlayerOptionsChange() && pInfo != null;
            clientDropDown.AllowDropDown = enable && (allowOtherPlayerOptionsChange || pInfo?.Name == ProgramConstants.PLAYERNAME);
            if (!clientDropDown.AllowDropDown)
                clientDropDown.SelectedIndex = clientDropDown.SelectedIndex > 0 ? 0 : clientDropDown.SelectedIndex;
        }

        protected PlayerInfo GetPlayerInfoForIndex(int playerIndex)
        {
            if (playerIndex < Players.Count)
                return Players[playerIndex];

            if (playerIndex < Players.Count + AIPlayers.Count)
                return AIPlayers[playerIndex - Players.Count];

            return null;
        }

        protected PlayerExtraOptions GetPlayerExtraOptions() => PlayerExtraOptionsPanel.GetPlayerExtraOptions();

        protected void SetPlayerExtraOptions(PlayerExtraOptions playerExtraOptions) => PlayerExtraOptionsPanel?.SetPlayerExtraOptions(playerExtraOptions);

        protected string GetTeamMappingsError() => GetPlayerExtraOptions()?.GetTeamMappingsError();

        private Texture2D LoadTextureOrNull(string name) =>
            AssetLoader.AssetExists(name) ? AssetLoader.LoadTexture(name) : null;

        /// <summary>
        /// Loads random side selectors from GameOptions.ini
        /// </summary>
        /// <param name="selectorNames">TODO comment</param>
        /// <param name="selectorSides">TODO comment</param>
        private void GetRandomSelectors(List<string> selectorNames, List<int[]> selectorSides)
        {
            List<string> keys = GameOptionsIni.GetSectionKeys("RandomSelectors");

            if (keys == null)
                return;

            foreach (string randomSelector in keys)
            {
                List<int> randomSides = new List<int>();
                try
                {
                    string[] tmp = GameOptionsIni.GetStringValue("RandomSelectors", randomSelector, string.Empty).Split(',');
                    randomSides = Array.ConvertAll(tmp, int.Parse).Distinct().ToList();
                    randomSides.RemoveAll(x => (x >= SideCount || x < 0));
                }
                catch (FormatException) { }

                if (randomSides.Count > 1)
                {
                    selectorNames.Add(randomSelector);
                    selectorSides.Add(randomSides.ToArray());
                }
            }
        }

        protected abstract void BtnLaunchGame_LeftClick(object sender, EventArgs e);

        protected abstract void BtnLeaveGame_LeftClick(object sender, EventArgs e);

        /// <summary>
        /// Updates Discord Rich Presence with actual information.
        /// </summary>
        /// <param name="resetTimer">Whether to restart the "Elapsed" timer or not</param>
        protected abstract void UpdateDiscordPresence(bool resetTimer = false);

        /// <summary>
        /// Resets Discord Rich Presence to default state.
        /// </summary>
        protected void ResetDiscordPresence() => discordHandler?.UpdatePresence();

        protected void LoadDefaultGameModeMap()
        {
            if (ddGameModeMapFilter.Items.Count > 0)
            {
                ddGameModeMapFilter.SelectedIndex = GetDefaultGameModeMapFilterIndex();

                lbGameModeMapList.SelectedIndex = 0;
            }
        }

        protected int GetDefaultGameModeMapFilterIndex()
        {
            return ddGameModeMapFilter.Items.FindIndex(i => (i.Tag as GameModeMapFilter)?.Any() ?? false);
        }

        protected GameModeMapFilter GetDefaultGameModeMapFilter()
        {
            return ddGameModeMapFilter.Items[GetDefaultGameModeMapFilterIndex()].Tag as GameModeMapFilter;
        }

        private int GetSpectatorSideIndex() => SideCount + RandomSelectorCount;

        /// <summary>
        /// Applies disallowed side indexes to the side option drop-downs
        /// and player options.
        /// </summary>
        protected void CheckDisallowedSides()
        {
            var disallowedSideArray = GetDisallowedSides();
            int defaultSide = 0;
            int allowedSideCount = disallowedSideArray.Count(b => b == false);

            if (allowedSideCount == 1)
            {
                // Disallow Random

                for (int i = 0; i < disallowedSideArray.Length; i++)
                {
                    if (!disallowedSideArray[i])
                        defaultSide = i + RandomSelectorCount;
                }

                foreach (XNADropDown dd in ddPlayerSides)
                {
                    for (int i = 0; i < RandomSelectorCount; i++)
                        dd.Items[i].Selectable = false;
                }
            }
            else
            {
                foreach (XNADropDown dd in ddPlayerSides)
                {

                    for (int i = 0; i < RandomSelectorCount; i++)
                        dd.Items[i].Selectable = true;
                }
            }

            var concatPlayerList = Players.Concat(AIPlayers);

            // Disable custom random groups if all or all except one of included sides are unavailable.
            int c = 0;
            var playerInfos = concatPlayerList.ToList();
            foreach (int[] randomSides in RandomSelectors)
            {
                int disableCount = 0;

                foreach (int side in randomSides)
                {
                    if (disallowedSideArray[side])
                        disableCount++;
                }

                bool disabled = disableCount >= randomSides.Length - 1;

                foreach (XNADropDown dd in ddPlayerSides)
                    dd.Items[1 + c].Selectable = !disabled;

                foreach (PlayerInfo pInfo in playerInfos)
                {
                    if (pInfo.SideId == 1 + c && disabled)
                        pInfo.SideId = defaultSide;
                }

                c++;
            }

            // Go over the side array and either disable or enable the side
            // dropdown options depending on whether the side is available
            for (int i = 0; i < disallowedSideArray.Length; i++)
            {
                bool disabled = disallowedSideArray[i];

                if (disabled)
                {
                    foreach (XNADropDown dd in ddPlayerSides)
                        dd.Items[i + RandomSelectorCount].Selectable = false;

                    // Change the sides of players that use the disabled 
                    // side to the default side
                    foreach (PlayerInfo pInfo in playerInfos)
                    {
                        if (pInfo.SideId == i + RandomSelectorCount)
                            pInfo.SideId = defaultSide;
                    }
                }
                else
                {
                    foreach (XNADropDown dd in ddPlayerSides)
                    {

                        dd.Items[i + RandomSelectorCount].Selectable = true;
                    }
                }
            }

            // If only 1 side is allowed, change all players' sides to that
            if (allowedSideCount == 1)
            {
                foreach (PlayerInfo pInfo in playerInfos)
                {
                    if (pInfo.SideId == 0)
                        pInfo.SideId = defaultSide;
                }
            }

            if (Map != null && Map.CoopInfo != null)
            {
                // Disallow spectator

                foreach (PlayerInfo pInfo in playerInfos)
                {
                    if (pInfo.SideId == GetSpectatorSideIndex())
                        pInfo.SideId = defaultSide;
                }

                foreach (XNADropDown dd in ddPlayerSides)
                {
                    if (dd.Items.Count > GetSpectatorSideIndex())
                        dd.Items[SideCount + RandomSelectorCount].Selectable = false;
                }
            }
            else
            {
                foreach (XNADropDown dd in ddPlayerSides)
                {

                    if (dd.Items.Count > SideCount + RandomSelectorCount)
                        dd.Items[SideCount + RandomSelectorCount].Selectable = true;
                }
            }
        }

        public List<string> GetModeFile()
        {
            List<string> modeFile =new List<string>();
            foreach (var dropDown in DropDowns)
            {
                var skirmishSettingsIni = new IniFile(ProgramConstants.GamePath + "Client/SkirmishSettings.ini");
                int index = skirmishSettingsIni.GetIntValue("GameOptions", dropDown.Name, 0);

                if (dropDown.ApplyModeFileIndex() != ""&&dropDown.SelectedIndex!=index)
                    modeFile.Add(dropDown.ApplyModeFileIndex());
            }
            return modeFile;
        }



        public List<string> GetDeleteFile()
        {
            List<string> deleteFile = new List<string>();
            foreach (var dropDown in DropDowns)
            {

                var skirmishSettingsIni = new IniFile(ProgramConstants.GamePath + "Client/SkirmishSettings.ini");

                int index = skirmishSettingsIni.GetIntValue("GameOptions", dropDown.Name, 0);

                if (dropDown.Mode.Length > index &&dropDown.SelectedIndex!=index&& dropDown.Mode[index] != "")
                {
 
                   foreach (string file in  Directory.GetFiles(dropDown.Mode[skirmishSettingsIni.GetIntValue("GameOptions", dropDown.Name, 0)]))

                    deleteFile.Add(Path.GetFileName(file));

                    
                }
            }
            return deleteFile;
        }

        public List<string> GetAllModeFile()
        {
            List<string> modeFile = new List<string>();
            foreach (var dropDown in DropDowns)
            {
                
                if (dropDown.ApplyModeFileIndex() != "")
                    modeFile.Add(dropDown.ApplyModeFileIndex());
            }
            return modeFile;
        }

        protected List<string> GetAllDeleteFile()
        {
            List<string> deleteFile = new List<string>();
            foreach (var dropDown in DropDowns)
            {
                for (int i = 0; i < dropDown.Mode.Length; i++)
                    try
                    {
                        foreach (string file in Directory.GetFiles(dropDown.Mode[i]))
                            deleteFile.Add(Path.GetFileName(file));
                    }
                    catch
                    {
                        continue;
                    }
                }
                return deleteFile;
            }

        /// <summary>
        /// Gets a list of side indexes that are disallowed.
        /// </summary>
        /// <returns>A list of disallowed side indexes.</returns>
        protected bool[] GetDisallowedSides()
        {

            string[] sides = null;
            List<string> selectorNames = new List<string>();
            RandomSelectors.Clear();
            string[,] Randomside = null;
            int count = 0;
            foreach (var dropDown in DropDowns)
            {
                sides = dropDown.SetSides();
            }

            if (sides != null)
            {
                
                foreach (var dropDown in DropDowns)
                {
                    Randomside = dropDown.SetRandomSelectors();
                }
                if (Randomside != null)
                {
                    count = Randomside.GetLength(1);
                    MapPreviewBox.RandomSelectorCount = Randomside.GetLength(1);
                }
                RandomSelectorCount = count+1;

                SideCount = sides.Length;
            }
            else
            {
                sides = GameOptionsIni.GetStringValue("General", "Sides", String.Empty).Split(',');
                GetRandomSelectors(selectorNames, RandomSelectors);
                RandomSelectorCount = RandomSelectors.Count + 1;
                count = RandomSelectors.Count;
                Randomside = new string[selectorNames.Count, 2];
                for (int i = 0; i < selectorNames.Count; i++)
                {
                    Randomside[i, 0] = selectorNames[i];

                    Randomside[i, 1] = string.Join(",", RandomSelectors[i]);

                }
                SideCount = sides.Length;
            }
            
            foreach (var ddSide in ddPlayerSides)
            {
                ddSide.Items.Clear();
                ddSide.AddItem("随机", LoadTextureOrNull("随机icon.png"));
                RandomSelectors.Clear();
                for (int i = 0; i < count; i++)
                {
                    RandomSelectors.Add(Array.ConvertAll(Randomside[i, 1].Split(','), int.Parse));
                    ddSide.AddItem(Randomside[i, 0].L10N($"UI:Side:{Randomside[i, 0]}"), LoadTextureOrNull(Randomside[i, 0] + "icon.png"));
                }

                for (int i = count; i < sides.Length + count; i++)                         
                {
                    ddSide.AddItem(sides[i - count], LoadTextureOrNull(sides[i - count] + "icon.png"));
                }
                ddSide.AddItem("观察者", LoadTextureOrNull("观察者icon.png"));
            }

            var returnValue = new bool[SideCount];

            foreach (var dropDown in DropDowns)
            {
                dropDown.ApplyDisallowedSideIndex(returnValue);
            }
                if (Map != null && Map.CoopInfo != null)
            {
                // Co-Op map disallowed side logic

                foreach (int disallowedSideIndex in Map.CoopInfo.DisallowedPlayerSides)
                    returnValue[disallowedSideIndex] = true;
            }

            if (GameMode != null)
            {
                foreach (int disallowedSideIndex in GameMode.DisallowedPlayerSides)
                    returnValue[disallowedSideIndex] = true;
            }

            foreach (var checkBox in CheckBoxes)
                checkBox.ApplyDisallowedSideIndex(returnValue);

          

                
            return returnValue;
        }

        /// <summary>
        /// Randomizes options of both human and AI players
        /// and returns the options as an array of PlayerHouseInfos.
        /// </summary>
        /// <returns>An array of PlayerHouseInfos.</returns>
        protected virtual PlayerHouseInfo[] Randomize(List<TeamStartMapping> teamStartMappings)
        {
            int totalPlayerCount = Players.Count + AIPlayers.Count;
            PlayerHouseInfo[] houseInfos = new PlayerHouseInfo[totalPlayerCount];

            for (int i = 0; i < totalPlayerCount; i++)
                houseInfos[i] = new PlayerHouseInfo();

            // Gather list of spectators
            for (int i = 0; i < Players.Count; i++)
                houseInfos[i].IsSpectator = Players[i].SideId == GetSpectatorSideIndex();

            // Gather list of available colors

            List<int> freeColors = new List<int>();

            for (int cId = 0; cId < MPColors.Count; cId++)
                freeColors.Add(cId);

            if (Map.CoopInfo != null)
            {
                foreach (int colorIndex in Map.CoopInfo.DisallowedPlayerColors)
                    freeColors.Remove(colorIndex);

            }

            foreach (PlayerInfo player in Players)
                freeColors.Remove(player.ColorId - 1); // The first color is Random

            foreach (PlayerInfo aiPlayer in AIPlayers)
                freeColors.Remove(aiPlayer.ColorId - 1);

            // Gather list of available starting locations

            List<int> freeStartingLocations = new List<int>();
            List<int> takenStartingLocations = new List<int>();

            for (int i = 0; i < Map.MaxPlayers; i++)
                freeStartingLocations.Add(i);

            for (int i = 0; i < Players.Count; i++)
            {
                if (!houseInfos[i].IsSpectator)
                {
                    freeStartingLocations.Remove(Players[i].StartingLocation - 1);
                    //takenStartingLocations.Add(Players[i].StartingLocation - 1);
                    // ^ Gives everyone with a selected location a completely random
                    // location in-game, because PlayerHouseInfo.RandomizeStart already
                    // fills the list itself
                }
            }

            for (int i = 0; i < AIPlayers.Count; i++)
                freeStartingLocations.Remove(AIPlayers[i].StartingLocation - 1);

            foreach (var teamStartMapping in teamStartMappings.Where(mapping => mapping.IsBlock))
                freeStartingLocations.Remove(teamStartMapping.StartingWaypoint);

            // Randomize options

            Random random = new Random(RandomSeed);

            for (int i = 0; i < totalPlayerCount; i++)
            {
                PlayerInfo pInfo;
                PlayerHouseInfo pHouseInfo = houseInfos[i];

                if (i < Players.Count)
                    pInfo = Players[i];
                else
                    pInfo = AIPlayers[i - Players.Count];
              //  Logger.Log(GetDisallowedSides()[0].ToString());
                //SideCount一共有多少国家
                pHouseInfo.RandomizeSide(pInfo, SideCount, random, GetDisallowedSides(), RandomSelectors, RandomSelectorCount);

                pHouseInfo.RandomizeColor(pInfo, freeColors, MPColors, random);
                pHouseInfo.RandomizeStart(pInfo, random, freeStartingLocations, takenStartingLocations, teamStartMappings.Any());
            }

            return houseInfos;
        }

        /// <summary>
        /// Writes spawn.ini. Returns the player house info returned from the randomizer.
        /// </summary>
        private PlayerHouseInfo[] WriteSpawnIni()
        {
            Logger.Log("Writing spawn.ini");

            File.Delete(ProgramConstants.GamePath + ProgramConstants.SPAWNER_SETTINGS);

            if (Map.IsCoop)
            {
                foreach (PlayerInfo pInfo in Players)
                    pInfo.TeamId = 1;

                foreach (PlayerInfo pInfo in AIPlayers)
                    pInfo.TeamId = 1;
            }

            var teamStartMappings = PlayerExtraOptionsPanel.GetTeamStartMappings();

            PlayerHouseInfo[] houseInfos = Randomize(teamStartMappings);

            IniFile spawnIni = new IniFile(ProgramConstants.GamePath + ProgramConstants.SPAWNER_SETTINGS);

            IniSection settings = new IniSection("Settings");

            settings.SetStringValue("Name", ProgramConstants.PLAYERNAME);
            settings.SetStringValue("Scenario", ProgramConstants.SPAWNMAP_INI);
            settings.SetStringValue("UIGameMode", GameMode.UIName);
            settings.SetStringValue("UIMapName", Map.Name);
            settings.SetIntValue("PlayerCount", Players.Count);
            int myIndex = Players.FindIndex(c => c.Name == ProgramConstants.PLAYERNAME);
            settings.SetIntValue("Side", houseInfos[myIndex].InternalSideIndex);
            settings.SetBooleanValue("IsSpectator", houseInfos[myIndex].IsSpectator);
            settings.SetIntValue("Color", houseInfos[myIndex].ColorIndex);
            settings.SetStringValue("CustomLoadScreen", LoadingScreenController.GetLoadScreenName(houseInfos[myIndex].InternalSideIndex.ToString()));
            settings.SetIntValue("AIPlayers", AIPlayers.Count);
            settings.SetIntValue("Seed", RandomSeed);
            if (GetPvPTeamCount() > 1)
                settings.SetBooleanValue("CoachMode", true);
            if (GetGameType() == GameType.Coop)
                settings.SetBooleanValue("AutoSurrender", false);
            spawnIni.AddSection(settings);
            WriteSpawnIniAdditions(spawnIni);

            foreach (GameLobbyCheckBox chkBox in CheckBoxes)
                chkBox.ApplySpawnINICode(spawnIni);

            foreach (GameLobbyDropDown dd in DropDowns)
                dd.ApplySpawnIniCode(spawnIni);

            // Apply forced options from GameOptions.ini

            List<string> forcedKeys = GameOptionsIni.GetSectionKeys("ForcedSpawnIniOptions");

            if (forcedKeys != null)
            {
                foreach (string key in forcedKeys)
                {
                    spawnIni.SetStringValue("Settings", key,
                        GameOptionsIni.GetStringValue("ForcedSpawnIniOptions", key, String.Empty));
                }
            }

            GameMode.ApplySpawnIniCode(spawnIni); // Forced options from the game mode
            Map.ApplySpawnIniCode(spawnIni, Players.Count + AIPlayers.Count,
                AIPlayers.Count, GameMode.CoopDifficultyLevel); // Forced options from the map

            // Player options

            int otherId = 1;

            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];
                PlayerHouseInfo pHouseInfo = houseInfos[pId];

                if (pInfo.Name == ProgramConstants.PLAYERNAME)
                    continue;

                string sectionName = "Other" + otherId;

                spawnIni.SetStringValue(sectionName, "Name", pInfo.Name);
                spawnIni.SetIntValue(sectionName, "Side", pHouseInfo.InternalSideIndex);
                spawnIni.SetBooleanValue(sectionName, "IsSpectator", pHouseInfo.IsSpectator);
                spawnIni.SetIntValue(sectionName, "Color", pHouseInfo.ColorIndex);
                spawnIni.SetStringValue(sectionName, "Ip", GetIPAddressForPlayer(pInfo));
                spawnIni.SetIntValue(sectionName, "Port", pInfo.Port);

                otherId++;
            }

            // The spawner assigns players to SpawnX houses based on their in-game color index
            List<int> multiCmbIndexes = new List<int>();
            var sortedColorList = MPColors.OrderBy(mpc => mpc.GameColorIndex).ToList();

            for (int cId = 0; cId < sortedColorList.Count; cId++)
            {
                for (int pId = 0; pId < Players.Count; pId++)
                {
                    if (houseInfos[pId].ColorIndex == sortedColorList[cId].GameColorIndex)
                        multiCmbIndexes.Add(pId);
                }
            }

            if (AIPlayers.Count > 0)
            {
                for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
                {
                    int multiId = multiCmbIndexes.Count + aiId + 1;

                    string keyName = "Multi" + multiId;

                    spawnIni.SetIntValue("HouseHandicaps", keyName, AIPlayers[aiId].AILevel);
                    spawnIni.SetIntValue("HouseCountries", keyName, houseInfos[Players.Count + aiId].InternalSideIndex);
                    spawnIni.SetIntValue("HouseColors", keyName, houseInfos[Players.Count + aiId].ColorIndex);
                }
            }

            for (int multiId = 0; multiId < multiCmbIndexes.Count; multiId++)
            {
                int pIndex = multiCmbIndexes[multiId];
                if (houseInfos[pIndex].IsSpectator)
                    spawnIni.SetBooleanValue("IsSpectator", "Multi" + (multiId + 1), true);
            }

            // Write alliances, the code is pretty big so let's take it to another class
            AllianceHolder.WriteInfoToSpawnIni(Players, AIPlayers, multiCmbIndexes, houseInfos.ToList(), teamStartMappings, spawnIni);

          
            for (int pId = 0; pId < Players.Count; pId++)
            {
                int startingWaypoint = houseInfos[multiCmbIndexes[pId]].StartingWaypoint;
                Logger.Log("startingWaypoint" + startingWaypoint.ToString());
                // -1 means no starting location at all - let the game itself pick the starting location
                // using its own logic
                if (startingWaypoint > -1)
                {
                    int multiIndex = pId + 1;
                    spawnIni.SetIntValue("SpawnLocations", "Multi" + multiIndex,
                        startingWaypoint);
                    
                }
            }

           
            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                int startingWaypoint = houseInfos[Players.Count + aiId].StartingWaypoint;

                if (startingWaypoint > -1)
                {
                    int multiIndex = Players.Count + aiId + 1;
                    spawnIni.SetIntValue("SpawnLocations", "Multi" + multiIndex,
                        startingWaypoint);
                    Logger.Log(startingWaypoint.ToString());
                }
            }

            spawnIni.WriteIniFile();

            return houseInfos;
        }

        /// <summary>
        /// Returns the number of teams with human players in them.
        /// Does not count spectators and human players that don't have a team set.
        /// </summary>
        /// <returns>The number of human player teams in the game.</returns>
        private int GetPvPTeamCount()
        {
            int[] teamPlayerCounts = new int[4];
            int playerTeamCount = 0;

            foreach (PlayerInfo pInfo in Players)
            {
                if (pInfo.IsAI || IsPlayerSpectator(pInfo))
                    continue;

                if (pInfo.TeamId > 0)
                {
                    teamPlayerCounts[pInfo.TeamId - 1]++;
                    if (teamPlayerCounts[pInfo.TeamId - 1] == 2)
                        playerTeamCount++;
                }
            }

            return playerTeamCount;
        }

        /// <summary>
        /// Checks whether the specified player has selected Spectator as their side.
        /// </summary>
        /// <param name="pInfo">The player.</param>
        /// <returns>True if the player is a spectator, otherwise false.</returns>
        private bool IsPlayerSpectator(PlayerInfo pInfo)
        {
            if (pInfo.SideId == GetSpectatorSideIndex())
                return true;

            return false;
        }

        protected virtual string GetIPAddressForPlayer(PlayerInfo player) => "0.0.0.0";

        /// <summary>
        /// Override this in a derived class to write game lobby specific code to
        /// spawn.ini. For example, CnCNet game lobbies should write tunnel info
        /// in this method.
        /// </summary>
        /// <param name="iniFile">The spawn INI file.</param>
        protected virtual void WriteSpawnIniAdditions(IniFile iniFile)
        {
            // Do nothing by default
        }

        private void InitializeMatchStatistics(PlayerHouseInfo[] houseInfos)
        {
            matchStatistics = new MatchStatistics(ProgramConstants.GAME_VERSION, UniqueGameID,
                Map.Name, GameMode.UIName, Players.Count, Map.IsCoop);

            bool isValidForStar = true;
            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
            {
                if ((checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenChecked && checkBox.Checked) ||
                    (checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenUnchecked && !checkBox.Checked))
                {
                    isValidForStar = false;
                    break;
                }
            }

            matchStatistics.IsValidForStar = isValidForStar;

            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];
                matchStatistics.AddPlayer(pInfo.Name, pInfo.Name == ProgramConstants.PLAYERNAME,
                    false, pInfo.SideId == SideCount + RandomSelectorCount, houseInfos[pId].SideIndex + 1, pInfo.TeamId,
                    MPColors.FindIndex(c => c.GameColorIndex == houseInfos[pId].ColorIndex), 10);
            }

            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                var pHouseInfo = houseInfos[Players.Count + aiId];
                PlayerInfo aiInfo = AIPlayers[aiId];
                matchStatistics.AddPlayer("Computer", false, true, false,
                    pHouseInfo.SideIndex + 1, aiInfo.TeamId,
                    MPColors.FindIndex(c => c.GameColorIndex == pHouseInfo.ColorIndex),
                    aiInfo.ReversedAILevel);
            }
        }

        /// <summary>
        /// Writes spawnmap.ini.
        /// </summary>
        private void WriteMap(PlayerHouseInfo[] houseInfos)
        {
            File.Delete(ProgramConstants.GamePath + ProgramConstants.SPAWNMAP_INI);

            Logger.Log("Writing map.");

            Logger.Log("Loading map INI from " + Map.CompleteFilePath);

            IniFile mapIni = Map.GetMapIni();

            IniFile globalCodeIni = new IniFile(ProgramConstants.GamePath + "INI/Map Code/GlobalCode.ini");

            Logger.Log("mapini" + mapIni);

            MapCodeHelper.ApplyMapCode(mapIni, GameMode.GetMapRulesIniFile());
            MapCodeHelper.ApplyMapCode(mapIni, globalCodeIni);

            if (isMultiplayer)
            {
                IniFile mpGlobalCodeIni = new IniFile(ProgramConstants.GamePath + "INI/Map Code/MultiplayerGlobalCode.ini");
                MapCodeHelper.ApplyMapCode(mapIni, mpGlobalCodeIni);
            }

            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
                checkBox.ApplyMapCode(mapIni, GameMode);

            foreach (GameLobbyDropDown dropDown in DropDowns)
                dropDown.ApplyMapCode(mapIni, GameMode);

            mapIni.MoveSectionToFirst("MultiplayerDialogSettings"); // Required by YR

            ManipulateStartingLocations(mapIni, houseInfos);

            mapIni.WriteIniFile(ProgramConstants.GamePath + ProgramConstants.SPAWNMAP_INI);
        }

        private void ManipulateStartingLocations(IniFile mapIni, PlayerHouseInfo[] houseInfos)
        {
            if (RemoveStartingLocations)
            {
                if (Map.EnforceMaxPlayers)
                    return;

                // All random starting locations given by the game
                IniSection waypointSection = mapIni.GetSection("Waypoints");
                if (waypointSection == null)
                    return;

                // TODO implement IniSection.RemoveKey in Rampastring.Tools, then
                // remove implementation that depends on internal implementation
                // of IniSection
                for (int i = 0; i <= 7; i++)
                {
                    int index = waypointSection.Keys.FindIndex(k => !string.IsNullOrEmpty(k.Key) && k.Key == i.ToString());
                    if (index > -1)
                        waypointSection.Keys.RemoveAt(index);
                }
            }

            // Multiple players cannot properly share the same starting location
            // without breaking the SpawnX house logic that pre-placed objects depend on

            // To work around this, we add new starting locations that just point
            // to the same cell coordinates as existing stacked starting locations
            // and make additional players in the same start loc start from the new
            // starting locations instead.

            // As an additional restriction, players can only start from waypoints 0 to 7.
            // That means that if the map already has too many starting waypoints,
            // we need to move existing (but un-occupied) starting waypoints to point 
            // to the stacked locations so we can spawn the players there.


            // Check for stacked starting locations (locations with more than 1 player on it)
            bool[] startingLocationUsed = new bool[MAX_PLAYER_COUNT];
            bool stackedStartingLocations = false;
            foreach (PlayerHouseInfo houseInfo in houseInfos)
            {
                if (houseInfo.RealStartingWaypoint > -1)
                {
                    startingLocationUsed[houseInfo.RealStartingWaypoint] = true;

                    // If assigned starting waypoint is unknown while the real 
                    // starting location is known, it means that
                    // the location is shared with another player
                    if (houseInfo.StartingWaypoint == -1)
                    {
                        stackedStartingLocations = true;
                    }
                }
            }

            // If any starting location is stacked, re-arrange all starting locations
            // so that unused starting locations are removed and made to point at used
            // starting locations
            if (!stackedStartingLocations)
                return;

            // We also need to modify spawn.ini because WriteSpawnIni
            // doesn't handle stacked positions.
            // We could move this code there, but then we'd have to process
            // the stacked locations in two places (here and in WriteSpawnIni)
            // because we'd need to modify the map anyway.
            // Not sure whether having it like this or in WriteSpawnIni
            // is better, but this implementation is quicker to write for now.
            IniFile spawnIni = new IniFile(ProgramConstants.GamePath + ProgramConstants.SPAWNER_SETTINGS);

            // For each player, check if they're sharing the starting location
            // with someone else
            // If they are, find an unused waypoint and assign their 
            // starting location to match that
            for (int pId = 0; pId < houseInfos.Length; pId++)
            {
                PlayerHouseInfo houseInfo = houseInfos[pId];

                if (houseInfo.RealStartingWaypoint > -1 &&
                    houseInfo.StartingWaypoint == -1)
                {
                    // Find first unused starting location index
                    int unusedLocation = -1;
                    for (int i = 0; i < startingLocationUsed.Length; i++)
                    {
                        if (!startingLocationUsed[i])
                        {
                            unusedLocation = i;
                            startingLocationUsed[i] = true;
                            break;
                        }
                    }

                    houseInfo.StartingWaypoint = unusedLocation;
                    mapIni.SetIntValue("Waypoints", unusedLocation.ToString(),
                        mapIni.GetIntValue("Waypoints", houseInfo.RealStartingWaypoint.ToString(), 0));
                    spawnIni.SetIntValue("SpawnLocations", $"Multi{pId + 1}", unusedLocation);
                    //Logger.Log(unusedLocation.ToString());
                }
            }

            spawnIni.WriteIniFile();
        }

        public void DelFile(List<string> deleteFile)
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
        public void CopyDirectory(string sourceDirPath, string saveDirPath)
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
        /// Writes spawn.ini, writes the map file, initializes statistics and
        /// starts the game process.
        /// </summary>
        protected virtual void StartGame()
        {
           
            PlayerHouseInfo[] houseInfos = WriteSpawnIni();
            InitializeMatchStatistics(houseInfos);
           
            WriteMap(houseInfos);
           
            GameProcessLogic.GameProcessExited += GameProcessExited_Callback;

            GameProcessLogic.StartGameProcess();
            UpdateDiscordPresence(true);
        }

        private void GameProcessExited_Callback() => AddCallback(new Action(GameProcessExited), null);

        protected virtual void GameProcessExited()
        {
            GameProcessLogic.GameProcessExited -= GameProcessExited_Callback;

            Logger.Log("GameProcessExited: Parsing statistics.");

            matchStatistics.ParseStatistics(ProgramConstants.GamePath, ClientConfiguration.Instance.LocalGame, false);

            Logger.Log("GameProcessExited: Adding match to statistics.");

            StatisticsManager.Instance.AddMatchAndSaveDatabase(true, matchStatistics);

            ClearReadyStatuses();

            CopyPlayerDataToUI();

            UpdateDiscordPresence(true);
        }

        /// <summary>
        /// "Copies" player information from the UI to internal memory,
        /// applying users' player options changes.
        /// </summary>
        protected virtual void CopyPlayerDataFromUI(object sender, EventArgs e)
        {
            if (PlayerUpdatingInProgress)
                return;

            var senderDropDown = (XNADropDown)sender;
            if ((bool)senderDropDown.Tag)
                ClearReadyStatuses();

            var oldSideId = Players.Find(p => p.Name == ProgramConstants.PLAYERNAME)?.SideId;

            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];

                pInfo.ColorId = ddPlayerColors[pId].SelectedIndex;
                pInfo.SideId = ddPlayerSides[pId].SelectedIndex;
                pInfo.StartingLocation = ddPlayerStarts[pId].SelectedIndex;
                pInfo.TeamId = ddPlayerTeams[pId].SelectedIndex;

                if (pInfo.SideId == SideCount + RandomSelectorCount)
                    pInfo.StartingLocation = 0;

                XNADropDown ddName = ddPlayerNames[pId];

                switch (ddName.SelectedIndex)
                {
                    case 0:
                        break;
                    case 1:
                        ddName.SelectedIndex = 0;
                        break;
                    case 2:
                        KickPlayer(pId);
                        break;
                    case 3:
                        BanPlayer(pId);
                        break;
                }
            }

            AIPlayers.Clear();
            for (int cmbId = Players.Count; cmbId < 8; cmbId++)
            {
                XNADropDown dd = ddPlayerNames[cmbId];
                dd.Items[0].Text = "-";

                if (dd.SelectedIndex < 1)
                    continue;

                PlayerInfo aiPlayer = new PlayerInfo
                {
                    Name = dd.Items[dd.SelectedIndex].Text,
                    AILevel = 2 - (dd.SelectedIndex - 1),
                    SideId = Math.Max(ddPlayerSides[cmbId].SelectedIndex, 0),
                    ColorId = Math.Max(ddPlayerColors[cmbId].SelectedIndex, 0),
                    StartingLocation = Math.Max(ddPlayerStarts[cmbId].SelectedIndex, 0),
                    TeamId = Map != null && Map.IsCoop ? 1 : Math.Max(ddPlayerTeams[cmbId].SelectedIndex, 0),
                    IsAI = true
                };

                AIPlayers.Add(aiPlayer);
            }

            CopyPlayerDataToUI();
            btnLaunchGame.SetRank(GetRank());

            if (oldSideId != Players.Find(p => p.Name == ProgramConstants.PLAYERNAME)?.SideId)
                UpdateDiscordPresence();
        }

        /// <summary>
        /// Sets the ready status of all non-host human players to false.
        /// </summary>
        /// <param name="resetAutoReady">If set, players with autoready enabled are reset as well.</param>
        protected void ClearReadyStatuses(bool resetAutoReady = false)
        {
            for (int i = 1; i < Players.Count; i++)
            {
                if (resetAutoReady || !Players[i].AutoReady || Players[i].IsInGame)
                    Players[i].Ready = false;
            }
        }

        private bool CanRightClickMultiplayer(XNADropDownItem selectedPlayer)
        {
            return selectedPlayer != null &&
                   selectedPlayer.Text != ProgramConstants.PLAYERNAME &&
                   !ProgramConstants.AI_PLAYER_NAMES.Contains(selectedPlayer.Text);
        }

        private void MultiplayerName_RightClick(object sender, EventArgs e)
        {
            var selectedPlayer = ((XNADropDown)sender).SelectedItem;
            if (!CanRightClickMultiplayer(selectedPlayer))
                return;

            if (selectedPlayer == null ||
                selectedPlayer.Text == ProgramConstants.PLAYERNAME)
            {
                return;
            }

            MultiplayerNameRightClicked?.Invoke(this, new MultiplayerNameRightClickedEventArgs(selectedPlayer.Text));
        }

        /// <summary>
        /// Applies player information changes done in memory to the UI.
        /// </summary>
        protected virtual void CopyPlayerDataToUI()
        {
            PlayerUpdatingInProgress = true;

            bool allowOptionsChange = AllowPlayerOptionsChange();
            var playerExtraOptions = GetPlayerExtraOptions();

            // Human players
            for (int pId = 0; pId < Players.Count; pId++)
            {
                PlayerInfo pInfo = Players[pId];

                pInfo.Index = pId;

                XNADropDown ddPlayerName = ddPlayerNames[pId];
                ddPlayerName.Items[0].Text = pInfo.Name;
                ddPlayerName.Items[1].Text = string.Empty;
                ddPlayerName.Items[2].Text = "Kick";
                ddPlayerName.Items[3].Text = "Ban";
                ddPlayerName.SelectedIndex = 0;
                ddPlayerName.AllowDropDown = false;

                bool allowPlayerOptionsChange = allowOptionsChange || pInfo.Name == ProgramConstants.PLAYERNAME;

                ddPlayerSides[pId].SelectedIndex = pInfo.SideId;
                ddPlayerSides[pId].AllowDropDown = !playerExtraOptions.IsForceRandomSides && allowPlayerOptionsChange;

                ddPlayerColors[pId].SelectedIndex = pInfo.ColorId;
                ddPlayerColors[pId].AllowDropDown = !playerExtraOptions.IsForceRandomColors && allowPlayerOptionsChange;

                ddPlayerStarts[pId].SelectedIndex = pInfo.StartingLocation;

                ddPlayerTeams[pId].SelectedIndex = pInfo.TeamId;
                if (GameModeMap != null)
                {
                    ddPlayerTeams[pId].AllowDropDown = !playerExtraOptions.IsForceRandomTeams && allowPlayerOptionsChange && !Map.IsCoop && !Map.ForceNoTeams && !GameMode.ForceNoTeams;
                    ddPlayerStarts[pId].AllowDropDown = !playerExtraOptions.IsForceRandomStarts && allowPlayerOptionsChange && (Map.IsCoop || !Map.ForceRandomStartLocations && !GameMode.ForceRandomStartLocations);
                }
            }

            // AI players
            for (int aiId = 0; aiId < AIPlayers.Count; aiId++)
            {
                PlayerInfo aiInfo = AIPlayers[aiId];

                int index = Players.Count + aiId;

                aiInfo.Index = index;

                XNADropDown ddPlayerName = ddPlayerNames[index];
                ddPlayerName.Items[0].Text = "-";
                ddPlayerName.Items[1].Text = ProgramConstants.AI_PLAYER_NAMES[0];
                ddPlayerName.Items[2].Text = ProgramConstants.AI_PLAYER_NAMES[1];
                ddPlayerName.Items[3].Text = ProgramConstants.AI_PLAYER_NAMES[2];
                ddPlayerName.SelectedIndex = 3 - aiInfo.AILevel;
                ddPlayerName.AllowDropDown = allowOptionsChange;

                ddPlayerSides[index].SelectedIndex = aiInfo.SideId;
                ddPlayerSides[index].AllowDropDown = !playerExtraOptions.IsForceRandomSides && allowOptionsChange;

                ddPlayerColors[index].SelectedIndex = aiInfo.ColorId;
                ddPlayerColors[index].AllowDropDown = !playerExtraOptions.IsForceRandomColors && allowOptionsChange;

                ddPlayerStarts[index].SelectedIndex = aiInfo.StartingLocation;

                ddPlayerTeams[index].SelectedIndex = aiInfo.TeamId;

                if (GameModeMap != null)
                {
                    ddPlayerTeams[index].AllowDropDown = !playerExtraOptions.IsForceRandomTeams && allowOptionsChange && !Map.IsCoop && !Map.ForceNoTeams && !GameMode.ForceNoTeams;
                    ddPlayerStarts[index].AllowDropDown = !playerExtraOptions.IsForceRandomStarts && allowOptionsChange && (Map.IsCoop || !Map.ForceRandomStartLocations && !GameMode.ForceRandomStartLocations);
                }
            }

            // Unused player slots
            for (int ddIndex = Players.Count + AIPlayers.Count; ddIndex < MAX_PLAYER_COUNT; ddIndex++)
            {
                XNADropDown ddPlayerName = ddPlayerNames[ddIndex];
                ddPlayerName.AllowDropDown = false;
                ddPlayerName.Items[0].Text = string.Empty;
                ddPlayerName.Items[1].Text = ProgramConstants.AI_PLAYER_NAMES[0];
                ddPlayerName.Items[2].Text = ProgramConstants.AI_PLAYER_NAMES[1];
                ddPlayerName.Items[3].Text = ProgramConstants.AI_PLAYER_NAMES[2];
                ddPlayerName.SelectedIndex = 0;

                ddPlayerSides[ddIndex].SelectedIndex = -1;
                ddPlayerSides[ddIndex].AllowDropDown = false;

                ddPlayerColors[ddIndex].SelectedIndex = -1;
                ddPlayerColors[ddIndex].AllowDropDown = false;

                ddPlayerStarts[ddIndex].SelectedIndex = -1;
                ddPlayerStarts[ddIndex].AllowDropDown = false;

                ddPlayerTeams[ddIndex].SelectedIndex = -1;
                ddPlayerTeams[ddIndex].AllowDropDown = false;
            }

            if (allowOptionsChange && Players.Count + AIPlayers.Count < MAX_PLAYER_COUNT)
                ddPlayerNames[Players.Count + AIPlayers.Count].AllowDropDown = true;

            MapPreviewBox.UpdateStartingLocationTexts();
            UpdateMapPreviewBoxEnabledStatus();

            PlayerUpdatingInProgress = false;
        }

        /// <summary>
        /// Updates the enabled status of starting location selectors
        /// in the map preview box.
        /// </summary>
        protected abstract void UpdateMapPreviewBoxEnabledStatus();

        /// <summary>
        /// Override this in a derived class to kick players.
        /// </summary>
        /// <param name="playerIndex">The index of the player that should be kicked.</param>
        protected virtual void KickPlayer(int playerIndex)
        {
            // Do nothing by default
        }

        /// <summary>
        /// Override this in a derived class to ban players.
        /// </summary>
        /// <param name="playerIndex">The index of the player that should be banned.</param>
        protected virtual void BanPlayer(int playerIndex)
        {
            // Do nothing by default
        }

        /// <summary>
        /// Changes the current map and game mode.
        /// </summary>
        /// <param name="gameModeMap">The new game mode map.</param>
        protected virtual void ChangeMap(GameModeMap gameModeMap)
        {
            GameModeMap = gameModeMap;

            if (GameMode == null || Map == null)
            {
                lblMapName.Text = "地图: 未知";
                //lblMapAuthor.Text = "作者: 未知";
                lblGameMode.Text = "游戏模式: 未知";
                lblMapSize.Text = "Size: 未知";

                //lblMapAuthor.X = MapPreviewBox.Right - lblMapAuthor.Width;

                MapPreviewBox.GameModeMap = null;

                return;
            }

            lblMapName.Text = "地图: " + Renderer.GetSafeString(Map.Name, lblMapName.FontIndex);
            //lblMapAuthor.Text = "作者 " + Renderer.GetSafeString(Map.Author, lblMapAuthor.FontIndex);
            lblGameMode.Text = "游戏模式: " + GameMode.UIName;
            lblMapSize.Text = "大小: " + Map.GetSizeString();

            disableGameOptionUpdateBroadcast = true;

            // Clear forced options
            foreach (var ddGameOption in DropDowns)
                ddGameOption.AllowDropDown = true;

            foreach (var checkBox in CheckBoxes)
                checkBox.AllowChecking = true;

            // We could either pass the CheckBoxes and DropDowns of this class
            // to the Map and GameMode instances and let them apply their forced
            // options, or we could do it in this class with helper functions.
            // The second approach is probably clearer.

            // We use these temp lists to determine which options WERE NOT forced
            // by the map. We then return these to user-defined settings.
            // This prevents forced options from one map getting carried
            // to other maps.

            var checkBoxListClone = new List<GameLobbyCheckBox>(CheckBoxes);
            var dropDownListClone = new List<GameLobbyDropDown>(DropDowns);

            ApplyForcedCheckBoxOptions(checkBoxListClone, GameMode.ForcedCheckBoxValues);
            ApplyForcedCheckBoxOptions(checkBoxListClone, Map.ForcedCheckBoxValues);

            ApplyForcedDropDownOptions(dropDownListClone, GameMode.ForcedDropDownValues);
            ApplyForcedDropDownOptions(dropDownListClone, Map.ForcedDropDownValues);

            foreach (var chkBox in checkBoxListClone)
                chkBox.Checked = chkBox.HostChecked;

            foreach (var dd in dropDownListClone)
                dd.SelectedIndex = dd.HostSelectedIndex;

            // Enable all sides by default
            foreach (var ddSide in ddPlayerSides)
            {
                ddSide.Items.ForEach(item => item.Selectable = true);
            }

            // Enable all colors by default
            foreach (var ddColor in ddPlayerColors)
            {
                ddColor.Items.ForEach(item => item.Selectable = true);
            }

            // Apply starting locations
            foreach (var ddStart in ddPlayerStarts)
            {
                ddStart.Items.Clear();

                ddStart.AddItem("???");

                for (int i = 1; i <= Map.MaxPlayers; i++)
                    ddStart.AddItem(i.ToString());
            }


            // Check if AI players allowed
            bool AIAllowed = !(Map.MultiplayerOnly || GameMode.MultiplayerOnly) ||
                             !(Map.HumanPlayersOnly || GameMode.HumanPlayersOnly);
            foreach (var ddName in ddPlayerNames)
            {
                if (ddName.Items.Count > 3)
                {
                    ddName.Items[1].Selectable = AIAllowed;
                    ddName.Items[2].Selectable = AIAllowed;
                    ddName.Items[3].Selectable = AIAllowed;
                }
            }

            if (!AIAllowed) AIPlayers.Clear();
            IEnumerable<PlayerInfo> concatPlayerList = Players.Concat(AIPlayers).ToList();

            foreach (PlayerInfo pInfo in concatPlayerList)
            {
                if (pInfo.StartingLocation > Map.MaxPlayers ||
                    (!Map.IsCoop && (Map.ForceRandomStartLocations || GameMode.ForceRandomStartLocations)))
                    pInfo.StartingLocation = 0;
                if (!Map.IsCoop && (Map.ForceNoTeams || GameMode.ForceNoTeams))
                    pInfo.TeamId = 0;
            }

            CheckDisallowedSides();


            if (Map.CoopInfo != null)
            {
                // Co-Op map disallowed color logic
                foreach (int disallowedColorIndex in Map.CoopInfo.DisallowedPlayerColors)
                {
                    if (disallowedColorIndex >= MPColors.Count)
                        continue;

                    foreach (XNADropDown ddColor in ddPlayerColors)
                        ddColor.Items[disallowedColorIndex + 1].Selectable = false;

                    foreach (PlayerInfo pInfo in concatPlayerList)
                    {
                        if (pInfo.ColorId == disallowedColorIndex + 1)
                            pInfo.ColorId = 0;
                    }
                }

                foreach (int disallowedStartIndex in Map.CoopInfo.DisallowedPlayerStarts)
                {
                    //if (disallowedStartIndex >= MPColors.Count)
                    //    continue;

                    foreach (XNADropDown ddStart in ddPlayerStarts)
                    {
                        ddStart.Items[disallowedStartIndex].Selectable = false;
                        ddStart.SelectedIndex = ddStart.Items.Count-1;
                    }
                        

                    //foreach (PlayerInfo pInfo in concatPlayerList)
                    //{
                    //    if (pInfo.ColorId == disallowedColorIndex + 1)
                    //        pInfo.ColorId = 0;
                    //}

                }

                    // Force teams
                    foreach (PlayerInfo pInfo in concatPlayerList)
                    pInfo.TeamId = 1;
            }

            OnGameOptionChanged();
            
            MapPreviewBox.GameModeMap = GameModeMap;
            //MapPreviewBox.Initialize();
            //  MapPreviewBox.UpdateStartingLocationTexts();

            CopyPlayerDataToUI();

            disableGameOptionUpdateBroadcast = false;

            PlayerExtraOptionsPanel.UpdateForMap(Map);
        }

        private void ApplyForcedCheckBoxOptions(List<GameLobbyCheckBox> optionList,
            List<KeyValuePair<string, bool>> forcedOptions)
        {
            foreach (KeyValuePair<string, bool> option in forcedOptions)
            {
                GameLobbyCheckBox checkBox = CheckBoxes.Find(chk => chk.Name == option.Key);
                if (checkBox != null)
                {
                    checkBox.Checked = option.Value;
                    checkBox.AllowChecking = false;
                    optionList.Remove(checkBox);
                }
            }
        }

        private void ApplyForcedDropDownOptions(List<GameLobbyDropDown> optionList,
            List<KeyValuePair<string, int>> forcedOptions)
        {
            foreach (KeyValuePair<string, int> option in forcedOptions)
            {
                GameLobbyDropDown dropDown = DropDowns.Find(dd => dd.Name == option.Key);
                if (dropDown != null)
                {
                    dropDown.SelectedIndex = option.Value;
                    dropDown.AllowDropDown = false;
                    optionList.Remove(dropDown);
                }
            }
        }

        protected string AILevelToName(int aiLevel)
        {
            switch (aiLevel)
            {
                case 0:
                    return ProgramConstants.AI_PLAYER_NAMES[2];
                case 1:
                    return ProgramConstants.AI_PLAYER_NAMES[1];
                case 2:
                    return ProgramConstants.AI_PLAYER_NAMES[0];
            }

            return string.Empty;
        }

        protected GameType GetGameType()
        {
            int teamCount = GetPvPTeamCount();

            if (teamCount == 0)
                return GameType.FFA;

            if (teamCount == 1)
                return GameType.Coop;

            return GameType.TeamGame;
        }

        protected int GetRank()
        {
            if (GameMode == null || Map == null)
                return RANK_NONE;

            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
            {
                if ((checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenChecked && checkBox.Checked) ||
                    (checkBox.MapScoringMode == CheckBoxMapScoringMode.DenyWhenUnchecked && !checkBox.Checked))
                {
                    return RANK_NONE;
                }
            }

            PlayerInfo localPlayer = Players.Find(p => p.Name == ProgramConstants.PLAYERNAME);

            if (localPlayer == null)
                return RANK_NONE;

            if (IsPlayerSpectator(localPlayer))
                return RANK_NONE;

            // These variables are used by both the skirmish and multiplayer code paths
            int[] teamMemberCounts = new int[5];
            int lowestEnemyAILevel = 2;
            int highestAllyAILevel = 0;

            foreach (PlayerInfo aiPlayer in AIPlayers)
            {
                teamMemberCounts[aiPlayer.TeamId]++;

                if (aiPlayer.TeamId > 0 && aiPlayer.TeamId == localPlayer.TeamId)
                {
                    if (aiPlayer.ReversedAILevel > highestAllyAILevel)
                        highestAllyAILevel = aiPlayer.ReversedAILevel;
                }
                else
                {
                    if (aiPlayer.ReversedAILevel < lowestEnemyAILevel)
                        lowestEnemyAILevel = aiPlayer.ReversedAILevel;
                }
            }

            if (isMultiplayer)
            {
                if (Players.Count == 1)
                    return RANK_NONE;

                // PvP stars for 2-player and 3-player maps
                if (Map.MaxPlayers <= 3)
                {
                    List<PlayerInfo> filteredPlayers = Players.Where(p => !IsPlayerSpectator(p)).ToList();

                    if (AIPlayers.Count > 0)
                        return RANK_NONE;

                    if (filteredPlayers.Count != Map.MaxPlayers)
                        return RANK_NONE;

                    int localTeamIndex = localPlayer.TeamId;
                    if (localTeamIndex > 0 && filteredPlayers.Count(p => p.TeamId == localTeamIndex) > 1)
                        return RANK_NONE;

                    return RANK_HARD;
                }

                // Coop stars for maps with 4 or more players
                // See the code in StatisticsManager.GetRankForCoopMatch for the conditions

                if (Players.Find(p => IsPlayerSpectator(p)) != null)
                    return RANK_NONE;

                if (AIPlayers.Count == 0)
                    return RANK_NONE;

                if (Players.Find(p => p.TeamId != localPlayer.TeamId) != null)
                    return RANK_NONE;

                if (Players.Find(p => p.TeamId == 0) != null)
                    return RANK_NONE;

                if (AIPlayers.Find(p => p.TeamId == 0) != null)
                    return RANK_NONE;

                teamMemberCounts[localPlayer.TeamId] += Players.Count;

                if (lowestEnemyAILevel < highestAllyAILevel)
                {
                    // Check that the player's AI allies aren't stronger 
                    return RANK_NONE;
                }

                // Check that all teams have at least as many players
                // as the human players' team
                int allyCount = teamMemberCounts[localPlayer.TeamId];

                for (int i = 1; i < 5; i++)
                {
                    if (i == localPlayer.TeamId)
                        continue;

                    if (teamMemberCounts[i] > 0)
                    {
                        if (teamMemberCounts[i] < allyCount)
                            return RANK_NONE;
                    }
                }

                return lowestEnemyAILevel + 1;
            }

            // *********
            // Skirmish!
            // *********

            if (AIPlayers.Count != Map.MaxPlayers - 1)
                return RANK_NONE;

            teamMemberCounts[localPlayer.TeamId]++;

            if (lowestEnemyAILevel < highestAllyAILevel)
            {
                // Check that the player's AI allies aren't stronger 
                return RANK_NONE;
            }

            if (localPlayer.TeamId > 0)
            {
                // Check that all teams have at least as many players
                // as the local player's team
                int allyCount = teamMemberCounts[localPlayer.TeamId];

                for (int i = 1; i < 5; i++)
                {
                    if (i == localPlayer.TeamId)
                        continue;

                    if (teamMemberCounts[i] > 0)
                    {
                        if (teamMemberCounts[i] < allyCount)
                            return RANK_NONE;
                    }
                }

                // Check that there is a team other than the players' team that is at least as large
                bool pass = false;
                for (int i = 1; i < 5; i++)
                {
                    if (i == localPlayer.TeamId)
                        continue;

                    if (teamMemberCounts[i] >= allyCount)
                    {
                        pass = true;
                        break;
                    }
                }

                if (!pass)
                    return RANK_NONE;
            }

            return lowestEnemyAILevel + 1;
        }

        protected string AddGameOptionPreset(string name)
        {
            string error = GameOptionPreset.IsNameValid(name);
            if (!string.IsNullOrEmpty(error))
                return error;

            GameOptionPreset preset = new GameOptionPreset(name);
            foreach (GameLobbyCheckBox checkBox in CheckBoxes)
            {
                preset.AddCheckBoxValue(checkBox.Name, checkBox.Checked);
            }

            foreach (GameLobbyDropDown dropDown in DropDowns)
            {
                preset.AddDropDownValue(dropDown.Name, dropDown.SelectedIndex);
            }

            GameOptionPresets.Instance.AddPreset(preset);
            return null;
        }

        public bool LoadGameOptionPreset(string name)
        {
            GameOptionPreset preset = GameOptionPresets.Instance.GetPreset(name);
            if (preset == null)
                return false;

            disableGameOptionUpdateBroadcast = true;

            var checkBoxValues = preset.GetCheckBoxValues();
            foreach (var kvp in checkBoxValues)
            {
                GameLobbyCheckBox checkBox = CheckBoxes.Find(c => c.Name == kvp.Key);
                if (checkBox != null && checkBox.AllowChanges && checkBox.AllowChecking)
                    checkBox.Checked = kvp.Value;
            }

            var dropDownValues = preset.GetDropDownValues();
            foreach (var kvp in dropDownValues)
            {
                GameLobbyDropDown dropDown = DropDowns.Find(d => d.Name == kvp.Key);
                if (dropDown != null && dropDown.AllowDropDown)
                    dropDown.SelectedIndex = kvp.Value;
            }

            disableGameOptionUpdateBroadcast = false;
            OnGameOptionChanged();
            return true;
        }

        protected abstract bool AllowPlayerOptionsChange();
    }
}
