using System;
using Runtime.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    [DisallowMultipleComponent]
    public class InGameMenuView : MonoEventReceiver
    {
        [Header( "References" )]
        [SerializeField] SettingsView settingsView;
        [SerializeField] KnowledgeCoreView knowledgeCoreView;

        [Header( "Button Names in UI" )]
        [SerializeField] string resumeButtonName = "Resume";
        [SerializeField] string knowledgeCoreButtonName = "KnowledgeCore";
        [SerializeField] string settingsButtonName = "Settings";
        [SerializeField] string exitToMenuButtonName = "ExitToMenu";
        [SerializeField] string exitToDesktopButtonName = "ExitToDesktop";
        UIDocument uiDocument;
        VisualElement root;

        Button btnResume;
        Button btnKnowledgeCore;
        Button btnSettings;
        Button btnExitToMenu;
        Button btnExitToDesktop;

        Button[] navButtons;
        InputAction _pauseAction;
        InputAction navigateAction;
        InputAction nextTabAction;
        InputAction previousTabAction;

        bool _navSuspended;
        bool navSuspended
        {
            get => _navSuspended;
            set
            {
                _navSuspended = value;
                if ( navButtons == null ) return;
                foreach ( var btn in navButtons )
                    btn.focusable = !value;
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

            btnResume = root.Q<Button>( resumeButtonName );
            btnKnowledgeCore = root.Q<Button>( knowledgeCoreButtonName );
            btnSettings = root.Q<Button>( settingsButtonName );
            btnExitToMenu = root.Q<Button>( exitToMenuButtonName );
            btnExitToDesktop = root.Q<Button>( exitToDesktopButtonName );

            if ( btnResume != null ) btnResume.clicked += OnResume;
            if ( btnKnowledgeCore != null ) btnKnowledgeCore.clicked += OnKnowledgeCore;
            if ( btnSettings != null ) btnSettings.clicked += OnSettings;
            if ( btnExitToMenu != null ) btnExitToMenu.clicked += OnExitToMenu;
            if ( btnExitToDesktop != null ) btnExitToDesktop.clicked += OnExitToDesktop;

            Show( false ); // start hidden by default

            // Listen for pause/resume events to control menu visibility
            Runtime.Events.GamePaused.Subscribe( this, OnGamePaused );
            Runtime.Events.GameResumed.Subscribe( this, OnGameResumed );

            knowledgeCoreView.CloseRequested += OnKnowledgeCoreClosed;
            settingsView.CloseRequested += OnSettingsClosed;
            settingsView.gameObject.SetActive( false );
            knowledgeCoreView.gameObject.SetActive( false );

            _pauseAction = InputSystem.actions.FindAction( "Gameplay/Pause" );

            navButtons = new Button[] { btnResume, btnKnowledgeCore, btnSettings, btnExitToMenu, btnExitToDesktop };
            foreach ( var btn in navButtons )
                btn.focusable = true;

            navigateAction = InputSystem.actions.FindAction( "UI/Navigate" );
            navigateAction.started += OnNavigate;

            nextTabAction = InputSystem.actions.FindAction( "UI/PageNext" );
            previousTabAction = InputSystem.actions.FindAction( "UI/PagePrevious" );
            nextTabAction.performed += OnPageNext;
            previousTabAction.performed += OnPagePrevious;

            InputTypeTracker.Instance.onInputTypeChanged += OnInputTypeChanged;
        }

        void OnDisable()
        {
            if ( btnResume != null ) btnResume.clicked -= OnResume;
            if ( btnKnowledgeCore != null ) btnKnowledgeCore.clicked -= OnKnowledgeCore;
            if ( btnSettings != null ) btnSettings.clicked -= OnSettings;
            if ( btnExitToMenu != null ) btnExitToMenu.clicked -= OnExitToMenu;
            if ( btnExitToDesktop != null ) btnExitToDesktop.clicked -= OnExitToDesktop;

            Runtime.Events.GamePaused.Unsubscribe( this );
            Runtime.Events.GameResumed.Unsubscribe( this );

            navigateAction.started -= OnNavigate;
            nextTabAction.performed -= OnPageNext;
            previousTabAction.performed -= OnPagePrevious;
            InputTypeTracker.Instance.onInputTypeChanged -= OnInputTypeChanged;

            if ( knowledgeCoreView != null )
            {
                knowledgeCoreView.CloseRequested -= OnKnowledgeCoreClosed;
            }

            if ( settingsView != null )
            {
                settingsView.CloseRequested -= OnSettingsClosed;
            }
        }

        public void Show( bool visible )
        {
            root.visible = visible;
            _pauseAction?.Enable();

            if ( visible )
                OnInputTypeChanged( InputTypeTracker.Instance.currentType );
            else
                GameSceneManager.Instance.ResetCursorVisibility();
        }

        public void Toggle()
        {
            Show( !root.visible );
        }

        void OnResume()
        {
            PauseManager.Instance.ReleasePlayerPause();
        }

        void OnKnowledgeCore()
        {
            if ( knowledgeCoreView != null )
            {
                navSuspended = true;
                Show( false );
                OnInputTypeChanged( InputTypeTracker.Instance.currentType );
                _pauseAction.Disable();
                knowledgeCoreView.gameObject.SetActive( true );
                knowledgeCoreView.Show( true );
            }
        }

        void OnSettings()
        {
            if ( settingsView != null )
            {
                navSuspended = true;
                Show( false );
                OnInputTypeChanged( InputTypeTracker.Instance.currentType );
                _pauseAction.Disable();
                settingsView.gameObject.SetActive( true );
                settingsView.Show( true );
            }
        }

        void OnExitToMenu()
        {
            Time.timeScale = 1.0f;
            // We already save we changing scenes
            Runtime.Game.GameSceneManager.Instance.LoadMainMenuScene();
        }

        void OnExitToDesktop()
        {
            Runtime.Events.RequestGlobalSave.Trigger( new Runtime.Events.RequestGlobalSave() );
            Application.Quit();
        }

        void OnKnowledgeCoreClosed()
        {
            // Return to pause menu when Knowledge Core closes
            navSuspended = false;
            knowledgeCoreView.gameObject.SetActive( false );
            Show( true );
            btnKnowledgeCore.Focus();
        }

        void OnSettingsClosed()
        {
            navSuspended = false;
            settingsView.gameObject.SetActive( false );
            Show( true );
            btnSettings.Focus();
        }

        void OnGamePaused( Runtime.Events.GamePaused e )
        {
            Show( e.showPauseUi );
        }

        void OnGameResumed( Runtime.Events.GameResumed e )
        {
            Show( false );
        }

        void FocusFirstEnabled()
        {
            if ( navButtons == null ) return;
            foreach ( var btn in navButtons )
            {
                if ( btn.enabledSelf )
                {
                    btn.Focus();
                    return;
                }
            }
        }

        void StepFocus( bool forward )
        {
            if ( navSuspended ) return;
            if ( navButtons == null || navButtons.Length == 0 ) return;

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

        void OnPageNext( InputAction.CallbackContext _ ) { StepFocus( forward: true ); }
        void OnPagePrevious( InputAction.CallbackContext _ ) { StepFocus( forward: false ); }

        void OnNavigate( InputAction.CallbackContext ctx )
        {
            var value = ctx.ReadValue<Vector2>();
            if ( value.y > 0.5f )
                StepFocus( forward: false );
            else if ( value.y < -0.5f )
                StepFocus( forward: true );
        }

        void OnInputTypeChanged( InputTypeTracker.InputType type )
        {
            if ( type == InputTypeTracker.InputType.Gamepad )
            {
                if ( navButtons != null )
                    foreach ( var btn in navButtons )
                        btn.focusable = true;
                if ( root.visible && !navSuspended )
                    FocusFirstEnabled();
                GameSceneManager.Instance.OverrideCursorVisibility( false );
            }
            else
            {
                if ( navButtons != null )
                    foreach ( var btn in navButtons )
                    {
                        btn.Blur();
                        btn.focusable = false;
                    }
                GameSceneManager.Instance.OverrideCursorVisibility( true );
            }
        }
    }
}
