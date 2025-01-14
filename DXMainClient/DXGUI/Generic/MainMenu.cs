using ClientCore;
using ClientGUI;
using DTAClient.Domain;
using DTAClient.Domain.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer;
using DTAClient.DXGUI.Multiplayer.CnCNet;
using DTAClient.DXGUI.Multiplayer.GameLobby;
using DTAClient.Online;
using DTAConfig;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Updater;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// The main menu of the client.
    /// </summary>
    class MainMenu : XNAWindow, ISwitchable
    {
        private const float MEDIA_PLAYER_VOLUME_FADE_STEP = 0.01f;
        private const float MEDIA_PLAYER_VOLUME_EXIT_FADE_STEP = 0.025f;
        private const double UPDATE_RE_CHECK_THRESHOLD = 30.0;

        /// <summary>
        /// Creates a new instance of the main menu.
        /// </summary>
        public MainMenu(WindowManager windowManager, SkirmishLobby skirmishLobby,
            LANLobby lanLobby, TopBar topBar, OptionsWindow optionsWindow,
            CnCNetLobby cncnetLobby,
            CnCNetManager connectionManager, DiscordHandler discordHandler) : base(windowManager)
        {
            this.skirmishLobby = skirmishLobby;
            this.lanLobby = lanLobby;
            this.topBar = topBar;
            this.connectionManager = connectionManager;
            this.optionsWindow = optionsWindow;
            this.cncnetLobby = cncnetLobby;
            this.discordHandler = discordHandler;
            cncnetLobby.UpdateCheck += CncnetLobby_UpdateCheck;
            isMediaPlayerAvailable = IsMediaPlayerAvailable();
        }

        private MainMenuDarkeningPanel innerPanel;

        private XNALabel lblCnCNetPlayerCount;
        private XNALinkLabel lblUpdateStatus;
        private XNALinkLabel lblVersion;

        private CnCNetLobby cncnetLobby;

        private SkirmishLobby skirmishLobby;

        private LANLobby lanLobby;

        private CnCNetManager connectionManager;

        private OptionsWindow optionsWindow;

        private DiscordHandler discordHandler;

        private TopBar topBar;

        private XNAMessageBox firstRunMessageBox;

        private bool _updateInProgress;
        private bool UpdateInProgress
        {
            get { return _updateInProgress; }
            set
            {
                _updateInProgress = value;
                topBar.SetSwitchButtonsClickable(!_updateInProgress);
                topBar.SetOptionsButtonClickable(!_updateInProgress);
                SetButtonHotkeys(!_updateInProgress);
            }
        }

        private bool customComponentDialogQueued = false;

        private DateTime lastUpdateCheckTime;

        private Song themeSong;

        private static readonly object locker = new object();

        private bool isMusicFading = false;

        private readonly bool isMediaPlayerAvailable;

        private CancellationTokenSource cncnetPlayerCountCancellationSource;

        private string FA2Path;

        // Main Menu Buttons
        private XNAClientButton btnNewCampaign;
        private XNAClientButton btnLoadGame;
        private XNAClientButton btnSkirmish;
        private XNAClientButton btnCnCNet;
        private XNAClientButton btnLan;
        private XNAClientButton btnOptions;
        private XNAClientButton btnMapEditor;
        private XNAClientButton btnStatistics;
        private XNAClientButton btnCredits;
        private XNAClientButton btnExtras;

        /// <summary>
        /// Initializes the main menu's controls.
        /// </summary>
        public override void Initialize()
        {
            GameProcessLogic.GameProcessExited += SharedUILogic_GameProcessExited;

            Name = nameof(MainMenu);
            BackgroundTexture = AssetLoader.LoadTexture("MainMenu/mainmenubg.png");
            ClientRectangle = new Rectangle(0, 0, BackgroundTexture.Width, BackgroundTexture.Height);

            WindowManager.CenterControlOnScreen(this);

            btnNewCampaign = new XNAClientButton(WindowManager);
            btnNewCampaign.Name = nameof(btnNewCampaign);
            btnNewCampaign.IdleTexture = AssetLoader.LoadTexture("MainMenu/campaign.png");
            btnNewCampaign.HoverTexture = AssetLoader.LoadTexture("MainMenu/campaign_c.png");
            btnNewCampaign.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnNewCampaign.Text = "战役";
            btnNewCampaign.LeftClick += BtnNewCampaign_LeftClick;

            btnLoadGame = new XNAClientButton(WindowManager);
            btnLoadGame.Name = nameof(btnLoadGame);
            btnLoadGame.IdleTexture = AssetLoader.LoadTexture("MainMenu/loadmission.png");
            btnLoadGame.HoverTexture = AssetLoader.LoadTexture("MainMenu/loadmission_c.png");
            btnLoadGame.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnLoadGame.Text = "载入存档";
            btnLoadGame.LeftClick += BtnLoadGame_LeftClick;

            btnSkirmish = new XNAClientButton(WindowManager);
            btnSkirmish.Name = nameof(btnSkirmish);
            btnSkirmish.IdleTexture = AssetLoader.LoadTexture("MainMenu/skirmish.png");
            btnSkirmish.HoverTexture = AssetLoader.LoadTexture("MainMenu/skirmish_c.png");
            btnSkirmish.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnSkirmish.Text = "遭遇战";
            btnSkirmish.LeftClick += BtnSkirmish_LeftClick;

            btnCnCNet = new XNAClientButton(WindowManager);
            btnCnCNet.Name = nameof(btnCnCNet);
            btnCnCNet.IdleTexture = AssetLoader.LoadTexture("MainMenu/cncnet.png");
            btnCnCNet.HoverTexture = AssetLoader.LoadTexture("MainMenu/cncnet_c.png");
            btnCnCNet.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnCnCNet.Text = "联机大厅";
            btnCnCNet.LeftClick += BtnCnCNet_LeftClick;


            btnLan = new XNAClientButton(WindowManager);
            btnLan.Name = nameof(btnLan);
            btnLan.IdleTexture = AssetLoader.LoadTexture("MainMenu/lan.png");
            btnLan.HoverTexture = AssetLoader.LoadTexture("MainMenu/lan_c.png");
            btnLan.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnLan.Text = "局域网";
            btnLan.LeftClick += BtnLan_LeftClick;

            btnOptions = new XNAClientButton(WindowManager);
            btnOptions.Name = nameof(btnOptions);
            btnOptions.IdleTexture = AssetLoader.LoadTexture("MainMenu/options.png");
            btnOptions.HoverTexture = AssetLoader.LoadTexture("MainMenu/options_c.png");
            btnOptions.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnOptions.Text = "选项";
            btnOptions.LeftClick += BtnOptions_LeftClick;

            btnMapEditor = new XNAClientButton(WindowManager);
            btnMapEditor.Name = nameof(btnMapEditor);
            btnMapEditor.IdleTexture = AssetLoader.LoadTexture("MainMenu/mapeditor.png");
            btnMapEditor.HoverTexture = AssetLoader.LoadTexture("MainMenu/mapeditor_c.png");
            btnMapEditor.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnMapEditor.LeftClick += BtnMapEditor_LeftClick;
            btnMapEditor.Text = "地图编辑器";
        //    btnMapEditor.Enabled = false;

            btnStatistics = new XNAClientButton(WindowManager);
            btnStatistics.Name = nameof(btnStatistics);
            btnStatistics.IdleTexture = AssetLoader.LoadTexture("MainMenu/statistics.png");
            btnStatistics.HoverTexture = AssetLoader.LoadTexture("MainMenu/statistics_c.png");
            btnStatistics.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnStatistics.LeftClick += BtnStatistics_LeftClick;

            btnCredits = new XNAClientButton(WindowManager);
            btnCredits.Name = nameof(btnCredits);
            btnCredits.IdleTexture = AssetLoader.LoadTexture("MainMenu/credits.png");
            btnCredits.HoverTexture = AssetLoader.LoadTexture("MainMenu/credits_c.png");
            btnCredits.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnCredits.LeftClick += BtnCredits_LeftClick;

            btnExtras = new XNAClientButton(WindowManager);
            btnExtras.Name = nameof(btnExtras);
            btnExtras.IdleTexture = AssetLoader.LoadTexture("MainMenu/extras.png");
            btnExtras.HoverTexture = AssetLoader.LoadTexture("MainMenu/extras_c.png");
            btnExtras.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnExtras.LeftClick += BtnExtras_LeftClick;

            var btnExit = new XNAClientButton(WindowManager);
            btnExit.Name = nameof(btnExit);
            btnExit.IdleTexture = AssetLoader.LoadTexture("MainMenu/exitgame.png");
            btnExit.HoverTexture = AssetLoader.LoadTexture("MainMenu/exitgame_c.png");
            btnExit.HoverSoundEffect = new EnhancedSoundEffect("MainMenu/button.wav");
            btnExit.LeftClick += BtnExit_LeftClick;

            XNALabel lblCnCNetStatus = new XNALabel(WindowManager);
            lblCnCNetStatus.Name = nameof(lblCnCNetStatus);
            lblCnCNetStatus.Text = "CNC大厅里的玩家数:";
            lblCnCNetStatus.ClientRectangle = new Rectangle(12, 9, 0, 0);

            lblCnCNetPlayerCount = new XNALabel(WindowManager);
            lblCnCNetPlayerCount.Name = nameof(lblCnCNetPlayerCount);
            lblCnCNetPlayerCount.Text = "-";

            //版本号
            lblVersion = new XNALinkLabel(WindowManager);
            lblVersion.Name = nameof(lblVersion);
            lblVersion.Text = "1.0";
            lblVersion.ClientRectangle = new Rectangle(950,685,0,0);
            lblVersion.LeftClick += LblVersion_LeftClick;

            lblUpdateStatus = new XNALinkLabel(WindowManager);
            lblUpdateStatus.Name = nameof(lblUpdateStatus);
            lblUpdateStatus.LeftClick += LblUpdateStatus_LeftClick;
            lblUpdateStatus.ClientRectangle = new Rectangle(0, 0, UIDesignConstants.BUTTON_WIDTH_160, 20);

            AddChild(btnNewCampaign);
            AddChild(btnLoadGame);
            AddChild(btnSkirmish);
            AddChild(btnCnCNet);
            AddChild(btnLan);
            AddChild(btnOptions);
            AddChild(btnMapEditor);
            AddChild(btnStatistics);
            AddChild(btnCredits);
            AddChild(btnExtras);
            AddChild(btnExit);
            AddChild(lblCnCNetStatus);
            AddChild(lblCnCNetPlayerCount);
            AddChild(lblVersion);
            if (!ClientConfiguration.Instance.ModMode)
            {
                // ModMode disables version tracking and the updater if it's enabled

                AddChild(lblVersion);
                AddChild(lblUpdateStatus);

                CUpdater.FileIdentifiersUpdated += CUpdater_FileIdentifiersUpdated;
                CUpdater.OnCustomComponentsOutdated += CUpdater_OnCustomComponentsOutdated;
            }


            FA2Path = ProgramConstants.GamePath + ClientConfiguration.Instance.MapEditorExePath;
            if (!File.Exists(FA2Path))
            {
                Logger.Log("没有找到地编");
                btnMapEditor.Enabled = false;
            }
            else
            {
                IniFile ini = new IniFile(ProgramConstants.GamePath + "FinalAlert2SP/FinalAlert.ini", Encoding.GetEncoding("GBK"));
                ini.SetStringValue("TS", "Exe", (Encoding.GetEncoding("GBK").GetString(Encoding.Default.GetBytes(ProgramConstants.GamePath))+ "gamemd.exe").Replace('/','\\')); //地编路径必须是\，这里写两个是因为有一个是转义符
                ini.WriteIniFile();
            }

            base.Initialize(); // Read control attributes from INI

            innerPanel = new MainMenuDarkeningPanel(WindowManager, discordHandler);
            innerPanel.ClientRectangle = new Rectangle(0, 0,
                Width,
                Height);
            innerPanel.DrawOrder = int.MaxValue;
            innerPanel.UpdateOrder = int.MaxValue;
            AddChild(innerPanel);
            innerPanel.Hide();

            //lblVersion.Text = CUpdater.GameVersion;

            innerPanel.UpdateQueryWindow.UpdateDeclined += UpdateQueryWindow_UpdateDeclined;
            innerPanel.UpdateQueryWindow.UpdateAccepted += UpdateQueryWindow_UpdateAccepted;

            innerPanel.UpdateWindow.UpdateCompleted += UpdateWindow_UpdateCompleted;
            innerPanel.UpdateWindow.UpdateCancelled += UpdateWindow_UpdateCancelled;
            innerPanel.UpdateWindow.UpdateFailed += UpdateWindow_UpdateFailed;

            this.ClientRectangle = new Rectangle((WindowManager.RenderResolutionX - Width) / 2,
                (WindowManager.RenderResolutionY - Height) / 2,
                Width, Height);
            innerPanel.ClientRectangle = new Rectangle(0, 0,
                Math.Max(WindowManager.RenderResolutionX, Width),
                Math.Max(WindowManager.RenderResolutionY, Height));

            CnCNetPlayerCountTask.CnCNetGameCountUpdated += CnCNetInfoController_CnCNetGameCountUpdated;
            cncnetPlayerCountCancellationSource = new CancellationTokenSource();
            CnCNetPlayerCountTask.InitializeService(cncnetPlayerCountCancellationSource);

            WindowManager.GameClosing += WindowManager_GameClosing;

            skirmishLobby.Exited += SkirmishLobby_Exited;
            lanLobby.Exited += LanLobby_Exited;
            optionsWindow.EnabledChanged += OptionsWindow_EnabledChanged;

            optionsWindow.OnForceUpdate += (s, e) => ForceUpdate();

            GameProcessLogic.GameProcessStarted += SharedUILogic_GameProcessStarted;
            GameProcessLogic.GameProcessStarting += SharedUILogic_GameProcessStarting;

            UserINISettings.Instance.SettingsSaved += SettingsSaved;

            CUpdater.Restart += CUpdater_Restart;

            SetButtonHotkeys(true);
        }

        private void SetButtonHotkeys(bool enableHotkeys)
        {
            if (!Initialized)
                return;

            if (enableHotkeys)
            {
                btnNewCampaign.HotKey = Keys.C;
                btnLoadGame.HotKey = Keys.L;
                btnSkirmish.HotKey = Keys.S;
                btnCnCNet.HotKey = Keys.M;
                btnLan.HotKey = Keys.N;
                btnOptions.HotKey = Keys.O;
                btnMapEditor.HotKey = Keys.E;
                btnStatistics.HotKey = Keys.T;
                btnCredits.HotKey = Keys.R;
                btnExtras.HotKey = Keys.X;
            }
            else
            {
                btnNewCampaign.HotKey = Keys.None;
                btnLoadGame.HotKey = Keys.None;
                btnSkirmish.HotKey = Keys.None;
                btnCnCNet.HotKey = Keys.None;
                btnLan.HotKey = Keys.None;
                btnOptions.HotKey = Keys.None;
                btnMapEditor.HotKey = Keys.None;
                btnStatistics.HotKey = Keys.None;
                btnCredits.HotKey = Keys.None;
                btnExtras.HotKey = Keys.None;
            }
        }

        private void OptionsWindow_EnabledChanged(object sender, EventArgs e)
        {
            if (!optionsWindow.Enabled)
            {
                if (customComponentDialogQueued)
                    CUpdater_OnCustomComponentsOutdated();
            }
        }

        /// <summary>
        /// Refreshes settings. Called when the game process is starting.
        /// </summary>
        private void SharedUILogic_GameProcessStarting()
        {
            UserINISettings.Instance.ReloadSettings();

            try
            {
                optionsWindow.RefreshSettings();
            }
            catch (Exception ex)
            {
                Logger.Log("Refreshing settings failed! Exception message: " + ex.Message);
                // We don't want to show the dialog when starting a game
                //XNAMessageBox.Show(WindowManager, "Saving settings failed",
                //    "Saving settings failed! Error message: " + ex.Message);
            }
        }

        private void CUpdater_Restart(object sender, EventArgs e) =>
            WindowManager.AddCallback(new Action(ExitClient), null);

        /// <summary>
        /// Applies configuration changes (music playback and volume)
        /// when settings are saved.
        /// </summary>
        private void SettingsSaved(object sender, EventArgs e)
        {
            if (isMediaPlayerAvailable)
            {
                if (MediaPlayer.State == MediaState.Playing)
                {
                    if (!UserINISettings.Instance.PlayMainMenuMusic)
                        isMusicFading = true;
                }
                else if (topBar.GetTopMostPrimarySwitchable() == this &&
                    topBar.LastSwitchType == SwitchType.PRIMARY)
                {
                    PlayMusic();
                }
            }

            if (!connectionManager.IsConnected)
                ProgramConstants.PLAYERNAME = UserINISettings.Instance.PlayerName;

            if (UserINISettings.Instance.DiscordIntegration)
                discordHandler?.Connect();
            else
                discordHandler?.Disconnect();
        }

        /// <summary>
        /// Checks files which are required for the mod to function
        /// but not distributed with the mod (usually base game files
        /// for YR mods which can't be standalone).
        /// </summary>
        private void CheckRequiredFiles()
        {
            List<string> absentFiles = ClientConfiguration.Instance.RequiredFiles.ToList()
                .FindAll(f => !string.IsNullOrWhiteSpace(f) && !File.Exists(ProgramConstants.GamePath + f));

            if (absentFiles.Count > 0)
                XNAMessageBox.Show(WindowManager, "Missing Files",
#if ARES
                    "你丢失了尤里的复仇计划运行时所需的文件" + Environment.NewLine +
                    "游戏并不是独立的," + Environment.NewLine +
                    "因此你需要一份完整的尤里的复仇1.001文件" + Environment.NewLine +
                    "放在游戏文件夹中以游玩本游戏:" +
#else
                    "The following required files are missing:" +
#endif
                    Environment.NewLine + Environment.NewLine +
                    String.Join(Environment.NewLine, absentFiles) +
                    Environment.NewLine + Environment.NewLine +
                    "没有那些文件你就不能玩了.");
        }

        private void CheckForbiddenFiles()
        {
            List<string> presentFiles = ClientConfiguration.Instance.ForbiddenFiles.ToList()
                .FindAll(f => !string.IsNullOrWhiteSpace(f) && File.Exists(ProgramConstants.GamePath + f));

            if (presentFiles.Count > 0)
                XNAMessageBox.Show(WindowManager, "检测到干扰文件",
#if TS
                    "You have installed the mod on top of a Tiberian Sun" + Environment.NewLine +
                    "copy! This mod is standalone, therefore you have to" + Environment.NewLine +
                    "install it in an empty folder. Otherwise the mod won't" + Environment.NewLine +
                    "function correctly." +
                    Environment.NewLine + Environment.NewLine +
                    "Please reinstall the mod into an empty folder to play."
#else
                    "出现了以下干扰文件:" +
                    Environment.NewLine + Environment.NewLine +
                    String.Join(Environment.NewLine, presentFiles) +
                    Environment.NewLine + Environment.NewLine +
                    "没有删除这些文件，游戏可能将无法正常工作."
#endif
                    );
        }

        /// <summary>
        /// Checks whether the client is running for the first time.
        /// If it is, displays a dialog asking the user if they'd like
        /// to configure settings.
        /// </summary>
        private void CheckIfFirstRun()
        {
            if (UserINISettings.Instance.IsFirstRun)
            {
                UserINISettings.Instance.IsFirstRun.Value = false;
                UserINISettings.Instance.SaveSettings();


                firstRunMessageBox = XNAMessageBox.ShowYesNoDialog(WindowManager, "初次运行",
                    string.Format("检测到你是第一次运行{0}." + Environment.NewLine +
                    "强烈建议你在进行游戏之前调整游戏设置." +
                    Environment.NewLine + "你想要现在进行配置吗？", ClientConfiguration.Instance.LocalGame));

                firstRunMessageBox.YesClickedAction = FirstRunMessageBox_YesClicked;
                firstRunMessageBox.NoClickedAction = FirstRunMessageBox_NoClicked;
            }

            optionsWindow.PostInit();
        }

        private void FirstRunMessageBox_NoClicked(XNAMessageBox messageBox)
        {
            if (customComponentDialogQueued)
                CUpdater_OnCustomComponentsOutdated();
        }

        private void FirstRunMessageBox_YesClicked(XNAMessageBox messageBox) => optionsWindow.Open();

        private void SharedUILogic_GameProcessStarted() => MusicOff();

        private void WindowManager_GameClosing(object sender, EventArgs e) => Clean();

        private void SkirmishLobby_Exited(object sender, EventArgs e)
        {
            if (UserINISettings.Instance.StopMusicOnMenu)
                PlayMusic();
        }

        private void LanLobby_Exited(object sender, EventArgs e)
        {
            topBar.SetLanMode(false);

            if (UserINISettings.Instance.AutomaticCnCNetLogin)
                connectionManager.Connect();

            if (UserINISettings.Instance.StopMusicOnMenu)
                PlayMusic();
        }

        private void CnCNetInfoController_CnCNetGameCountUpdated(object sender, PlayerCountEventArgs e)
        {
            lock (locker)
            {
                if (e.PlayerCount == -1)
                    lblCnCNetPlayerCount.Text = "N/A";
                else
                    lblCnCNetPlayerCount.Text = e.PlayerCount.ToString();
            }
        }

        /// <summary>
        /// Attemps to "clean" the client session in a nice way if the user closes the game.
        /// </summary>
        private void Clean()
        {
            CUpdater.FileIdentifiersUpdated -= CUpdater_FileIdentifiersUpdated;

            if (cncnetPlayerCountCancellationSource != null) cncnetPlayerCountCancellationSource.Cancel();
            topBar.Clean();
            if (UpdateInProgress)
                CUpdater.TerminateUpdate = true;

            if (connectionManager.IsConnected)
                connectionManager.Disconnect();
        }

        /// <summary>
        /// Starts playing music, initiates an update check if automatic updates
        /// are enabled and checks whether the client is run for the first time.
        /// Called after all internal client UI logic has been initialized.
        /// </summary>
        public void PostInit()
        {
            themeSong = AssetLoader.LoadSong(ClientConfiguration.Instance.MainMenuMusicName);

            PlayMusic();

            if (!ClientConfiguration.Instance.ModMode)
            {
                if (UserINISettings.Instance.CheckForUpdates)
                    CheckForUpdates();
                else
                    lblUpdateStatus.Text = "单击以检查更新.";
            }

            CheckRequiredFiles();
            CheckForbiddenFiles();
            CheckIfFirstRun();
        }

        #region Updating / versioning system

        private void UpdateWindow_UpdateFailed(object sender, UpdateFailureEventArgs e)
        {
            innerPanel.Hide();
            lblUpdateStatus.Text = "Updating failed! Click to retry.";
            lblUpdateStatus.DrawUnderline = true;
            lblUpdateStatus.Enabled = true;
            UpdateInProgress = false;

            innerPanel.Show(null); // Darkening
            XNAMessageBox msgBox = new XNAMessageBox(WindowManager, "Update failed",
                string.Format("An error occured while updating. Returned error was: {0}" +
                Environment.NewLine + Environment.NewLine +
                "If you are connected to the Internet and your firewall isn't blocking" + Environment.NewLine +
                "{1}, and the issue is reproducible, contact us at " + Environment.NewLine +
                "{2} for support.",
                e.Reason, CUpdater.CURRENT_LAUNCHER_NAME, MainClientConstants.SUPPORT_URL_SHORT), XNAMessageBoxButtons.OK);
            msgBox.OKClickedAction = MsgBox_OKClicked;
            msgBox.Show();
        }

        private void MsgBox_OKClicked(XNAMessageBox messageBox)
        {
            innerPanel.Hide();
        }

        private void UpdateWindow_UpdateCancelled(object sender, EventArgs e)
        {
            innerPanel.Hide();
            lblUpdateStatus.Text = "The update was cancelled. Click to retry.";
            lblUpdateStatus.DrawUnderline = true;
            lblUpdateStatus.Enabled = true;
            UpdateInProgress = false;
        }

        private void UpdateWindow_UpdateCompleted(object sender, EventArgs e)
        {
            innerPanel.Hide();
            lblUpdateStatus.Text = MainClientConstants.GAME_NAME_SHORT + " was succesfully updated to v." + CUpdater.GameVersion;
            //lblVersion.Text = CUpdater.GameVersion;
            UpdateInProgress = false;
            lblUpdateStatus.Enabled = true;
            lblUpdateStatus.DrawUnderline = false;
        }

        private void LblUpdateStatus_LeftClick(object sender, EventArgs e)
        {
            Logger.Log(CUpdater.DTAVersionState.ToString());

            if (CUpdater.DTAVersionState == VersionState.OUTDATED ||
                CUpdater.DTAVersionState == VersionState.MISMATCHED ||
                CUpdater.DTAVersionState == VersionState.UNKNOWN ||
                CUpdater.DTAVersionState == VersionState.UPTODATE)
            {
                CheckForUpdates();
            }
        }

        private void LblVersion_LeftClick(object sender, EventArgs e)
        {
            Process.Start(ClientConfiguration.Instance.ChangelogURL);
        }

        private void ForceUpdate()
        {
            UpdateInProgress = true;
            innerPanel.Hide();
            innerPanel.UpdateWindow.ForceUpdate();
            innerPanel.Show(innerPanel.UpdateWindow);
            lblUpdateStatus.Text = "Force updating...";
        }

        /// <summary>
        /// Starts a check for updates.
        /// </summary>
        private void CheckForUpdates()
        {
            CUpdater.CheckForUpdates();
            lblUpdateStatus.Enabled = false;
            lblUpdateStatus.Text = "Checking for updates...";
            try
            {
                StatisticsSender.Instance.SendUpdate();
            }
            catch { }
            lastUpdateCheckTime = DateTime.Now;
        }

        private void CUpdater_FileIdentifiersUpdated()
        {
            WindowManager.AddCallback(new Action(HandleFileIdentifierUpdate), null);
        }

        /// <summary>
        /// Used for displaying the result of an update check in the UI.
        /// </summary>
        private void HandleFileIdentifierUpdate()
        {
            if (UpdateInProgress)
            {
                return;
            }

            if (CUpdater.DTAVersionState == VersionState.UPTODATE)
            {
                lblUpdateStatus.Text = MainClientConstants.GAME_NAME_SHORT + " is up to date.";
                lblUpdateStatus.Enabled = true;
                lblUpdateStatus.DrawUnderline = false;
            }
            else if (CUpdater.DTAVersionState == VersionState.OUTDATED)
            {
                lblUpdateStatus.Text = "An update is available.";
                innerPanel.UpdateQueryWindow.SetInfo(CUpdater.ServerGameVersion, CUpdater.UpdateSizeInKb);
                innerPanel.Show(innerPanel.UpdateQueryWindow);
            }
            else if (CUpdater.DTAVersionState == VersionState.UNKNOWN)
            {
                lblUpdateStatus.Text = "Checking for updates failed! Click to retry.";
                lblUpdateStatus.Enabled = true;
                lblUpdateStatus.DrawUnderline = true;
            }
        }

        /// <summary>
        /// Asks the user if they'd like to update their custom components.
        /// Handles an event raised by the updater when it has detected
        /// that the custom components are out of date.
        /// </summary>
        private void CUpdater_OnCustomComponentsOutdated()
        {
            if (innerPanel.UpdateQueryWindow.Visible)
                return;

            if (UpdateInProgress)
                return;

            if ((firstRunMessageBox != null && firstRunMessageBox.Visible) || optionsWindow.Enabled)
            {
                // If the custom components are out of date on the first run
                // or the options window is already open, don't show the dialog
                customComponentDialogQueued = true;
                return;
            }

            customComponentDialogQueued = false;

            XNAMessageBox ccMsgBox = XNAMessageBox.ShowYesNoDialog(WindowManager,
                "Custom Component Updates Available",
                "Updates for custom components are available. Do you want to open" + Environment.NewLine +
                "the Options menu where you can update the custom components?");
            ccMsgBox.YesClickedAction = CCMsgBox_YesClicked;
        }

        private void CCMsgBox_YesClicked(XNAMessageBox messageBox)
        {
            optionsWindow.Open();
            optionsWindow.SwitchToCustomComponentsPanel();
        }

        /// <summary>
        /// Called when the user has declined an update.
        /// </summary>
        private void UpdateQueryWindow_UpdateDeclined(object sender, EventArgs e)
        {
            UpdateQueryWindow uqw = (UpdateQueryWindow)sender;
            innerPanel.Hide();
            lblUpdateStatus.Text = "An update is available, click to install.";
            lblUpdateStatus.Enabled = true;
            lblUpdateStatus.DrawUnderline = true;
        }

        /// <summary>
        /// Called when the user has accepted an update.
        /// </summary>
        private void UpdateQueryWindow_UpdateAccepted(object sender, EventArgs e)
        {
            innerPanel.Hide();
            innerPanel.UpdateWindow.SetData(CUpdater.ServerGameVersion);
            innerPanel.Show(innerPanel.UpdateWindow);
            lblUpdateStatus.Text = "Updating...";
            UpdateInProgress = true;
            CUpdater.StartAsyncUpdate();
        }

        #endregion

        private void BtnOptions_LeftClick(object sender, EventArgs e) => optionsWindow.Open();

        private void BtnNewCampaign_LeftClick(object sender, EventArgs e) =>
            innerPanel.Show(innerPanel.CampaignSelector);

        private void BtnLoadGame_LeftClick(object sender, EventArgs e) =>
            innerPanel.Show(innerPanel.GameLoadingWindow);

        private void BtnLan_LeftClick(object sender, EventArgs e)
        {
            lanLobby.Open();

            if (UserINISettings.Instance.StopMusicOnMenu)
                MusicOff();

            if (connectionManager.IsConnected)
                connectionManager.Disconnect();

            topBar.SetLanMode(true);
        }

        private void BtnCnCNet_LeftClick(object sender, EventArgs e) => topBar.SwitchToSecondary();

        private void BtnSkirmish_LeftClick(object sender, EventArgs e)
        {
            skirmishLobby.Open();

            if (UserINISettings.Instance.StopMusicOnMenu)
                MusicOff();
        }

        private void BtnMapEditor_LeftClick(object sender, EventArgs e) => LaunchMapEditor();

        private void BtnStatistics_LeftClick(object sender, EventArgs e) =>
            innerPanel.Show(innerPanel.StatisticsWindow);

        private void BtnCredits_LeftClick(object sender, EventArgs e) =>
            Process.Start(MainClientConstants.CREDITS_URL);

        private void BtnExtras_LeftClick(object sender, EventArgs e) =>
            innerPanel.Show(innerPanel.ExtrasWindow);

        private void BtnExit_LeftClick(object sender, EventArgs e)
        {
            WindowManager.HideWindow();
            FadeMusicExit();
        }

        private void SharedUILogic_GameProcessExited() =>
            AddCallback(new Action(HandleGameProcessExited), null);

        private void HandleGameProcessExited()
        {
            innerPanel.GameLoadingWindow.ListSaves();
            innerPanel.Hide();

            // If music is disabled on menus, check if the main menu is the top-most
            // window of the top bar and only play music if it is
            // LAN has the top bar disabled, so to detect the LAN game lobby
            // we'll check whether the top bar is enabled
            if (!UserINISettings.Instance.StopMusicOnMenu ||
                (topBar.Enabled && topBar.LastSwitchType == SwitchType.PRIMARY &&
                topBar.GetTopMostPrimarySwitchable() == this))
                PlayMusic();
        }

        /// <summary>
        /// Switches to the main menu and performs a check for updates.
        /// </summary>
        private void CncnetLobby_UpdateCheck(object sender, EventArgs e)
        {
            CheckForUpdates();
            topBar.SwitchToPrimary();
        }

        public override void Update(GameTime gameTime)
        {
            if (isMusicFading)
                FadeMusic(gameTime);

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            lock (locker)
            {
                base.Draw(gameTime);
            }
        }

        /// <summary>
        /// Attempts to start playing the menu music.
        /// </summary>
        private void PlayMusic()
        {
            if (!isMediaPlayerAvailable)
                return; // SharpDX fails at music playback on Vista

            if (themeSong != null && UserINISettings.Instance.PlayMainMenuMusic)
            {
                isMusicFading = false;
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Volume = (float)UserINISettings.Instance.ClientVolume;

                try
                {
                    MediaPlayer.Play(themeSong);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Log("Playing main menu music failed! " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Lowers the volume of the menu music, or stops playing it if the
        /// volume is unaudibly low.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        private void FadeMusic(GameTime gameTime)
        {
            if (!isMediaPlayerAvailable || !isMusicFading || themeSong == null)
                return;

            // Fade during 1 second
            float step = SoundPlayer.Volume * (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (MediaPlayer.Volume > step)
                MediaPlayer.Volume -= step;
            else
            {
                MediaPlayer.Stop();
                isMusicFading = false;
            }
        }

        /// <summary>
        /// Exits the client. Quickly fades the music if it's playing.
        /// </summary>
        private void FadeMusicExit()
        {
            if (!isMediaPlayerAvailable || themeSong == null)
            {
                ExitClient();
                return;
            }

            float step = MEDIA_PLAYER_VOLUME_EXIT_FADE_STEP * (float)UserINISettings.Instance.ClientVolume;

            if (MediaPlayer.Volume > step)
            {
                MediaPlayer.Volume -= step;
                AddCallback(new Action(FadeMusicExit), null);
            }
            else
            {
                MediaPlayer.Stop();
                ExitClient();
            }
        }

        private void ExitClient()
        {
            Logger.Log("Exiting.");
            WindowManager.CloseGame();
#if !XNA
            Thread.Sleep(1000);
            Environment.Exit(0);
#endif
        }

        public void SwitchOn()
        {
            if (UserINISettings.Instance.StopMusicOnMenu)
                PlayMusic();

            if (!ClientConfiguration.Instance.ModMode && UserINISettings.Instance.CheckForUpdates)
            {
                // Re-check for updates

                if ((DateTime.Now - lastUpdateCheckTime) > TimeSpan.FromSeconds(UPDATE_RE_CHECK_THRESHOLD))
                    CheckForUpdates();
            }
        }

        public void SwitchOff()
        {
            if (UserINISettings.Instance.StopMusicOnMenu)
                MusicOff();
        }

        private void MusicOff()
        {
            try
            {
                if (isMediaPlayerAvailable &&
                    MediaPlayer.State == MediaState.Playing)
                {
                    isMusicFading = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Turning music off failed! Message: " + ex.Message);
            }
        }

        /// <summary>
        /// Checks if media player is available currently.
        /// It is not available on Windows Vista or other systems without the appropriate media player components.
        /// </summary>
        /// <returns>True if media player is available, false otherwise.</returns>
        private bool IsMediaPlayerAvailable()
        {
            if (MainClientConstants.OSId == OSVersion.WINVISTA)
                return false;

            try
            {
                MediaState state = MediaPlayer.State;
                return true;
            }
            catch (Exception e)
            {
                Logger.Log("Error encountered when checking media player availability. Error message: " + e.Message);
                return false;
            }
        }

        private void LaunchMapEditor()
        {
            OSVersion osVersion = ClientConfiguration.Instance.GetOperatingSystemVersion();
            Process mapEditorProcess = new Process();

           string strCmdText = string.Format("/c cd /d {0} && FinalAlert2SP.exe", ProgramConstants.GamePath+ "FinalAlert2SP");

            mapEditorProcess.StartInfo.FileName = "cmd.exe";
            mapEditorProcess.StartInfo.Arguments = strCmdText;
            mapEditorProcess.StartInfo.UseShellExecute = false;   //是否使用操作系统shell启动 
            mapEditorProcess.StartInfo.CreateNoWindow = true;   //是否在新窗口中启动该进程的值 (不显示程序窗口)
            mapEditorProcess.Start();
            mapEditorProcess.WaitForExit();  //等待程序执行完退出进程
            mapEditorProcess.Close();

     //       if (osVersion != OSVersion.UNIX)
      //      {
         //
      //      }
      //      else
       //     {
      //          mapEditorProcess.StartInfo.FileName = FA2Path;
                
        //        mapEditorProcess.StartInfo.UseShellExecute = false;
        //    }

        //    mapEditorProcess.Start();
        }

        public string GetSwitchName() => "主菜单";
    }
}
