using Godot;
using System;
using System.Collections.Generic;

// Base abstract class to remap buttons into icon resource files
public abstract class ControllerIconMapper {
	// Returns a resource path for a keyboard key
	public virtual string GetResourcePath(Key key) => throw new NotImplementedException("Please use a derivated ControllerIconMapper");
	// Returns a resource path for a mouse button
	public virtual string GetResourcePath(MouseButton btn) => throw new NotImplementedException("Please use a derivated ControllerIconMapper");
	// Returns a resource path for a joypad axis
	public virtual string GetResourcePath(JoyAxis axis) => throw new NotImplementedException("Please use a derivated ControllerIconMapper");
	// Returns a resource path for a joypad button
	public virtual string GetResourcePath(JoyButton btn) => throw new NotImplementedException("Please use a derivated ControllerIconMapper");
	// Checks if a Event is compatible with this mapper
	public abstract bool IsEventCompatible(InputEvent evt);
	// Returns all icons accociated with a event
	public abstract string[] GetEventIcons(InputEvent evt);
}

// Factory class that instantiate mapper class from a device name
public partial class ControllerIconMapperFactory : GodotObject {
	private static Dictionary<string, ControllerIconMapper> _cache = [];

	// Instantiate or get from cache a Mapper for a named device, if the name is empty returns a fallback Mapper
	public virtual ControllerIconMapper Create(string deviceName = "") {
		if (_cache.TryGetValue(deviceName, out var mapper)) return mapper; // try cache

		if (deviceName == "") 									mapper = new ControllerIconMapperFallback();
		else if (deviceName.Contains("Keyboard Mouse"))			mapper = new ControllerIconMapperKeyMouse();
		else if (deviceName.Contains("PS3 Controller"))			mapper = new ControllerIconMapperPS3();
		else if (deviceName.Contains("PS4 Controller")
			|| deviceName.Contains("DUALSHOCK 4"))				mapper = new ControllerIconMapperPS4();
		else if (deviceName.Contains("PS5 Controller")
			|| deviceName.Contains("DualSense"))				mapper = new ControllerIconMapperPS5();
		else if (deviceName.Contains("Xbox 360 Controller"))	mapper = new ControllerIconMapperXBox360();
		else if (deviceName.Contains("Xbox One")
			|| deviceName.Contains("X-Box One")
			|| deviceName.Contains("Xbox Wireless Controller"))	mapper = new ControllerIconMapperXBoxOne();
		else if (deviceName.Contains("Xbox Series"))			mapper = new ControllerIconMapperXBoxSeries();

		if (mapper != null) _cache[deviceName] = mapper;
		else GD.PrintErr($"Unsupported controller: {deviceName}");
		return mapper;

		// TODO test other devices
		if (deviceName.Contains("Luna Controller"))			return mapper = new ControllerIconMapperLuna();
		if (deviceName.Contains("Stadia Controller"))		return mapper = new ControllerIconMapperStadia();
		if (deviceName.Contains("Steam Controller") )		return mapper = new ControllerIconMapperSteam();
		if (deviceName.Contains("Switch Controller")
			|| deviceName.Contains("Switch Pro Controller"))return mapper = new ControllerIconMapperSwitch();
		if (deviceName.Contains("Joy-Con"))					return mapper = new ControllerIconMapperSwitch(); // joycon
		if (deviceName.Contains("Steam Deck")
			|| deviceName.Contains("Steam Virtual Gamepad"))return mapper = new ControllerIconMapperSteamDeck();
		if (deviceName.Contains("OUYA Controller"))			return mapper = new ControllerIconMapperOuya();
	}
}

// Mapper implementation for Keyboard and Mouse inputs
public class ControllerIconMapperKeyMouse : ControllerIconMapper {
	protected readonly bool IsMacOS = OS.GetName() == "macOS";

	public override bool IsEventCompatible(InputEvent evt) => evt is InputEventKey || evt is InputEventMouseButton;
	public override string GetResourcePath(MouseButton btn) => $"res://addons/controller_icons/assets/mouse/{btn}.png";
	public override string GetResourcePath(Key key) {
		var keyName = key.ToString();
		if (IsMacOS && (key == Key.Meta || key == Key.Ctrl)) keyName = "Command"; // TODO verify macOS differences
		return $"res://addons/controller_icons/assets/key/{keyName}.png" ;
	}

