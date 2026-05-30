using System;
using Godot;

public static class ControllerIconSettings {
	// initialize and set plugin property
	private static void PrepareSetting(string name, Variant val, PropertyHint hint = PropertyHint.None, string hint_string = "", bool isBasic = true) {
		if (!ProjectSettings.HasSetting(name)) ProjectSettings.SetSetting(name, val);
		if (hint != PropertyHint.None) {
			ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary {
				{ "name", name },
				{ "type", (int)val.VariantType },
				{ "hint", (int)hint },
				{ "hint_string", hint_string },
			});
		}
		ProjectSettings.SetInitialValue(name, val);
		ProjectSettings.SetAsBasic(name, isBasic);
	}

	private const string defaultMapperFactory	= "res://addons/controller_icons/ControllerIconMapperFactory.cs";
	private const string defaultJoypadFallback	= "Xbox 360 Controller";
	private const float  defaultJoypadDeadzone	= 0.5f;
	private const bool   defaultMouseRemap		= true;
	private const int    defaultMouseMinMove	= 200;
	private const string defaultConcatTexture	= "res://addons/controller_icons/assets/concat.png";

	public static void PrepareAllSettings() {
		PrepareSetting("controller_icons/general/mapper_factory", defaultMapperFactory, PropertyHint.File, "*.cs");
		PrepareSetting("controller_icons/general/joypad_fallback", defaultJoypadFallback);
		PrepareSetting("controller_icons/general/joypad_deadzone", defaultJoypadDeadzone, PropertyHint.Range, "0.0,1.0");
		PrepareSetting("controller_icons/general/allow_mouse_remap", defaultMouseRemap);
		PrepareSetting("controller_icons/general/mouse_min_movement", defaultMouseMinMove, PropertyHint.Range, "0,10000");
		PrepareSetting("controller_icons/general/custom_concat_texture", defaultConcatTexture, PropertyHint.File, "*.png");
	}

	private static ControllerIconMapperFactory _mapperFactory;
	// Custom mapper factory to find resource paths
	public static ControllerIconMapperFactory MapperFactory {
		get {
			_mapperFactory ??= GD.Load<CSharpScript>(ProjectSettings.GetSetting("controller_icons/general/mapper_factory", defaultMapperFactory).As<string>()).New().As<ControllerIconMapperFactory>();
			return _mapperFactory;
		}
	}

	// Controller type to fallback to if automatic
	// controller detection fails
	public static string JoypadFallback => ProjectSettings.GetSetting("controller_icons/general/joypad_fallback", defaultJoypadFallback).As<string>();

	// Controller deadzone for triggering an icon remap when input
	// is analogic (movement sticks or triggers)
	public static float JoypadDeadzone => ProjectSettings.GetSetting("controller_icons/general/joypad_deadzone", defaultJoypadDeadzone).As<float>();

	// Allow mouse movement to trigger an icon remap
	public static bool AllowMouseRemap => ProjectSettings.GetSetting("controller_icons/general/allow_mouse_remap", defaultMouseRemap).As<bool>();

	// Minimum mouse "instantaneous" movement for
	// triggering an icon remap
	public static int MouseMinMovement => ProjectSettings.GetSetting("controller_icons/general/mouse_min_movement", defaultMouseMinMove).As<int>();

	// Custom LabelSettings. If unset, uses engine default settings.
	public static string CustomConcatTexture => ProjectSettings.GetSetting("controller_icons/general/custom_concat_texture", defaultConcatTexture).As<string>();
}

