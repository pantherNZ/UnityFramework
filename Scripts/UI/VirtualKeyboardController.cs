using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// A reusable controller-friendly virtual keyboard for UI Toolkit.
    /// Designed for gamepad input — navigates keys with the d-pad and confirms with South button.
    ///
    /// Usage:
    ///   var keyboard = new VirtualKeyboardController();
    ///   keyboard.Open( rootElement, myTextField, "Field Label",
    ///       onSubmit: () => DoSomething(),
    ///       onClose:  () => RestoreFocus() );
    ///
    /// The keyboard adds itself as a child of <paramref name="panelRoot"/> and removes itself on close.
    /// Cancel (B button) is handled externally — call Close() from your view's cancel handler.
    /// </summary>
    public class VirtualKeyboardController
    {
        // ---------------------------------------------------------------------------
        // Key layout — row × column.  Special tokens: "space", "backspace", "enter"
        // ---------------------------------------------------------------------------
        static readonly string[][] KeyLayout =
        {
            new[] { "1","2","3","4","5","6","7","8","9","0" },
            new[] { "q","w","e","r","t","y","u","i","o","p" },
            new[] { "a","s","d","f","g","h","j","k","l" },
            new[] { "z","x","c","v","b","n","m","space" },
            new[] { "backspace","enter" },
        };

        // Animation constants
        const int AnimInMs = 320;
        const int AnimOutMs = 280;
        // How far (px) the "push" element slides upward when the keyboard opens
        const float PushUpPx = 72f;

        VisualElement _keyboardElement;
        VisualElement _panelEl;       // "VirtualKeyboardPanel" child
        VisualElement _pushElement;   // optional element that slides up to make room
        TextField _target;
        Label _display;
        Action _onSubmit;
        Action _onClose;

        Button[][] _grid;
        int _focusRow = 1;
        int _focusCol = 0;
        bool _isClosing;

        public bool IsOpen => !_isClosing && _keyboardElement != null && _keyboardElement.parent != null;

        // ---------------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Opens the virtual keyboard over <paramref name="panelRoot"/>, targeting <paramref name="target"/>.
        /// </summary>
        /// <param name="panelRoot">The VisualElement the keyboard will be added to (typically the UIDocument root).</param>
        /// <param name="target">The TextField whose value will be edited.</param>
        /// <param name="fieldLabel">Optional display name shown above the current value (e.g. "Game Name").</param>
        /// <param name="onSubmit">Callback fired when the user presses Enter. Called before Close().</param>
        /// <param name="onClose">Callback fired when the keyboard is closed for any reason.</param>
        /// <param name="elementToPush">Optional VisualElement (e.g. the game panel) that slides up to make room for the keyboard.</param>
        public void Open( VisualElement panelRoot, TextField target, string fieldLabel = null, Action onSubmit = null, Action onClose = null, VisualElement elementToPush = null )
        {
            if ( IsOpen || _isClosing )
                Close();

            _target = target;
            _onSubmit = onSubmit;
            _onClose = onClose;
            _pushElement = elementToPush;

            var uxmlAsset = Resources.Load<VisualTreeAsset>( "UI/Shared/VirtualKeyboard" );
            if ( uxmlAsset == null )
            {
                UnityEngine.Debug.LogError( "VirtualKeyboardController: VirtualKeyboard.uxml not found at Resources/UI/Shared/VirtualKeyboard" );
                return;
            }

            _keyboardElement = uxmlAsset.Instantiate();

            // The template container must cover the full parent so the panel can anchor to the bottom
            _keyboardElement.style.position = Position.Absolute;
            _keyboardElement.style.left = 0;
            _keyboardElement.style.right = 0;
            _keyboardElement.style.top = 0;
            _keyboardElement.style.bottom = 0;
            _keyboardElement.pickingMode = PickingMode.Ignore;

            // Apply stylesheet programmatically (avoids GUID issues with newly created assets)
            var stylesheet = Resources.Load<StyleSheet>( "UI/Shared/VirtualKeyboard" );
            if ( stylesheet != null )
                _keyboardElement.styleSheets.Add( stylesheet );

            panelRoot.Add( _keyboardElement );

            // ---- Slide-in animation ----
            // Grab the inner panel element so we can translate it
            _panelEl = _keyboardElement.Q<VisualElement>( "VirtualKeyboardPanel" );
            if ( _panelEl != null )
            {
                // Start below the viewport (100 % of the panel height ≈ off-screen)
                _panelEl.style.translate = new StyleTranslate( new Translate( 0, new Length( 110, LengthUnit.Percent ), 0 ) );
            }
            // Set up push-element transition inline so we don't need to touch its own USS
            if ( _pushElement != null )
            {
                _pushElement.style.transitionProperty = new StyleList<StylePropertyName>( new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName( "translate" ) } );
                _pushElement.style.transitionDuration = new StyleList<TimeValue>( new System.Collections.Generic.List<TimeValue> { new TimeValue( AnimInMs, TimeUnit.Millisecond ) } );
                _pushElement.style.transitionTimingFunction = new StyleList<EasingFunction>( new System.Collections.Generic.List<EasingFunction> { new EasingFunction( EasingMode.EaseOut ) } );
            }
            // Defer one frame so the layout pass sets the panel height before we clear the translate
            _keyboardElement.schedule.Execute( () =>
            {
                if ( _panelEl != null )
                    _panelEl.style.translate = new StyleTranslate( new Translate( 0, 0, 0 ) );
                if ( _pushElement != null )
                    _pushElement.style.translate = new StyleTranslate( new Translate( 0, -PushUpPx, 0 ) );
            } );

            // Populate header and display
            var labelEl = _keyboardElement.Q<Label>( "VKFieldLabel" );
            if ( labelEl != null )
                labelEl.text = fieldLabel ?? target?.label ?? string.Empty;

            _display = _keyboardElement.Q<Label>( "VKDisplay" );
            RefreshDisplay();

            // Build the button grid and wire clicks
            _grid = new Button[KeyLayout.Length][];
            for ( int r = 0; r < KeyLayout.Length; r++ )
            {
                var rowEl = _keyboardElement.Q<VisualElement>( $"vk-row-{r}" );
                if ( rowEl == null )
                {
                    _grid[r] = Array.Empty<Button>();
                    continue;
                }

                var btns = rowEl.Query<Button>().ToList();
                _grid[r] = new Button[btns.Count];
                for ( int c = 0; c < btns.Count; c++ )
                {
                    _grid[r][c] = btns[c];
                    btns[c].tabIndex = -1; // disable built-in tab focus; we drive focus manually
                    int captureR = r, captureC = c;
                    btns[c].clicked += () => OnKeyPressed( captureR, captureC );
                }
            }

            // Intercept d-pad navigation at the panel level so we can do 2-D grid movement
            _panelEl?.RegisterCallback<NavigationMoveEvent>( OnNavigationMove, TrickleDown.TrickleDown );

            // Start on 'q' (row 1, col 0)
            _focusRow = 1;
            _focusCol = 0;
            FocusCurrentKey();
        }

        /// <summary>Closes the keyboard (with slide-out animation) and fires the onClose callback.</summary>
        public void Close()
        {
            if ( _keyboardElement == null ) return;

            _isClosing = true;

            _panelEl?.UnregisterCallback<NavigationMoveEvent>( OnNavigationMove );

            // Animate out: slide panel back down, push element back to original position
            if ( _panelEl != null )
                _panelEl.style.translate = new StyleTranslate( new Translate( 0, new Length( 110, LengthUnit.Percent ), 0 ) );
            if ( _pushElement != null )
                _pushElement.style.translate = new StyleTranslate( new Translate( 0, 0, 0 ) );

            // Remove after the animation completes
            var elementToRemove = _keyboardElement;
            var pushToClean = _pushElement;
            var closeCallback = _onClose;
            _keyboardElement.schedule.Execute( () =>
            {
                elementToRemove.RemoveFromHierarchy();
                // Clear the inline transitions we added to the push element
                if ( pushToClean != null )
                {
                    pushToClean.style.transitionProperty = StyleKeyword.Null;
                    pushToClean.style.transitionDuration = StyleKeyword.Null;
                    pushToClean.style.transitionTimingFunction = StyleKeyword.Null;
                }
                closeCallback?.Invoke();
            } ).StartingIn( AnimOutMs + 30 );

            _keyboardElement = null;
            _panelEl = null;
            _pushElement = null;
            _grid = null;
            _isClosing = false; // allow re-open once animation fires (element ref already null)
        }

        // ---------------------------------------------------------------------------
        // Internal — navigation
        // ---------------------------------------------------------------------------

        void OnNavigationMove( NavigationMoveEvent evt )
        {
            // Prevent UI Toolkit's default focus movement; we drive the grid ourselves
            evt.StopPropagation();
            evt.PreventDefault();

            switch ( evt.direction )
            {
                case NavigationMoveEvent.Direction.Left:
                    _focusCol = ( _focusCol - 1 + _grid[_focusRow].Length ) % _grid[_focusRow].Length;
                    break;
                case NavigationMoveEvent.Direction.Right:
                    _focusCol = ( _focusCol + 1 ) % _grid[_focusRow].Length;
                    break;
                case NavigationMoveEvent.Direction.Up:
                    {
                        int prev = ( _focusRow - 1 + _grid.Length ) % _grid.Length;
                        _focusCol = Mathf.Clamp( _focusCol, 0, _grid[prev].Length - 1 );
                        _focusRow = prev;
                        break;
                    }
                case NavigationMoveEvent.Direction.Down:
                    {
                        int next = ( _focusRow + 1 ) % _grid.Length;
                        _focusCol = Mathf.Clamp( _focusCol, 0, _grid[next].Length - 1 );
                        _focusRow = next;
                        break;
                    }
            }

            FocusCurrentKey();
        }

        // ---------------------------------------------------------------------------
        // Internal — key actions
        // ---------------------------------------------------------------------------

        void OnKeyPressed( int row, int col )
        {
            if ( _target == null ) return;
            if ( row >= KeyLayout.Length || col >= KeyLayout[row].Length ) return;

            switch ( KeyLayout[row][col] )
            {
                case "backspace":
                    if ( _target.value.Length > 0 )
                        _target.value = _target.value[..^1];
                    break;

                case "enter":
                    _onSubmit?.Invoke();
                    Close();
                    return; // Close already fires _onClose; skip RefreshDisplay

                case "space":
                    AppendChar( ' ' );
                    break;

                default:
                    AppendChar( KeyLayout[row][col][0] );
                    break;
            }

            RefreshDisplay();
        }

        void AppendChar( char c )
        {
            if ( _target == null ) return;
            int max = _target.maxLength > 0 ? _target.maxLength : int.MaxValue;
            if ( _target.value.Length < max )
                _target.value += c;
        }

        void RefreshDisplay()
        {
            if ( _display != null )
                _display.text = ( _target?.value ?? string.Empty ) + "│";
        }

        void FocusCurrentKey()
        {
            if ( _grid == null ) return;
            _grid[_focusRow]?[_focusCol]?.Focus();
        }
    }
}