	protected List<string> ParseModifiers(InputEventWithModifiers evt) {
		if (evt == null) return [];
		List<string> result = [];

		if (evt.CommandOrControlAutoremap) result.Add(GetResourcePath(Key.Ctrl));
		else {
			if (evt.CtrlPressed) result.Add(GetResourcePath(Key.Ctrl));
			if (evt.MetaPressed) result.Add(GetResourcePath(Key.Meta));
		}

		if (evt.ShiftPressed) result.Add(GetResourcePath(Key.Shift));
		if (evt.AltPressed) result.Add(GetResourcePath(Key.Alt));
		return result;
	}

	public override string[] GetEventIcons(InputEvent evt) {
		var modifiers = ParseModifiers(evt as InputEventWithModifiers);
		if (evt is InputEventKey keyEvent) {
			var code = keyEvent.Keycode != 0 ? keyEvent.Keycode : DisplayServer.KeyboardGetKeycodeFromPhysical(keyEvent.PhysicalKeycode);
			return [.. modifiers, GetResourcePath(code)];
		} else if (evt is InputEventMouseButton mouseEvent) {
			return [.. modifiers, GetResourcePath(mouseEvent.ButtonIndex)];
		} else return [];
	}
}

// Base Mapper implementation for joypads
public abstract class ControllerIconMapperJoypad : ControllerIconMapper {
	public override bool IsEventCompatible(InputEvent evt) => evt is InputEventJoypadButton || evt is InputEventJoypadMotion;
	public override string[] GetEventIcons(InputEvent evt) {
		if (evt is InputEventJoypadButton btnEvent) {
			return [GetResourcePath(btnEvent.ButtonIndex)];
		} else if (evt is InputEventJoypadMotion moveEvent) {
			return [GetResourcePath(moveEvent.Axis)];
		} else return [];
	}
}

// Fallback Mapper is just a interface for two keybaord+joypad mappers
public class ControllerIconMapperFallback : ControllerIconMapper {
	private readonly ControllerIconMapper keyMouse = ControllerIconSettings.MapperFactory.Create("Keyboard Mouse");
	private readonly ControllerIconMapper joypad = ControllerIconSettings.MapperFactory.Create(ControllerIconSettings.JoypadFallback);

	public override bool IsEventCompatible(InputEvent evt) => keyMouse.IsEventCompatible(evt) || joypad.IsEventCompatible(evt);
	public override string GetResourcePath(MouseButton btn) => keyMouse.GetResourcePath(btn);
	public override string GetResourcePath(Key key) => keyMouse.GetResourcePath(key);
	public override string GetResourcePath(JoyButton btn) => joypad.GetResourcePath(btn);
	public override string GetResourcePath(JoyAxis axis) => joypad.GetResourcePath(axis);
	public override string[] GetEventIcons(InputEvent evt) {
		if (keyMouse.IsEventCompatible(evt)) return keyMouse.GetEventIcons(evt);
		else if (joypad.IsEventCompatible(evt)) return joypad.GetEventIcons(evt);
		else return [];
	}
}

public class ControllerIconMapperLuna : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/luna/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/luna/{btn}.png";
}

public class ControllerIconMapperOuya : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/ouya/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/ouya/{btn}.png";
}

public class ControllerIconMapperPS3 : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/ps3/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/ps3/{btn}.png";
}

public class ControllerIconMapperPS4 : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/ps4/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/ps4/{btn}.png";
}

public class ControllerIconMapperPS5 : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/ps5/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/ps5/{btn}.png";
}

public class ControllerIconMapperStadia : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/stadia/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/stadia/{btn}.png";
}

public class ControllerIconMapperSteam : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/steam/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/steam/{btn}.png";
}

public class ControllerIconMapperSteamDeck : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/steamdeck/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/steamdeck/{btn}.png";
}

public class ControllerIconMapperSwitch : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/switch/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/switch/{btn}.png";
}

public class ControllerIconMapperXBox360 : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/xbox360/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/xbox360/{btn}.png";
}

public class ControllerIconMapperXBoxOne : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/xboxone/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/xboxone/{btn}.png";
}

public class ControllerIconMapperXBoxSeries : ControllerIconMapperJoypad {
	public override string GetResourcePath(JoyAxis axis) => $"res://addons/controller_icons/assets/xboxseries/{axis}.png";
	public override string GetResourcePath(JoyButton btn) => $"res://addons/controller_icons/assets/xboxseries/{btn}.png";
}
