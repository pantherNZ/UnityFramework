using System;
using Runtime.Audio;
using Runtime.Game;
using Schema;
using Schema.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    [DisallowMultipleComponent]
    public class NewGameView : MonoBehaviour
    {
        [Header( "Audio" )]
        [SerializeField] AudioDataSchema buttonStartAudio;
        [SerializeField] AudioDataSchema buttonCloseAudio;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _overlay;
        VisualElement _veil;
        VisualElement _panel;

        TextField _gameNameField;
        TextField _seedField;
        Toggle _hardcoreToggle;
        Button _startButton;
        Button _closeButton;
        string _closeText;
        Button _gameNameEditBtn;
        Button _seedEditBtn;
        VisualElement _gameNameEditRow;
        VisualElement _seedEditRow;

        int _pendingSaveIdx;
        InputAction _cancelAction;
        InputAction _navigateAction;
        bool _lockNewGameFocus;

        Button[] _navButtons;
        VirtualKeyboardController _keyboard = new();

        public event Action CloseRequested;

        void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            _root = _doc != null ? _doc.rootVisualElement : null;
            if ( _root == null )
                return;

            _overlay = _root.Q<VisualElement>( "Overlay" );
            _veil = _root.Q<VisualElement>( "Veil" );
            _panel = _root.Q<VisualElement>( "Panel" );

            _gameNameField = _root.Q<TextField>( "GameName" );
            _seedField = _root.Q<TextField>( "Seed" );
            _hardcoreToggle = _root.Q<Toggle>( "Hardcore" );
            if ( _hardcoreToggle != null )
                TooltipController.Bind( _hardcoreToggle, () => "Hardcore mode is one life and the game is much harder", topBottom: true );
            _startButton = _root.Q<Button>( "StartButton" );
            _closeButton = _root.Q<Button>( "CloseButton" );
            _closeText = _closeButton.text;
            _gameNameEditBtn = _root.Q<Button>( "GameNameEditBtn" );
            _seedEditBtn = _root.Q<Button>( "SeedEditBtn" );
            _gameNameEditRow = _root.Q<VisualElement>( "GameNameEditRow" );
            _seedEditRow = _root.Q<VisualElement>( "SeedEditRow" );

            if ( _startButton != null ) _startButton.clicked += OnStartClicked;
            if ( _closeButton != null ) _closeButton.clicked += OnCloseClicked;
            if ( _gameNameEditBtn != null ) _gameNameEditBtn.clicked += OnGameNameEditClicked;
            if ( _seedEditBtn != null ) _seedEditBtn.clicked += OnSeedEditClicked;

            _navButtons = new Button[] { _closeButton, _gameNameEditBtn, _seedEditBtn, _startButton };

            _cancelAction = InputSystem.actions.FindAction( "UI/Cancel" );
            if ( _cancelAction != null ) _cancelAction.started += OnCancelPerformed;

            _navigateAction = InputSystem.actions.FindAction( "UI/Navigate" );
            if ( _navigateAction != null ) _navigateAction.started += OnNavigate;

            // Swallow all NavigationMoveEvents at the root level so focus can never
            // escape this panel into the parent screen's buttons while we are open.
            _root.RegisterCallback<NavigationMoveEvent>( OnRootNavigationMove, TrickleDown.TrickleDown );

            ConfigureCenteredLayout();
            ApplyInputTypeLayout( InputTypeTracker.Instance.currentType );
            InputTypeTracker.Instance.onInputTypeChanged += OnInputTypeChanged;

            _gameNameField.RegisterValueChangedCallback( TextChangedEventGameName );
            _seedField.RegisterValueChangedCallback( TextChangedEventSeed );
        }

        void TextChangedEventGameName( ChangeEvent<string> evt )
        {
            TextChangedEvent( evt, _gameNameField );
        }

        void TextChangedEventSeed( ChangeEvent<string> evt )
        {
            TextChangedEvent( evt, _seedField );
        }

        void TextChangedEvent( ChangeEvent<string> evt, TextField field )
        {
            if ( string.IsNullOrEmpty( evt.newValue ) )
                return;

            char lastChar = evt.newValue[evt.newValue.Length - 1];
            if ( !char.IsLetterOrDigit( lastChar ) )
                field.SetValueWithoutNotify( evt.previousValue );
        }

        void OnDisable()
        {
            if ( _startButton != null ) _startButton.clicked -= OnStartClicked;
            if ( _closeButton != null ) _closeButton.clicked -= OnCloseClicked;
            if ( _gameNameEditBtn != null ) _gameNameEditBtn.clicked -= OnGameNameEditClicked;
            if ( _seedEditBtn != null ) _seedEditBtn.clicked -= OnSeedEditClicked;
            if ( _cancelAction != null ) _cancelAction.started -= OnCancelPerformed;
            if ( _navigateAction != null ) _navigateAction.started -= OnNavigate;
            _root?.UnregisterCallback<NavigationMoveEvent>( OnRootNavigationMove );
            InputTypeTracker.Instance.onInputTypeChanged -= OnInputTypeChanged;
            SetNewGameFocusLock( false );
            _keyboard.Close();
        }

        public void Show( bool visible )
        {
            if ( _root == null )
                return;

            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if ( !visible )
            {
                if ( _keyboard.IsOpen )
                    _keyboard.Close();
                SetNewGameFocusLock( false );
                return;
            }

            ConfigureCenteredLayout();
            PopulateDefaults();
            _hardcoreToggle.value = false;
            RefreshEditButtonLabels();
            if ( InputTypeTracker.Instance.currentType == InputTypeTracker.InputType.Gamepad )
            {
                _root.Focus();
                _closeButton?.Focus();
            }
            else
            {
                _root.Focus();
                if ( _gameNameField != null )
                    _gameNameField.Focus();
            }
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

            if ( _overlay != null )
            {
                _overlay.style.position = Position.Absolute;
                _overlay.style.left = 0;
                _overlay.style.right = 0;
                _overlay.style.top = 0;
                _overlay.style.bottom = 0;
                _overlay.style.justifyContent = Justify.Center;
                _overlay.style.alignItems = Align.Center;
                _overlay.style.display = DisplayStyle.Flex;
            }

            if ( _veil != null )
            {
                _veil.style.position = Position.Absolute;
                _veil.style.left = 0;
                _veil.style.right = 0;
                _veil.style.top = 0;
                _veil.style.bottom = 0;
            }

            if ( _panel != null )
            {
                _panel.style.position = Position.Relative;
            }
        }

        void PopulateDefaults()
        {
            var saves = Save.SaveManager.Instance.GetFromDirectory<Save.SaveMetaData>( GlobalConstantsHandler.Constants.RootSavePath );
            int? maxIdx = null;
            foreach ( var save in saves )
                maxIdx = Math.Max( maxIdx ?? 0, save.saveIdx );

            _pendingSaveIdx = maxIdx.HasValue
                ? maxIdx.Value + Utility.DefaultRng.Range( 7, 39 )
                : 542;

            if ( _gameNameField != null )
                _gameNameField.value = $"Scout{_pendingSaveIdx}";

            if ( _seedField != null )
                _seedField.value = String.IsNullOrEmpty( Runtime.Settings.Seed ) ? UnityEngine.Random.Range( 0, 99999999 ).ToString() : Runtime.Settings.Seed;
        }

        // Prevent any NavigationMoveEvent from propagating outside this panel's root.
        // This stops the main-menu buttons from receiving gamepad focus while the panel is open.
        // When the virtual keyboard is open it handles its own navigation, so we let the event through.
        void OnRootNavigationMove( NavigationMoveEvent evt )
        {
            if ( _keyboard.IsOpen || InputTypeTracker.Instance.currentType != InputTypeTracker.InputType.Gamepad )
                return;
            evt.StopPropagation();
        }

        void OnNavigate( InputAction.CallbackContext ctx )
        {
            if ( _navButtons == null )
                return;
            if ( _root == null || _root.style.display == DisplayStyle.None )
                return;
            if ( _keyboard.IsOpen || InputTypeTracker.Instance.currentType != InputTypeTracker.InputType.Gamepad )
                return;

            var value = ctx.ReadValue<Vector2>();
            if ( Mathf.Abs( value.y ) < 0.5f )
                return;
            bool forward = value.y < -0.5f;

            int current = -1;
            for ( int i = 0; i < _navButtons.Length; i++ )
            {
                if ( _navButtons[i]?.focusController?.focusedElement == _navButtons[i] )
                {
                    current = i;
                    break;
                }
            }

            // If no nav button currently has focus (e.g. focus escaped to another panel),
            // snap back to a sensible default instead of letting the other panel navigate.
            if ( current == -1 )
            {
                _closeButton?.Focus();
                return;
            }

            int next = forward
                ? ( current + 1 ) % _navButtons.Length
                : ( current - 1 + _navButtons.Length ) % _navButtons.Length;
            _navButtons[next]?.Focus();
        }

        void OnInputTypeChanged( InputTypeTracker.InputType type )
        {
            ApplyInputTypeLayout( type );
            if ( _root == null || _root.style.display == DisplayStyle.None )
                return;
            if ( _lockNewGameFocus )
                return;
            if ( type == InputTypeTracker.InputType.Gamepad )
                _closeButton?.Focus();
            else
                _gameNameField?.Focus();
        }

        void ApplyInputTypeLayout( InputTypeTracker.InputType type )
        {
            bool isGamepad = type == InputTypeTracker.InputType.Gamepad;

            // In gamepad mode: hide the real TextField and show the edit button instead.
            // In mouse/kb mode: show the real TextField and hide the edit button.
            var fieldDisplay = isGamepad ? DisplayStyle.None : DisplayStyle.Flex;
            var editBtnDisplay = isGamepad ? DisplayStyle.Flex : DisplayStyle.None;

            if ( _gameNameField != null )
            {
                _gameNameField.style.display = fieldDisplay;
                _gameNameField.pickingMode = isGamepad ? PickingMode.Ignore : PickingMode.Position;
            }
            if ( _seedField != null )
            {
                _seedField.style.display = fieldDisplay;
                _seedField.pickingMode = isGamepad ? PickingMode.Ignore : PickingMode.Position;
            }
            if ( _gameNameEditRow != null ) _gameNameEditRow.style.display = editBtnDisplay;
            if ( _seedEditRow != null ) _seedEditRow.style.display = editBtnDisplay;

            _navButtons = isGamepad
                ? new Button[] { _closeButton, _gameNameEditBtn, _seedEditBtn, _startButton }
                : new Button[] { _closeButton, _startButton };

            _closeButton.enableRichText = true;
            _closeButton.text = isGamepad ? ( _closeText + " " + InputBindIconData.GetRichTextInputIconString( _cancelAction ) ) : _closeText;

            ApplyFocusabilityState( type );
        }

        void SetNewGameFocusLock( bool locked )
        {
            _lockNewGameFocus = locked;
            ApplyFocusabilityState( InputTypeTracker.Instance.currentType );

            if ( !locked )
                return;

            _closeButton?.Blur();
            _startButton?.Blur();
            _gameNameEditBtn?.Blur();
            _seedEditBtn?.Blur();
            _gameNameField?.Blur();
            _seedField?.Blur();
        }

        void ApplyFocusabilityState( InputTypeTracker.InputType type )
        {
            bool isGamepad = type == InputTypeTracker.InputType.Gamepad;
            bool canFocus = !_lockNewGameFocus;

            if ( _closeButton != null ) _closeButton.focusable = canFocus;
            if ( _startButton != null ) _startButton.focusable = canFocus;
            if ( _gameNameEditBtn != null ) _gameNameEditBtn.focusable = canFocus && isGamepad;
            if ( _seedEditBtn != null ) _seedEditBtn.focusable = canFocus && isGamepad;
            if ( _gameNameField != null ) _gameNameField.focusable = canFocus && !isGamepad;
            if ( _seedField != null ) _seedField.focusable = canFocus && !isGamepad;
            if ( _hardcoreToggle != null ) _hardcoreToggle.focusable = canFocus && !isGamepad;
        }

        void RefreshEditButtonLabels()
        {
            if ( _gameNameEditBtn != null )
                _gameNameEditBtn.text = string.IsNullOrEmpty( _gameNameField?.value ) ? "(empty)" : _gameNameField.value;
            if ( _seedEditBtn != null )
                _seedEditBtn.text = string.IsNullOrEmpty( _seedField?.value ) ? "(empty)" : _seedField.value;
        }

        void OnGameNameEditClicked()
        {
            SetNewGameFocusLock( true );
            _keyboard.Open(
                _root,
                _gameNameField,
                "Game Name",
                onSubmit: RefreshEditButtonLabels,
                onClose: () =>
                {
                    SetNewGameFocusLock( false );
                    RefreshEditButtonLabels();
                    _gameNameEditBtn?.Focus();
                },
                elementToPush: _panel
            );

            if ( !_keyboard.IsOpen )
                SetNewGameFocusLock( false );
        }

        void OnSeedEditClicked()
        {
            SetNewGameFocusLock( true );
            _keyboard.Open(
                _root,
                _seedField,
                "Seed",
                onSubmit: RefreshEditButtonLabels,
                onClose: () =>
                {
                    SetNewGameFocusLock( false );
                    RefreshEditButtonLabels();
                    _seedEditBtn?.Focus();
                },
                elementToPush: _panel
            );

            if ( !_keyboard.IsOpen )
                SetNewGameFocusLock( false );
        }

        void OnStartClicked()
        {
            var gameName = _gameNameField != null ? _gameNameField.value.Trim() : $"Scout{_pendingSaveIdx}";
            if ( string.IsNullOrWhiteSpace( gameName ) )
                gameName = $"Scout{_pendingSaveIdx}";

            var seed = _seedField != null ? _seedField.value.Trim() : string.Empty;
            if ( string.IsNullOrWhiteSpace( seed ) )
                seed = UnityEngine.Random.Range( 0, 99999999 ).ToString();

            Runtime.Settings.Seed = seed;
            Runtime.Settings.Save();

            if ( buttonStartAudio != null )
                Runtime.Audio.SfxManager.Instance.PlayUI( buttonStartAudio );

            GlobalRuntimeConstants.GameName = gameName;
            GlobalRuntimeConstants.SaveIdx = _pendingSaveIdx;
            GlobalRuntimeConstants.Hardcore = _hardcoreToggle?.value ?? false;
            GameSceneManager.Instance.LoadGameScene();
        }

        void OnCloseClicked()
        {
            if ( buttonCloseAudio != null )
                Runtime.Audio.SfxManager.Instance.PlayUI( buttonCloseAudio );

            CloseRequested?.Invoke();
            Show( false );
        }

        void OnCancelPerformed( InputAction.CallbackContext ctx )
        {
            if ( _root != null && _root.style.display != DisplayStyle.None )
            {
                if ( _keyboard.IsOpen )
                {
                    _keyboard.Close();
                    return;
                }
                CloseRequested?.Invoke();
                Show( false );
            }
        }
    }
}
