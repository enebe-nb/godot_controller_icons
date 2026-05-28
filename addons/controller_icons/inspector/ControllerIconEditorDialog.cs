using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Tracing;
using System.Transactions;

[Tool]
public partial class ControllerIconEditorDialog : ConfirmationDialog {
	// GUI Nodes
	private TabContainer _tabControl;
	private LineEdit _actionFilter;
	private ItemList _actionList;
	private LineEdit _inputFilter;
	private Tree _inputList;

	public int TabIndex { get => _tabControl.CurrentTab; }
	public string currentActionName;
	public InputEvent currentInputEvent;
	private bool _isListening;

	public override void _Ready() {
		_tabControl = GetNode<TabContainer>("%TabControl");
		_actionFilter = GetNode<LineEdit>("%ActionFilter");
		_actionList = GetNode<ItemList>("%ActionList");
		_inputFilter = GetNode<LineEdit>("%InputFilter");
		_inputList = GetNode<Tree>("%InputList");

		GetNode<LineEdit>("%ActionFilter").TextChanged += OnActionFilterChanged;
		GetNode<LineEdit>("%InputFilter").TextChanged += OnInputFilterChanged;
		GetNode<Button>("%ListenButton").Pressed += OnListenButtonPressed;
		_actionList.ItemActivated += OnActionActivated;
		_actionList.ItemSelected += OnActionSelected;
		_inputList.ItemActivated += OnInputActivated;
		_inputList.ItemSelected += OnInputSelected;

		UpdateActionList("");
		UpdateInputList("");
	}

	public override void _Input(InputEvent evt) {
		if (!_isListening) return;
		if (evt.IsPressed() && evt.IsActionType()) {
			currentInputEvent = evt;
			_inputFilter.PlaceholderText = evt.AsText();
			_isListening = false;
		}
		GetViewport()?.SetInputAsHandled();
	}

	private void OnListenButtonPressed() {
		_inputFilter.Clear();
		_isListening = true;
		_inputFilter.PlaceholderText = "Waiting input ...";
	}

	private void OnActionFilterChanged(string text) {
		UpdateActionList(text);
	}

	private void OnActionActivated(long index) {
		OnActionSelected(index);
		EmitSignalConfirmed();
		Hide();
	}

	private void OnActionSelected(long index) {
		if (index < 0) currentActionName = "";
		else currentActionName = _actionList.GetItemMetadata((int)index).As<string>();
	}

	private void OnInputFilterChanged(string text) {
		UpdateInputList(text);
		_inputFilter.PlaceholderText = "Filter by Input Name";
		_inputList.GetRoot().SetCollapsedRecursive(text != "");
	}

	private void OnInputActivated() {
		OnInputSelected();
		EmitSignalConfirmed();
		Hide();
	}

	private void OnInputSelected() {
		var item = _inputList.GetSelected();
		currentInputEvent = item.GetMeta("__event").As<InputEvent>();
		_inputFilter.PlaceholderText = "Filter by Input Name";
	}

