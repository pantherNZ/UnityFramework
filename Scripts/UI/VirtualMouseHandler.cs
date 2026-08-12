using Runtime.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class VirtualMouseHandler : MonoBehaviour
{
    [SerializeField] RectTransform canvas;

    VirtualMouseInput virtualMouseInput;
    Vector2 lastMousePos;

    void Awake()
    {
        virtualMouseInput = GetComponent<VirtualMouseInput>();
    }

    void Start()
    {
        InputTypeTracker.Instance.onInputTypeChanged += OnInputTypeChanged;

        OnInputTypeChanged( InputTypeTracker.InputType.KeyboardMouse );
    }

    void Update()
    {
        transform.localScale = Vector3.one * ( 1.0f / canvas.localScale.x );
        transform.SetAsLastSibling();
    }

    void LateUpdate()
    {
        if ( !virtualMouseInput.enabled )
        {
            lastMousePos = Mouse.current.position.ReadValue();
            return;
        }

        var pos = virtualMouseInput.virtualMouse.position.value;
        pos.x = Mathf.Clamp( pos.x, 0, Screen.width );
        pos.y = Mathf.Clamp( pos.y, 0, Screen.height );
        InputState.Change( virtualMouseInput.virtualMouse, pos );
    }

    void OnInputTypeChanged( InputTypeTracker.InputType newType )
    {
        bool gamepad = newType == InputTypeTracker.InputType.Gamepad;
        virtualMouseInput.enabled = gamepad;
        virtualMouseInput.cursorTransform.gameObject.SetActive( gamepad );
        virtualMouseInput.cursorGraphic.gameObject.SetActive( gamepad );

        if ( gamepad )
        {
            InputState.Change( virtualMouseInput.virtualMouse, lastMousePos );
            GameSceneManager.Instance.OverrideCursorVisibility( false );
        }
        else
        {
            GameSceneManager.Instance.ResetCursorVisibility();
        }
    }
}
