using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    [DisallowMultipleComponent]
    public class SettingsView : MonoEventReceiver
    {

        [Header( "Audio" )]
        [SerializeField] AudioDataSchema closeAudio;
        [SerializeField] AudioDataSchema changeTabAudio;
        [SerializeField] AudioDataSchema confirmSettingsAudio;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _settingsRoot;
        Button _closeButton;
        string _closeText;
        Button _applyButton;
        VisualElement _bumperLeftIcon;
        VisualElement _bumperRightIcon;

        // Tab buttons
        Button _tabGraphics;
        Button _tabAudio;
        Button _tabControls;

        // Tab panes
        VisualElement _paneGraphics;
        VisualElement _paneAudio;
        VisualElement _paneControls;

        DropdownField _resolutionDropdown;
        DropdownField _refreshRateDropdown;
        Toggle _fullscreenToggle;

        DropdownField _gameFpsLimitDropdown;
        DropdownField _menuFpsLimitDropdown;

        Slider _masterVolumeSlider;
        Slider _musicVolumeSlider;
        Slider _sfxVolumeSlider;
        Slider _dialogueVolumeSlider;
        Slider _uiVolumeSlider;

        List<Vector2Int> _resolutions = new();
        List<RefreshRate> _refreshRates = new();

        static readonly List<string> GameFpsChoices = new() { "Unlimited", "30", "60", "90", "120", "144", "165", "240" };
        static readonly List<string> MenuFpsChoices = new() { "30", "60", "90", "120", "144" };

        public event Action CloseRequested;

        public InputActionAsset actions;
        Label bindingDisplayNameText;

        private InputAction _cancelAction;
        private InputAction _nextTabAction;
        private InputAction _previousTabAction;
        private static readonly string[] TabNames = { "Graphics", "Audio", "Controls" };
        private int _currentTabIndex = 0;
        private InputAction actionToRebind;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;


        void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        void Start()
        {
            actions = InputTypeTracker.Instance.actions;
        }

        void OnEnable()
        {
            _root = _doc != null ? _doc.rootVisualElement : null;
            if ( _root == null )
                return;

            _settingsRoot = _root.Q<VisualElement>( "SettingsRoot" ) ?? _root;

            _closeButton = _root.Q<Button>( "CloseButton" );
            _closeText = _closeButton?.text;
            _applyButton = _root.Q<Button>( "ApplyButton" );
            _bumperLeftIcon = _root.Q<VisualElement>( "BumperLeftIcon" );
            _bumperRightIcon = _root.Q<VisualElement>( "BumperRightIcon" );
            _tabGraphics = _root.Q<Button>( "TabGraphics" );
            _tabAudio = _root.Q<Button>( "TabAudio" );
            _tabControls = _root.Q<Button>( "TabControls" );

            _paneGraphics = _root.Q<VisualElement>( "PaneGraphics" );
            _paneAudio = _root.Q<VisualElement>( "PaneAudio" );
            _paneControls = _root.Q<VisualElement>( "PaneControls" );

            _resolutionDropdown = _root.Q<DropdownField>( "Resolution" );
            _refreshRateDropdown = _root.Q<DropdownField>( "RefreshRate" );
            _fullscreenToggle = _root.Q<Toggle>( "Fullscreen" );

            _gameFpsLimitDropdown = _root.Q<DropdownField>( "GameFpsLimit" );
            _menuFpsLimitDropdown = _root.Q<DropdownField>( "MenuFpsLimit" );

            _masterVolumeSlider = _root.Q<Slider>( "MasterVolume" );
            _musicVolumeSlider = _root.Q<Slider>( "MusicVolume" );
            _sfxVolumeSlider = _root.Q<Slider>( "SfxVolume" );
            _dialogueVolumeSlider = _root.Q<Slider>( "DialogueVolume" );
            _uiVolumeSlider = _root.Q<Slider>( "UiVolume" );

            _closeButton.clicked += OnCloseClicked;
            _applyButton.clicked += OnApplyClicked;

            _tabGraphics.clicked += () => SelectTab( "Graphics" );
            _tabAudio.clicked += () => SelectTab( "Audio" );
            _tabControls.clicked += () => SelectTab( "Controls" );

            _resolutionDropdown.RegisterValueChangedCallback( OnResolutionChanged );
            _refreshRateDropdown.RegisterValueChangedCallback( OnRefreshRateChanged );
            _fullscreenToggle.RegisterValueChangedCallback( OnFullscreenChanged );
            _gameFpsLimitDropdown.RegisterValueChangedCallback( OnGameFpsChanged );
            _menuFpsLimitDropdown.RegisterValueChangedCallback( OnMenuFpsChanged );

            _masterVolumeSlider.RegisterValueChangedCallback( OnMasterVolumeChanged );
            _musicVolumeSlider.RegisterValueChangedCallback( OnMusicVolumeChanged );
            _sfxVolumeSlider.RegisterValueChangedCallback( OnSfxVolumeChanged );
            _dialogueVolumeSlider.RegisterValueChangedCallback( OnDialogueVolumeChanged );
            _uiVolumeSlider.RegisterValueChangedCallback( OnUiVolumeChanged );

            _cancelAction = InputSystem.actions.FindAction( "UI/Cancel" );
            if ( _cancelAction != null ) _cancelAction.started += OnCancelPerformed;

            _nextTabAction = InputSystem.actions.FindAction( "UI/PageNext" );
            _previousTabAction = InputSystem.actions.FindAction( "UI/PagePrevious" );
            if ( _nextTabAction != null ) _nextTabAction.started += OnNextTab;
            if ( _previousTabAction != null ) _previousTabAction.started += OnPreviousTab;

            ConfigureCenteredLayout();
            InitializeDisplayOptions();
            LoadFromPrefsToUi();
            SelectTab( "Graphics", false );
            Show( false );
            ShowApplyButton( false );

            ApplyInputTypeLayout( InputTypeTracker.Instance.currentType );
            InputTypeTracker.Instance.onInputTypeChanged += ApplyInputTypeLayout;
        }

        void OnDisable()
        {
            if ( _closeButton != null )
                _closeButton.clicked -= OnCloseClicked;
            if ( _applyButton != null )
                _applyButton.clicked -= OnApplyClicked;
            if ( _resolutionDropdown != null )
                _resolutionDropdown.UnregisterValueChangedCallback( OnResolutionChanged );
            if ( _cancelAction != null )
                _cancelAction.started -= OnCancelPerformed;
            if ( _nextTabAction != null )
                _nextTabAction.started -= OnNextTab;
            if ( _previousTabAction != null )
                _previousTabAction.started -= OnPreviousTab;
            InputTypeTracker.Instance.onInputTypeChanged -= ApplyInputTypeLayout;
        }

        void ApplyInputTypeLayout( InputTypeTracker.InputType type )
        {
            if ( _closeButton == null ) return;
            bool isGamepad = type == InputTypeTracker.InputType.Gamepad;
            _closeButton.enableRichText = true;
            _closeButton.text = isGamepad
                ? ( _closeText + " " + InputBindIconData.GetRichTextInputIconString( _cancelAction ) )
                : _closeText;

            var bumperDisplay = isGamepad ? DisplayStyle.Flex : DisplayStyle.None;
            if ( _bumperLeftIcon != null ) _bumperLeftIcon.style.display = bumperDisplay;
            if ( _bumperRightIcon != null ) _bumperRightIcon.style.display = bumperDisplay;

            if ( isGamepad )
            {
                SetBumperIcon( _bumperLeftIcon, _previousTabAction );
                SetBumperIcon( _bumperRightIcon, _nextTabAction );
            }
        }

        void SetBumperIcon( VisualElement icon, InputAction action )
        {
            if ( icon == null || action == null ) return;
            var sprite = action.GetSprite();
            if ( sprite != null )
                icon.style.backgroundImage = new UnityEngine.UIElements.StyleBackground( sprite );
        }

        void SelectTab( string tabName, bool playAudio = true )
        {
            var allPanes = new[] { _paneGraphics, _paneAudio, _paneControls };
            var allButtons = new[] { _tabGraphics, _tabAudio, _tabControls };

            for ( int i = 0; i < allPanes.Length; i++ )
            {
                bool active = TabNames[i] == tabName;
                if ( active ) _currentTabIndex = i;
                if ( allPanes[i] != null )
                    allPanes[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                if ( allButtons[i] != null )
                {
                    if ( active )
                        allButtons[i].AddToClassList( "tab-button--active" );
                    else
                        allButtons[i].RemoveFromClassList( "tab-button--active" );
                }
            }

            if ( playAudio )
                Runtime.Audio.SfxManager.Instance.PlayUI( changeTabAudio );
        }

        public void Show( bool visible )
        {
            if ( _settingsRoot == null )
                return;

            _settingsRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsRoot.visible = visible;

            ConfigureCenteredLayout();
            if ( visible )
                _settingsRoot.Focus();

            ShowApplyButton( false );
        }

        void ConfigureCenteredLayout()
        {
            if ( _root != null )
            {
                _root.style.position = Position.Absolute;
                _root.style.left = 0;
                _root.style.right = 0;
                _root.style.top = 0;
                _root.style.bottom = 0;
            }
        }

        void InitializeDisplayOptions()
        {
            var uniqueResolutions = new HashSet<Vector2Int>();
            var validResolutions = Screen.resolutions
                .Where( x => x.width >= 1280 && x.height >= 720 )
                .OrderByDescending( x => x.width )
                .ThenByDescending( x => x.height )
                .ToList();

            foreach ( var res in validResolutions )
                uniqueResolutions.Add( new Vector2Int( res.width, res.height ) );

            _resolutions = uniqueResolutions
                .OrderByDescending( x => x.x )
                .ThenByDescending( x => x.y )
                .ToList();

            if ( _resolutions.Count == 0 )
                _resolutions.Add( new Vector2Int( Screen.currentResolution.width, Screen.currentResolution.height ) );

            if ( _resolutionDropdown != null )
                _resolutionDropdown.choices = _resolutions.Select( ResolutionToDisplayString ).ToList();
        }

        void LoadFromPrefsToUi()
        {
            var savedResolution = Runtime.Settings.Resolution;
            var resolutionValue = ResolutionToDisplayString( savedResolution );
            if ( !_resolutionDropdown.choices.Contains( resolutionValue ) )
            {
                savedResolution = _resolutions.First();
                resolutionValue = ResolutionToDisplayString( savedResolution );
            }

            _resolutionDropdown.value = resolutionValue;
            RefreshRefreshRateChoices( savedResolution );

            var savedRefreshRate = Runtime.Settings.RefreshRate;
            var refreshValue = RefreshRateToDisplayString( savedRefreshRate );
            _refreshRateDropdown.value = _refreshRateDropdown.choices.Contains( refreshValue )
                ? refreshValue
                : _refreshRateDropdown.choices.First();

            _fullscreenToggle.value = Runtime.Settings.Fullscreen;

            _gameFpsLimitDropdown.choices = GameFpsChoices;
            _gameFpsLimitDropdown.value = FpsLimitToDisplayString( Runtime.Settings.GameFpsLimit, isGame: true );

            _menuFpsLimitDropdown.choices = MenuFpsChoices;
            _menuFpsLimitDropdown.value = FpsLimitToDisplayString( Runtime.Settings.MenuFpsLimit, isGame: false );

            _masterVolumeSlider.value = Runtime.Settings.MasterVolume;
            _musicVolumeSlider.value = Runtime.Settings.MusicVolume;
            _sfxVolumeSlider.value = Runtime.Settings.SfxVolume;
            _dialogueVolumeSlider.value = Runtime.Settings.DialogueVolume;
            _uiVolumeSlider.value = Runtime.Settings.UIVolume;
        }

        void OnResolutionChanged( ChangeEvent<string> evt )
        {
            var resolution = StringToResolution( evt.newValue );
            RefreshRefreshRateChoices( resolution );
            if ( evt.newValue != ResolutionToDisplayString( Runtime.Settings.Resolution ) )
                ShowApplyButton( true );
        }

        void OnRefreshRateChanged( ChangeEvent<string> evt )
        {
            if ( evt.newValue != RefreshRateToDisplayString( Runtime.Settings.RefreshRate ) )
                ShowApplyButton( true );
        }

        void OnFullscreenChanged( ChangeEvent<bool> evt )
        {
            if ( evt.newValue != Runtime.Settings.Fullscreen )
                ShowApplyButton( true );
        }

        void OnGameFpsChanged( ChangeEvent<string> evt )
        {
            if ( evt.newValue != FpsLimitToDisplayString( Runtime.Settings.GameFpsLimit, isGame: true ) )
                ShowApplyButton( true );
        }

        void OnMenuFpsChanged( ChangeEvent<string> evt )
        {
            if ( evt.newValue != FpsLimitToDisplayString( Runtime.Settings.MenuFpsLimit, isGame: false ) )
                ShowApplyButton( true );
        }

        void OnMasterVolumeChanged( ChangeEvent<float> evt )
        {
            if ( !Mathf.Approximately( evt.newValue, Runtime.Settings.MasterVolume ) )
                ShowApplyButton( true );
        }

        void OnMusicVolumeChanged( ChangeEvent<float> evt )
        {
            if ( !Mathf.Approximately( evt.newValue, Runtime.Settings.MusicVolume ) )
                ShowApplyButton( true );
        }

        void OnSfxVolumeChanged( ChangeEvent<float> evt )
        {
            if ( !Mathf.Approximately( evt.newValue, Runtime.Settings.SfxVolume ) )
                ShowApplyButton( true );
        }

        void OnDialogueVolumeChanged( ChangeEvent<float> evt )
        {
            if ( !Mathf.Approximately( evt.newValue, Runtime.Settings.DialogueVolume ) )
                ShowApplyButton( true );
        }

        void OnUiVolumeChanged( ChangeEvent<float> evt )
        {
            if ( !Mathf.Approximately( evt.newValue, Runtime.Settings.UIVolume ) )
                ShowApplyButton( true );
        }

        void ShowApplyButton( bool visible )
        {
            _applyButton.SetEnabled( visible );
            _applyButton.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        void RefreshRefreshRateChoices( Vector2Int resolution )
        {
            _refreshRates = Screen.resolutions
                .Where( x => x.width == resolution.x && x.height == resolution.y )
                .Select( x => x.refreshRateRatio )
                .Distinct()
                .OrderByDescending( x => x.numerator / ( float )Mathf.Max( 1, ( int )x.denominator ) )
                .ToList();

            if ( _refreshRates.Count == 0 )
                _refreshRates.Add( Screen.currentResolution.refreshRateRatio );

            var oldValue = _refreshRateDropdown.value;
            _refreshRateDropdown.choices = _refreshRates.Select( RefreshRateToDisplayString ).ToList();
            _refreshRateDropdown.value = _refreshRateDropdown.choices.Contains( oldValue ) ? oldValue : _refreshRateDropdown.choices.First();
        }

        void OnApplyClicked()
        {
            var selectedResolution = StringToResolution( _resolutionDropdown.value );
            var selectedRefreshRate = StringToRefreshRate( _refreshRateDropdown.value );
            var fullscreenMode = _fullscreenToggle.value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

            Runtime.Settings.Resolution = selectedResolution;
            Runtime.Settings.RefreshRate = selectedRefreshRate;
            Runtime.Settings.Fullscreen = _fullscreenToggle.value;
            Runtime.Settings.MasterVolume = _masterVolumeSlider.value;
            Runtime.Settings.MusicVolume = _musicVolumeSlider.value;
            Runtime.Settings.SfxVolume = _sfxVolumeSlider.value;
            Runtime.Settings.DialogueVolume = _dialogueVolumeSlider.value;
            Runtime.Settings.UIVolume = _uiVolumeSlider.value;
            var gameFps = DisplayStringToFpsLimit( _gameFpsLimitDropdown.value, isGame: true );
            var menuFps = DisplayStringToFpsLimit( _menuFpsLimitDropdown.value, isGame: false );
            Runtime.Settings.GameFpsLimit = gameFps;
            Runtime.Settings.MenuFpsLimit = menuFps;
            Runtime.Settings.Save();

            ShowApplyButton( false );
            Screen.SetResolution( selectedResolution.x, selectedResolution.y, fullscreenMode, selectedRefreshRate );

            var display = Screen.mainWindowDisplayInfo;
            display.name = "Noctus Quarry";
            Screen.MoveMainWindowTo( display, Screen.mainWindowPosition );

            // Menu FPS applied immediately; game FPS is applied when the game scene loads.
            Application.targetFrameRate = menuFps;
            QualitySettings.vSyncCount = 0;

            Runtime.Audio.SfxManager.Instance.PlayUI( confirmSettingsAudio );

            Runtime.Events.SettingsModified.Trigger( new() );
        }

        /// <summary>Returns the display string for a stored fps limit value (-1 = Unlimited for game fps).</summary>
        static string FpsLimitToDisplayString( int fps, bool isGame )
        {
            if ( isGame && fps <= 0 )
                return "Unlimited";
            var choices = isGame ? GameFpsChoices : MenuFpsChoices;
            var str = fps.ToString();
            return choices.Contains( str ) ? str : choices[0];
        }

        /// <summary>Parses a display string back to an fps int (-1 for Unlimited).</summary>
        static int DisplayStringToFpsLimit( string value, bool isGame )
        {
            if ( isGame && value == "Unlimited" )
                return -1;
            return int.TryParse( value, out var fps ) ? fps : ( isGame ? Runtime.Settings.DefaultGameFpsLimit : Runtime.Settings.DefaultMenuFpsLimit );
        }

        public static string ResolutionToDisplayString( Vector2Int res ) => $"{res.x}x{res.y}";

        public static string ResolutionToString( Vector2Int res ) => $"{res.x}x{res.y}";

        public static string RefreshRateToDisplayString( RefreshRate rate )
        {
            var hz = rate.numerator / ( float )Mathf.Max( 1, ( int )rate.denominator );
            return $"{Mathf.RoundToInt( hz )}hz";
        }

        public static string RefreshRateToString( RefreshRate rate ) => $"{rate.numerator}/{rate.denominator}";

        public static Vector2Int StringToResolution( string value ) =>
            Runtime.Settings.ParseResolution( value );

        public static RefreshRate StringToRefreshRate( string value ) =>
            Runtime.Settings.ParseRefreshRate( value );

        void OnNextTab( InputAction.CallbackContext _ )
        {
            if ( _settingsRoot == null || _settingsRoot.style.display == DisplayStyle.None ) return;
            SelectTab( TabNames[( _currentTabIndex + 1 ) % TabNames.Length] );
        }

        void OnPreviousTab( InputAction.CallbackContext _ )
        {
            if ( _settingsRoot == null || _settingsRoot.style.display == DisplayStyle.None ) return;
            SelectTab( TabNames[( _currentTabIndex - 1 + TabNames.Length ) % TabNames.Length] );
        }

        void OnCloseClicked()
        {
            CloseRequested?.Invoke();
            Show( false );
            Runtime.Audio.SfxManager.Instance.PlayUI( closeAudio );
        }

        void OnCancelPerformed( InputAction.CallbackContext ctx )
        {
            if ( _settingsRoot != null && _settingsRoot.style.display != DisplayStyle.None )
            {
                CloseRequested?.Invoke();
                Show( false );
                Runtime.Audio.SfxManager.Instance.PlayUI( closeAudio );
            }
        }

        public void StartRebinding()
        {
            // Disable the action temporarily to prevent accidental triggers during rebind
            actionToRebind.Disable();

            // Dispose of any previous operation
            rebindingOperation?.Dispose();

            // Start the interactive rebinding operation
            rebindingOperation = actionToRebind.PerformInteractiveRebinding()
                .WithControlsExcluding( "<Mouse>/position" ) // Exclude mouse position to avoid accidental binding
                .WithControlsExcluding( "<Mouse>/delta" ) // Exclude mouse delta
                .OnMatchWaitForAnother( 0.1f ) // Wait briefly to avoid accidental double-input
                .OnComplete( operation => RebindComplete() )
                .OnCancel( operation => RebindCancel() )
                .Start();

            bindingDisplayNameText.text = "Waiting for input...";
        }

        private void RebindComplete()
        {
            actionToRebind.Enable();
            rebindingOperation.Dispose();
            rebindingOperation = null;
            UpdateBindingText();
            SaveBindings();
        }

        private void RebindCancel()
        {
            actionToRebind.Enable();
            rebindingOperation.Dispose();
            rebindingOperation = null;
            UpdateBindingText();
        }

        private void UpdateBindingText()
        {
            // Display the current binding path
            int bindingIndex = actionToRebind.GetBindingIndex();
            //bindingDisplayNameText.text = InputBinding.MaskingPath( actionToRebind.bindings[bindingIndex].effectivePath );
        }

        void SaveBindings()
        {
            string overridesJson = actions.SaveBindingOverridesAsJson();
            Runtime.Settings.Bindings = overridesJson;
            Runtime.Settings.Save();
        }
    }
}