	private void UpdateActionList(string filter) {
		// TODO texture icons?
		_actionList.Clear();
		currentActionName = "";
		foreach (var action in InputMap.GetActions()) {
			if (!action.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			var index = _actionList.AddItem($"{action} ({InputMap.GetActionDescription(action)})");
			_actionList.SetItemMetadata(index, action);
		}
	}

	private static readonly Key[] _keyList = [
		Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.K, Key.L, Key.M, Key.N,
		Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,

		Key.F1,   Key.F2,   Key.F3,   Key.F4,   Key.F5,   Key.F6,   Key.F7,   Key.F8,   Key.F9,   Key.F10,  Key.F11,  Key.F12,
		Key.Key0, Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9,
		Key.Kp0,  Key.Kp1,  Key.Kp2,  Key.Kp3,  Key.Kp4,  Key.Kp5,  Key.Kp6,  Key.Kp7,  Key.Kp8,  Key.Kp9,
		Key.KpAdd, Key.KpDivide, Key.KpEnter, Key.KpMultiply, Key.KpPeriod, Key.KpSubtract,

		Key.Up, Key.Down, Key.Left, Key.Right, Key.Insert, Key.Delete, Key.Home, Key.End, Key.Pageup, Key.Pagedown,
		Key.Shift, Key.Ctrl, Key.Alt, Key.Meta,
		Key.Backspace, Key.Capslock, Key.Enter, Key.Escape, Key.Numlock, Key.Print, Key.Space, Key.Tab,
		Key.Apostrophe, Key.Asciitilde, Key.Asterisk, Key.Backslash, Key.Bracketleft, Key.Bracketright, Key.Comma, Key.Equal,
		Key.Greater, Key.Less, Key.Minus, Key.Period, Key.Plus, Key.Question, Key.Quotedbl, Key.Quoteleft, Key.Semicolon, Key.Slash,
	];

	private static readonly MouseButton[] _mouseList = [
		MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.WheelUp, MouseButton.WheelDown, MouseButton.Xbutton1, MouseButton.Xbutton2,
	];

	private static readonly JoyAxis[] _axisList = [
		JoyAxis.LeftX, JoyAxis.LeftY, JoyAxis.RightX, JoyAxis.RightY, JoyAxis.TriggerLeft, JoyAxis.TriggerRight,
	];

	private static readonly JoyButton[] _joypadList = [
		JoyButton.A, JoyButton.B, JoyButton.X, JoyButton.Y, JoyButton.Back, JoyButton.Guide, JoyButton.Start,
		JoyButton.LeftStick, JoyButton.RightStick, JoyButton.LeftShoulder, JoyButton.RightShoulder,
		JoyButton.DpadUp, JoyButton.DpadDown, JoyButton.DpadLeft, JoyButton.DpadRight,
		JoyButton.Misc1, JoyButton.Paddle1, JoyButton.Paddle2, JoyButton.Paddle3, JoyButton.Paddle4, JoyButton.Touchpad,
	];

	private void UpdateInputList(string filter) {
		_inputList.Clear();
		currentInputEvent = null;
		var root = _inputList.CreateItem();

		var keyboard = _inputList.CreateItem(root);
		keyboard.SetText(0, "Keyboard Keys");
		keyboard.SetCollapsedRecursive(true);
		keyboard.SetSelectable(0, false);
		foreach (var value in _keyList) {
			// TODO do we use Visible instead to filter?
			if (!value.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			var evt = new InputEventKey(){Keycode = value};
			var item = _inputList.CreateItem(keyboard);
			item.SetText(0, evt.AsTextKeycode());
			item.SetMeta("__event", evt);
		} keyboard.Visible = keyboard.GetChildCount() > 0;

		var mouse = _inputList.CreateItem(root);
		mouse.SetText(0, "Mouse Buttons");
		mouse.SetCollapsedRecursive(true);
		mouse.SetSelectable(0, false);
		foreach (var value in _mouseList) {
			if (!value.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			var evt = new InputEventMouseButton(){ButtonIndex = value};
			var item = _inputList.CreateItem(mouse);
			item.SetText(0, evt.AsText());
			item.SetMeta("__event", evt);
		} mouse.Visible = mouse.GetChildCount() > 0;

		var joypad = _inputList.CreateItem(root);
		joypad.SetText(0, "Joypad");
		joypad.SetCollapsedRecursive(true);
		joypad.SetSelectable(0, false);
		foreach (var value in _axisList) {
			if (!value.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			var evt = new InputEventJoypadMotion(){Axis = value};
			var item = _inputList.CreateItem(joypad);
			item.SetText(0, evt.AsText());
			item.SetMeta("__event", evt);
		} foreach (var value in _joypadList) {
			if (!value.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
			var evt = new InputEventJoypadButton(){ButtonIndex = value};
			var item = _inputList.CreateItem(joypad);
			item.SetText(0, evt.AsText());
			item.SetMeta("__event", evt);
		} joypad.Visible = joypad.GetChildCount() > 0;
	}
}
