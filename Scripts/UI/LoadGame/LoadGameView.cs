using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Runtime.Audio;
using Runtime.Game;
using Save;
using Schema.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
    [DisallowMultipleComponent]
    public class LoadGameView : MonoBehaviour
    {
        [Header( "Assets" )]
        [SerializeField] Texture2D defaultIcon;
        [SerializeField] Texture2D hardcoreSkullIcon;
        [SerializeField] Schema.PopupSchema deleteConfirmationPopupSchema;
        [SerializeField] AudioDataSchema buttonPressAudio;
        [SerializeField] AudioDataSchema deleteButtonAudio;
        [SerializeField] AudioDataSchema buttonPressCloseAudio;

        UIDocument _doc;
        VisualElement _root;

        VisualElement _saveList;

        VisualElement _detailsIcon;
        VisualElement _detailsHardcoreIcon;
        Label _detailsTitle;
        Label _detailsLevelLabel;
        Label _detailsDescription;
        Button _loadButton;
        Button _deleteButton;
        string _closeText;
        Button _closeButton;

        VisualElement _overlay;
        VisualElement _veil;
        VisualElement _panel;

        List<SaveMetaData> _saves;
        VisualElement _selectedItem;
        SaveMetaData _selectedSave;

        InputAction _cancelAction;
        InputAction _nextAction;
        InputAction _previousAction;

        public event Action<SaveMetaData> LoadRequested;
        public event Action CloseRequested;

        void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            _root = _doc ? _doc.rootVisualElement : null;
            if ( _root == null ) return;

            _overlay = _root.Q<VisualElement>( "Overlay" );
            _veil = _root.Q<VisualElement>( "Veil" );
            _panel = _root.Q<VisualElement>( "Panel" );

            ConfigureCenteredLayout();

            _saveList = _root.Q<VisualElement>( "SaveList" );
            _detailsIcon = _root.Q<VisualElement>( "DetailsIcon" );
            _detailsHardcoreIcon = new VisualElement();
            _detailsHardcoreIcon.style.width = 28;
            _detailsHardcoreIcon.style.height = 28;
            _detailsHardcoreIcon.style.flexShrink = 0;
            _detailsHardcoreIcon.style.alignSelf = Align.Center;
            _detailsHardcoreIcon.style.marginLeft = 6;
            _detailsHardcoreIcon.style.display = DisplayStyle.None;
            _detailsHardcoreIcon.style.unityBackgroundImageTintColor = new UnityEngine.Color( 1f, 0.2f, 0.2f );
            _root.Q<VisualElement>( "DetailsLevelRow" )?.Add( _detailsHardcoreIcon );
            _detailsTitle = _root.Q<Label>( "DetailsTitle" );
            _detailsLevelLabel = _root.Q<Label>( "DetailsLevelLabel" );
            _detailsDescription = _root.Q<Label>( "DetailsDescription" );
            _loadButton = _root.Q<Button>( "LoadButton" );
            _deleteButton = _root.Q<Button>( "DeleteButton" );
            _closeButton = _root.Q<Button>( "CloseButton" );
            _closeText = _closeButton.text;

            if ( _loadButton != null ) _loadButton.clicked += OnLoadClicked;
            if ( _deleteButton != null ) _deleteButton.clicked += OnDeleteClicked;
            if ( _closeButton != null ) _closeButton.clicked += OnCloseClicked;

            _cancelAction = InputSystem.actions.FindAction( "UI/Cancel" );
            if ( _cancelAction != null ) _cancelAction.started += OnCancelPerformed;

            _nextAction = InputSystem.actions.FindAction( "UI/PageNext" );
            _previousAction = InputSystem.actions.FindAction( "UI/PagePrevious" );
            if ( _nextAction != null ) _nextAction.started += OnNextItem;
            if ( _previousAction != null ) _previousAction.started += OnPreviousItem;

            ApplyInputTypeLayout( InputTypeTracker.Instance.currentType );
            InputTypeTracker.Instance.onInputTypeChanged += ApplyInputTypeLayout;
        }

        void ApplyInputTypeLayout( InputTypeTracker.InputType type )
        {
            bool isGamepad = type == InputTypeTracker.InputType.Gamepad;

            _closeButton.enableRichText = true;
            _closeButton.text = isGamepad ? ( _closeText + " " + InputBindIconData.GetRichTextInputIconString( _cancelAction ) ) : _closeText;
        }

        void OnDisable()
        {
            if ( _loadButton != null ) _loadButton.clicked -= OnLoadClicked;
            if ( _deleteButton != null ) _deleteButton.clicked -= OnDeleteClicked;
            if ( _closeButton != null ) _closeButton.clicked -= OnCloseClicked;
            if ( _cancelAction != null ) _cancelAction.started -= OnCancelPerformed;
            if ( _nextAction != null ) _nextAction.started -= OnNextItem;
            if ( _previousAction != null ) _previousAction.started -= OnPreviousItem;

            InputTypeTracker.Instance.onInputTypeChanged -= ApplyInputTypeLayout;
        }

        public void Show( bool visible )
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if ( visible )
            {
                ConfigureCenteredLayout();
                _root.Focus();
                RebuildList();
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

        void RebuildList()
        {
            _saves = Save.SaveManager.Instance.GetFromDirectory<Save.SaveMetaData>( GlobalConstantsHandler.Constants.RootSavePath )
                .OrderByDescending( s => s.lastPlayed )
                .ToList();
            _saveList.Clear();
            _selectedItem = null;
            _selectedSave = null;
            ClearDetails();
            bool firstItemSet = false;

            foreach ( var save in _saves )
            {
                var item = new VisualElement();
                item.AddToClassList( "list-item" );
                item.userData = save;

                var icon = new VisualElement();
                icon.AddToClassList( "item-icon" );
                var tex = defaultIcon; // no icon in SaveMetaData; use default
                if ( tex != null ) icon.style.backgroundImage = new StyleBackground( tex );
                item.Add( icon );

                var texts = new VisualElement();
                texts.AddToClassList( "item-texts" );

                var nameLabel = new Label( save.gameName ?? "Unnamed Save" );
                nameLabel.AddToClassList( "item-name" );
                texts.Add( nameLabel );

                var metaText = $"Last Played: {FormatDate( save.lastPlayed )}";
                var metaLabel = new Label( metaText );
                metaLabel.AddToClassList( "item-meta" );
                texts.Add( metaLabel );

                item.Add( texts );

                var levelBadge = new VisualElement();
                levelBadge.AddToClassList( "item-level-badge" );
                var lIcon = new VisualElement();
                lIcon.AddToClassList( "item-level-icon" );
                var lLabel = new Label( ( 1 + save.level ).ToString() );
                lLabel.AddToClassList( "item-level-label" );
                lIcon.Add( lLabel );
                levelBadge.Add( lIcon );
                item.Add( levelBadge );

                if ( save.isHardcore )
                {
                    var skullIcon = new VisualElement();
                    skullIcon.style.width = 28;
                    skullIcon.style.height = 28;
                    skullIcon.style.marginLeft = 6;
                    skullIcon.style.marginRight = 2;
                    skullIcon.style.alignSelf = Align.Center;
                    skullIcon.style.flexShrink = 0;
                    if ( hardcoreSkullIcon != null )
                        skullIcon.style.backgroundImage = new StyleBackground( hardcoreSkullIcon );
                    skullIcon.style.unityBackgroundImageTintColor = new UnityEngine.Color( 1f, 0.2f, 0.2f );
                    item.Add( skullIcon );
                }

                item.RegisterCallback<ClickEvent>( _ => OnItemClicked( item, save ) );
                _saveList.Add( item );

                if ( !firstItemSet )
                {
                    OnItemClicked( item, save );
                    firstItemSet = true;
                }
            }
        }

        void OnItemClicked( VisualElement item, SaveMetaData save )
        {
            if ( _selectedItem != null )
                _selectedItem.RemoveFromClassList( "selected" );
            _selectedItem = item; _selectedSave = save;
            if ( _selectedItem != null )
                _selectedItem.AddToClassList( "selected" );

            UpdateDetails( save );

            SfxManager.Instance.PlayUI( buttonPressAudio );
        }

        void UpdateDetails( SaveMetaData save )
        {
            _detailsTitle.text = save.gameName ?? "Unnamed Save";
            var description = new StringBuilder();
            description.AppendLine( $"Scout{save.saveIdx}" );
            description.AppendLine( $"Last Played: {FormatDate( save.lastPlayed )}" );
            description.AppendLine( $"Level: {1 + save.level}" );
            description.AppendLine( $"Max Danger Reached: {save.maxDangerReached}" );
            description.AppendLine( $"Seed: {save.seed}" );
            if ( save.isHardcore )
                description.AppendLine( "HARDCORE".Red() );
            _detailsDescription.text = description.ToString();
            _detailsIcon.style.backgroundImage = defaultIcon != null ? new StyleBackground( defaultIcon ) : StyleKeyword.None;
            if ( _detailsLevelLabel != null )
                _detailsLevelLabel.text = ( 1 + save.level ).ToString();

            if ( save.isHardcore )
            {
                if ( _detailsHardcoreIcon != null )
                {
                    if ( hardcoreSkullIcon != null )
                        _detailsHardcoreIcon.style.backgroundImage = new StyleBackground( hardcoreSkullIcon );
                    _detailsHardcoreIcon.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                _detailsTitle.style.color = StyleKeyword.Null;
                _detailsDescription.style.color = StyleKeyword.Null;
                if ( _detailsHardcoreIcon != null )
                    _detailsHardcoreIcon.style.display = DisplayStyle.None;
            }
        }

        void ClearDetails()
        {
            _detailsTitle.text = "Select a save";
            _detailsDescription.text = string.Empty;
            _detailsIcon.style.backgroundImage = StyleKeyword.None;
            if ( _detailsLevelLabel != null )
                _detailsLevelLabel.text = string.Empty;
        }

        void OnNextItem( InputAction.CallbackContext _ ) => StepSelection( forward: true );
        void OnPreviousItem( InputAction.CallbackContext _ ) => StepSelection( forward: false );

        void StepSelection( bool forward )
        {
            if ( _root == null || _root.style.display == DisplayStyle.None ) return;
            if ( _saves == null || _saves.Count == 0 ) return;

            int current = _selectedSave != null ? _saves.IndexOf( _selectedSave ) : -1;
            if ( current == -1 ) current = forward ? _saves.Count - 1 : 0;
            int next = forward
                ? ( current + 1 ) % _saves.Count
                : ( current - 1 + _saves.Count ) % _saves.Count;

            var items = _saveList.Children().ToList();
            if ( next < items.Count )
                OnItemClicked( items[next], _saves[next] );
        }

        void OnLoadClicked()
        {
            if ( _selectedSave != null )
            {
                LoadRequested?.Invoke( _selectedSave );
            }
        }

        void OnDeleteClicked()
        {
            if ( _selectedSave != null )
            {
                SfxManager.Instance.PlayUI( buttonPressAudio );
                Popup.YesNoCancel( _root, deleteConfirmationPopupSchema, ( callback ) =>
                {
                    if ( callback == Popup.ReturnState.Yes )
                    {
                        SaveManager.Instance.DeleteSave( $"{GlobalConstantsHandler.Constants.RootSavePath}/{_selectedSave.gameName}" );
                        SfxManager.Instance.PlayUI( deleteButtonAudio );
                        RebuildList();

                        if ( _saves.IsEmpty() )
                            OnCloseClicked();
                    }
                    else
                    {
                        SfxManager.Instance.PlayUI( buttonPressCloseAudio );
                    }
                }, playAudio: false );
            }
        }

        void OnCloseClicked()
        {
            CloseRequested?.Invoke();
            Show( false );
        }

        void OnCancelPerformed( InputAction.CallbackContext ctx )
        {
            if ( _root != null && _root.style.display != DisplayStyle.None )
            {
                CloseRequested?.Invoke();
                Show( false );
            }
        }

        static string FormatDate( System.DateTime dt )
        {
            return dt.ToString( "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture );
        }
    }
}
