using Godot;
using System;

[Tool]
public partial class ControllerIconPlugin : EditorPlugin {
	private ControllerIconEditorInspector inspectorPlugin;

	public override void _EnablePlugin() {
		AddAutoloadSingleton("ControllerIcons", "res://addons/controller_icons/ControllerIconManager.cs");
	}

	public override void _DisablePlugin() {
		RemoveAutoloadSingleton("ControllerIcons");
	}

	public override void _EnterTree() {
		inspectorPlugin = new();
		AddInspectorPlugin(inspectorPlugin);
	}

	public override void _ExitTree() {
		RemoveInspectorPlugin(inspectorPlugin);
	}

	public override Texture2D _GetPluginIcon() {
		return ResourceLoader.Load<Texture2D>("res://addons/controller_icons/icon.svg");
	}
}
