using System;
using Runtime;
using Runtime.Audio;
using Runtime.Game;
using Schema;
using Schema.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace UI
{
    [DisallowMultipleComponent]
    public class MainMenuView : MonoBehaviour
    {
        [Header( "References" )]
        [SerializeField] NewGameView newGameView;
        [SerializeField] LoadGameView loadGameView;
        [SerializeField] HighScoresView highScoresView;
        [SerializeField] StatisticsView statisticsView;
        [SerializeField] SettingsView settingsView;
        [SerializeField] AudioDataSchema buttonPressOpenAudio;
        [SerializeField] AudioDataSchema buttonPressCloseAudio;

        [Header( "Social Links" )]
        [SerializeField] string discordUrl;
        [SerializeField] string instagramUrl;
        [SerializeField] string steamUrl;
        [SerializeField] string versionLabelText = "Noctus Quarry Pre-Release\nVersion {0}";

        UIDocument uiDocument;
        VisualElement root;

        Button btnNew;
        Button btnLoad;
        Button btnHighScores;
        Button btnStatistics;
        Button btnSettings;
        Button btnExit;

        Button btnDiscord;
        Button btnInstagram;
        Button btnSteam;

        Label versionLabel;


        Button[] navButtons;
        InputAction nextTabAction;
        InputAction previousTabAction;
        InputAction navigateAction;

        bool _navSuspended { get; set; }
        bool navSuspended
        {
            get => _navSuspended;
            set
            {
                _navSuspended = value;
                if ( value )
                {
                    foreach ( var btn in navButtons )
                        btn.focusable = false;
                }
                else
                {
                    foreach ( var btn in navButtons )
                        btn.focusable = true;
                }
            }
        }

        void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if ( root == null )
                return;

            btnNew = root.Q<Button>( "NewGame" );
            btnLoad = root.Q<Button>( "LoadGame" );
            btnHighScores = root.Q<Button>( "HighScores" );
            btnStatistics = root.Q<Button>( "Statistics" );
            btnSettings = root.Q<Button>( "Settings" );
            btnExit = root.Q<Button>( "Exit" );

            btnDiscord = root.Q<Button>( "Discord" );
            btnInstagram = root.Q<Button>( "Instagram" );
            btnSteam = root.Q<Button>( "Steam" );

            btnNew.clicked += OnNewGame;
            btnLoad.clicked += OnLoadGame;
            btnHighScores.clicked += OnHighScores;
            btnStatistics.clicked += OnStatistics;
            btnSettings.clicked += OnSettings;
            btnExit.clicked += OnExit;

            btnDiscord.clicked += OpenDiscord;
            btnInstagram.clicked += OpenInstagram;
            btnSteam.clicked += OpenSteam;

            loadGameView.CloseRequested += OnLoadClosed;
            loadGameView.LoadRequested += OnLoadRequested;
            loadGameView.Show( false );

            newGameView.CloseRequested += OnNewGameClosed;
            newGameView.Show( false );

            settingsView.CloseRequested += OnSettingsClosed;
            settingsView.Show( false );

            highScoresView.CloseRequested += OnHighScoresClosed;
            highScoresView.Show( false );

            if ( statisticsView != null )
            {
                statisticsView.CloseRequested += OnStatisticsClosed;
                statisticsView.Show( false );
            }

            settingsView.gameObject.SetActive( false );
            highScoresView.gameObject.SetActive( false );
            if ( statisticsView != null )
                statisticsView.gameObject.SetActive( false );
            loadGameView.gameObject.SetActive( false );
            newGameView.gameObject.SetActive( false );

            navButtons = new Button[] { btnNew, btnLoad, btnHighScores, btnStatistics, btnSettings, btnExit };
            foreach ( var btn in navButtons )
                btn.focusable = true;

            navigateAction = InputSystem.actions.FindAction( "UI/Navigate" );
            navigateAction.started += OnNavigate;

            nextTabAction = InputSystem.actions.FindAction( "UI/PageNext" );
            previousTabAction = InputSystem.actions.FindAction( "UI/PagePrevious" );
            nextTabAction.performed += OnPageNext;
            previousTabAction.performed += OnPagePrevious;

            InputTypeTracker.Instance.onInputTypeChanged += OnInputTypeChanged;
            OnInputTypeChanged( InputTypeTracker.Instance.currentType );

            versionLabel = root.Q<Label>( "VersionLabel" );
        }

        void OnPageNext( InputAction.CallbackContext _ ) { StepFocus( forward: true ); }
        void OnPagePrevious( InputAction.CallbackContext _ ) { StepFocus( forward: false ); }

        void StepFocus( bool forward )
        {
            if ( navSuspended )
                return;

            if ( navButtons == null || navButtons.Length == 0 )
                return;

            int current = -1;
            for ( int i = 0; i < navButtons.Length; i++ )
            {
                if ( navButtons[i].focusController?.focusedElement == navButtons[i] )
                {
                    current = i;
                    break;
                }
            }

            if ( current == -1 )
                current = forward ? navButtons.Length - 1 : 0;

            int next = forward
                ? ( current + 1 ) % navButtons.Length
                : ( current - 1 + navButtons.Length ) % navButtons.Length;

            int attempts = navButtons.Length;
            while ( !navButtons[next].enabledSelf && attempts-- > 0 )
                next = forward
                    ? ( next + 1 ) % navButtons.Length
                    : ( next - 1 + navButtons.Length ) % navButtons.Length;

            navButtons[next].Focus();
        }

        void OnNavigate( InputAction.CallbackContext ctx )
        {
            var value = ctx.ReadValue<Vector2>();
            if ( value.y > 0.5f )
                StepFocus( forward: false );
            else if ( value.y < -0.5f )
                StepFocus( forward: true );
        }

        private void OnInputTypeChanged( InputTypeTracker.InputType type )
        {
            if ( type == InputTypeTracker.InputType.Gamepad )
            {
                foreach ( var btn in navButtons )
                    btn.focusable = true;
                if ( !navSuspended )
                    btnNew.Focus();
                GameSceneManager.Instance.OverrideCursorVisibility( false );
            }
            else
            {
                // Remove focus
                btnNew.Blur();
                btnLoad.Blur();
                btnHighScores.Blur();
                btnStatistics.Blur();
                btnSettings.Blur();
                btnExit.Blur();
                foreach ( var btn in navButtons )
                    btn.focusable = false;
                GameSceneManager.Instance.ResetCursorVisibility();
            }
        }

        void Start()
        {
            Init();
        }

        void Init()
        {
            var saves = Save.SaveManager.Instance.GetFromDirectory<Save.SaveMetaData>( GlobalConstantsHandler.Constants.RootSavePath );
            var hasSaves = saves.Count > 0;
            if ( btnLoad != null )
            {
                // Disable interactions and hover when there are no saves%
                btnLoad.SetEnabled( hasSaves );
                btnLoad.pickingMode = hasSaves ? PickingMode.Position : PickingMode.Ignore;
            }

            Screen.SetResolution( Settings.Resolution.x, Settings.Resolution.y, Settings.Fullscreen );

            var display = Screen.mainWindowDisplayInfo;
            display.name = "Noctus Quarry";
            Screen.MoveMainWindowTo( display, Screen.mainWindowPosition );

            // Menu FPS applied immediately; game FPS is applied when the game scene loads.
            Application.targetFrameRate = Settings.MenuFpsLimit;
            QualitySettings.vSyncCount = 0;

            if ( versionLabel != null )
                versionLabel.text = string.Format( versionLabelText, GlobalRuntimeConstants.VersionNumber );
        }

        void OnDisable()
        {
            btnNew.clicked -= OnNewGame;
            btnLoad.clicked -= OnLoadGame;
            btnHighScores.clicked -= OnHighScores;
            btnStatistics.clicked -= OnStatistics;
            btnSettings.clicked -= OnSettings;
            btnExit.clicked -= OnExit;

            btnDiscord.clicked -= OpenDiscord;
            btnInstagram.clicked -= OpenInstagram;
            btnSteam.clicked -= OpenSteam;

            loadGameView.CloseRequested -= OnLoadClosed;
            loadGameView.LoadRequested -= OnLoadRequested;
            newGameView.CloseRequested -= OnNewGameClosed;
            settingsView.CloseRequested -= OnSettingsClosed;
            highScoresView.CloseRequested -= OnHighScoresClosed;
            if ( statisticsView != null )
                statisticsView.CloseRequested -= OnStatisticsClosed;

            navigateAction.started -= OnNavigate;
            nextTabAction.performed -= OnPageNext;
            previousTabAction.performed -= OnPagePrevious;
            InputTypeTracker.Instance.onInputTypeChanged -= OnInputTypeChanged;
        }

        void OpenDiscord()
        {
            OpenUrl( discordUrl );
        }

        void OpenInstagram()
        {
            OpenUrl( instagramUrl );
        }

        void OpenSteam()
        {
            OpenUrl( steamUrl );
        }

        void OnNewGame()
        {
            navSuspended = true;
            SfxManager.Instance.PlayUI( buttonPressOpenAudio );
            newGameView.gameObject.SetActive( true );
            newGameView.Show( true );
        }

        void OnNewGameClosed()
        {
            navSuspended = false;
            SfxManager.Instance.PlayUI( buttonPressCloseAudio );
            newGameView.gameObject.SetActive( false );
            newGameView.Show( false );
            btnNew.Focus();
        }

        void OnLoadGame()
        {
            navSuspended = true;
            SfxManager.Instance.PlayUI( buttonPressOpenAudio );
            loadGameView.gameObject.SetActive( true );
            loadGameView.Show( true );
        }

        void OnSettings()
        {
            navSuspended = true;
            SfxManager.Instance.PlayUI( buttonPressOpenAudio );
            settingsView.gameObject.SetActive( true );
            settingsView.Show( true );
        }

        void OnHighScores()
        {
            navSuspended = true;
            SfxManager.Instance.PlayUI( buttonPressOpenAudio );
            highScoresView.gameObject.SetActive( true );
            highScoresView.Show( true );
        }

        void OnStatistics()
        {
            if ( statisticsView == null )
                return;

            navSuspended = true;
            SfxManager.Instance.PlayUI( buttonPressOpenAudio );
            statisticsView.gameObject.SetActive( true );
            statisticsView.Show( true );
        }

        void OnExit()
        {
            Application.Quit();
        }

        void OnLoadClosed()
        {
            navSuspended = false;
            SfxManager.Instance.PlayUI( buttonPressCloseAudio );
            loadGameView.gameObject.SetActive( false );
            loadGameView.Show( false );
            Init();
            btnLoad.Focus();
        }

        private void OnSettingsClosed()
        {
            navSuspended = false;
            SfxManager.Instance.PlayUI( buttonPressCloseAudio );
            settingsView.gameObject.SetActive( false );
            settingsView.Show( false );
            btnSettings.Focus();
        }

        void OnHighScoresClosed()
        {
            navSuspended = false;
            SfxManager.Instance.PlayUI( buttonPressCloseAudio );
            highScoresView.gameObject.SetActive( false );
            highScoresView.Show( false );
            btnHighScores.Focus();
        }

        void OnStatisticsClosed()
        {
            navSuspended = false;
            SfxManager.Instance.PlayUI( buttonPressCloseAudio );
            if ( statisticsView != null )
            {
                statisticsView.gameObject.SetActive( false );
                statisticsView.Show( false );
            }
            btnStatistics?.Focus();
        }

        void OnLoadRequested( Save.SaveMetaData save )
        {
            GlobalRuntimeConstants.GameName = save.gameName;
            GlobalRuntimeConstants.SaveIdx = save.saveIdx;
            GlobalRuntimeConstants.Seed = save.seed;
            GlobalRuntimeConstants.DepthLayer = save.depthLayer;

            if ( save.inOutpost )
                GameSceneManager.Instance.LoadOutpostScene();
            else
                GameSceneManager.Instance.LoadGameScene();
        }

        static void OpenUrl( string url )
        {
            if ( string.IsNullOrWhiteSpace( url ) )
                return;
            Application.OpenURL( url );
        }

    }
}