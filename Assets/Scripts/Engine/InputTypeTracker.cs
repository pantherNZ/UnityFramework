using UnityEngine;
using UnityEngine.InputSystem;

public class InputTypeTracker : MonoBehaviour
{
	public static InputTypeTracker Instance;

	public enum InputType { KeyboardMouse, Gamepad, Unknown }
	[HideInInspector] public InputType currentType { get; set; } = InputType.Unknown;
	[HideInInspector] public event System.Action<InputType> onInputTypeChanged;

	[SerializeField] PlayerInput playerInput;
	[SerializeField] public InputActionAsset actions;

	//public InputAction[] SkillActions { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Debug.LogWarning("Multiple instances of InputTypeTracker detected. Destroying duplicate.");
			Destroy(gameObject);
		}

		currentType = (InputType)Runtime.Settings.LastInputType;
		if (currentType == InputType.Unknown)
			currentType = InputType.KeyboardMouse;
	}

	void Start()
	{
		actions.Enable();

		if (playerInput != null)
		{
			playerInput.onControlsChanged += OnControlsChanged;
			playerInput.onDeviceLost += OnDeviceLost;
			playerInput.onDeviceRegained += OnDeviceRegained;
		}

		onInputTypeChanged?.Invoke(currentType);

		SetupBindings();

		//SkillActions = new InputAction[System.Enum.GetValues(typeof(BindingType)).Length - 1];
		//SkillActions[0] = InputSystem.actions.FindAction("Gameplay/Skill1");
		//SkillActions[1] = InputSystem.actions.FindAction("Gameplay/Skill2");
		//SkillActions[2] = InputSystem.actions.FindAction("Gameplay/Skill3");
		//SkillActions[3] = InputSystem.actions.FindAction("Gameplay/Skill4");
		//SkillActions[4] = InputSystem.actions.FindAction("Gameplay/Skill5");
		//SkillActions[5] = InputSystem.actions.FindAction("Gameplay/Consumable");
		//SkillActions[6] = InputSystem.actions.FindAction("Gameplay/Dash");
	}

	void SetupBindings()
	{
		if (Runtime.Settings.Bindings != string.Empty)
			actions.LoadBindingOverridesFromJson(Runtime.Settings.Bindings);
	}

	private void OnControlsChanged(PlayerInput input)
	{
		OnControlsChanged(input.currentControlScheme);
	}

	private void OnControlsChanged(string scheme)
	{
		if (scheme == "Gamepad" && currentType != InputType.Gamepad)
		{
			currentType = InputType.Gamepad;
			Runtime.Settings.LastInputType = (int)currentType;
			onInputTypeChanged?.Invoke(currentType);
		}
		else if (scheme == "Keyboard&Mouse" && currentType != InputType.KeyboardMouse)
		{
			currentType = InputType.KeyboardMouse;
			Runtime.Settings.LastInputType = (int)currentType;
			onInputTypeChanged?.Invoke(currentType);
		}
	}

	private void OnDeviceLost(PlayerInput input)
	{
		// Try to auto-switch to another available input type
		bool hasGamepad = false;
		bool hasKeyboardMouse = false;

		foreach (var device in InputSystem.devices)
		{
			if (device is UnityEngine.InputSystem.Gamepad) hasGamepad = true;
			else if (device is UnityEngine.InputSystem.Keyboard || device is UnityEngine.InputSystem.Mouse) hasKeyboardMouse = true;
		}

		if (currentType == InputType.Gamepad && !hasGamepad)
		{
			if (hasKeyboardMouse)
			{
				currentType = InputType.KeyboardMouse;
				Runtime.Settings.LastInputType = (int)currentType;
				onInputTypeChanged?.Invoke(currentType);
			}
			else
			{
				currentType = InputType.Unknown;
				onInputTypeChanged?.Invoke(currentType);
			}
		}
		else if (currentType == InputType.KeyboardMouse && !hasKeyboardMouse)
		{
			if (hasGamepad)
			{
				currentType = InputType.Gamepad;
				Runtime.Settings.LastInputType = (int)currentType;
				onInputTypeChanged?.Invoke(currentType);
			}
			else
			{
				currentType = InputType.Unknown;
				onInputTypeChanged?.Invoke(currentType);
			}
		}
	}

	private void OnDeviceRegained(PlayerInput input)
	{
		// Re-evaluate based on current control scheme after device is reconnected
		OnControlsChanged(input.currentControlScheme);
	}
}
