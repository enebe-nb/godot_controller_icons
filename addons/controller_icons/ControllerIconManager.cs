using Godot;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System;

// Singleton class to manage global events
[Tool]
public partial class ControllerIconManager : Node {
	// Signal Event (using csharp because the argument is not Variant)
	public delegate void InputTypeChangedEventHandler(ControllerIconMapper mapper);
	public event InputTypeChangedEventHandler InputTypeChanged;

	// Singleton instance
	public static ControllerIconManager Instance { get; private set; }

	// Mouse move detection variables
	private const float _MOUSE_VELOCITY_DELTA = 0.1f;
	private float _t = 0;
	private int MouseVelocity = 0;
	private bool queueAutoDetect = true;

	// public readonly ControllerIconMapper Mapper = new(); // TODO make it customizable (settings are annoying)
	// Mapper management variables
	public readonly ControllerIconMapper keyMouseMapper;
	public readonly ControllerIconMapper fallbackMapper;
	private readonly Dictionary<int, ControllerIconMapper> _connectedMappers = [];
	private ControllerIconMapper _currentMapper;
	public ControllerIconMapper CurrentMapper {
		get => _currentMapper;
		set {
			if (value == null) return;
			if (ReferenceEquals(_currentMapper, value)) return;
			_currentMapper = value;
			InputTypeChanged?.Invoke(value);
		}
	}

	public ControllerIconManager() {
		ControllerIconSettings.PrepareAllSettings();
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		// Init default mappers
		var factory = ControllerIconSettings.MapperFactory;
		keyMouseMapper = factory.Create("Keyboard Mouse");
		_currentMapper = fallbackMapper = factory.Create();

		// Load current joypads
		foreach (var device in Input.GetConnectedJoypads()) {
			_connectedMappers[device] = factory.Create(Input.GetJoyName(device));
		}
	}

	public override void _Ready() {
		Input.JoyConnectionChanged += OnJoyConnectionChanged;
	}

	private void OnJoyConnectionChanged(long deviceID, bool connected) {
		if (connected) {
			_connectedMappers[(int)deviceID] = ControllerIconSettings.MapperFactory.Create(Input.GetJoyName((int)deviceID));
			if (!ReferenceEquals(CurrentMapper, keyMouseMapper)) CurrentMapper = _connectedMappers[(int)deviceID];
		} else {
			_connectedMappers.Remove((int)deviceID);
			if (!ReferenceEquals(CurrentMapper, keyMouseMapper)) {
				var joypads = Input.GetConnectedJoypads();
				if (joypads.Count == 0) CurrentMapper = keyMouseMapper;
				else CurrentMapper = _connectedMappers[joypads[0]];
			}
		}
	}

	private bool TestMouseVelocity(Vector2 relative_vec) {
		if (_t > _MOUSE_VELOCITY_DELTA) {
			_t = 0;
			MouseVelocity = 0;
		}

		// We do a component sum instead of a length, to save on a
		// sqrt operation, and because length_squared is negatively
		// affected by low value vectors (<10).
		// It is also good enough for this system, so reliability
		// is sacrificed in favor of speed.
		MouseVelocity += Mathf.RoundToInt(Mathf.Abs(relative_vec.X) + Mathf.Abs(relative_vec.Y));

		return MouseVelocity / _MOUSE_VELOCITY_DELTA > ControllerIconSettings.MouseMinMovement;
	}

	public override void _Input(InputEvent ev) {
		switch (ev) {
			case InputEventKey or InputEventMouseButton:
				CurrentMapper = keyMouseMapper;
				break;
			case InputEventMouseMotion eventMouseMotion:
				if (ControllerIconSettings.AllowMouseRemap && TestMouseVelocity(eventMouseMotion.Relative)) {
					CurrentMapper = keyMouseMapper;
				} break;
			case InputEventJoypadButton:
				CurrentMapper = _connectedMappers[ev.Device];
				break;
			case InputEventJoypadMotion eventJoyMotion:
				if (Mathf.Abs(eventJoyMotion.AxisValue) > ControllerIconSettings.JoypadDeadzone) {
					CurrentMapper = _connectedMappers[ev.Device];
				} break;
		}
	}

	public override void _Process(double delta) {
		_t += (float)delta;

		if (queueAutoDetect) {
			var joypads = Input.GetConnectedJoypads();
			if (joypads.Count == 0) CurrentMapper = keyMouseMapper;
			else CurrentMapper = _connectedMappers[joypads[0]];
			queueAutoDetect = false;
		}
	}
}
