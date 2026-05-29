using System.Collections.Generic;
using Godot;

public partial class IconRemapper : Control {
	private Control[] nodes;
	//private List<string> baseNames;

	public override void _Ready() {
		nodes = [
			GetNode<Control>("%A"), GetNode<Control>("%B"), GetNode<Control>("%X"), GetNode<Control>("%Y"),
			GetNode<Control>("%LB"), GetNode<Control>("%RB"), GetNode<Control>("%LT"), GetNode<Control>("%RT"),
			GetNode<Control>("%L_Stick_Click"), GetNode<Control>("%R_Stick_Click"), GetNode<Control>("%LStick"), GetNode<Control>("%RStick"),
			GetNode<Control>("%Select"), GetNode<Control>("%Start"),// GetNode<Control>("%DPAD"),
			GetNode<Control>("%Home"), GetNode<Control>("%Share"),
			GetNode<Control>("%DPAD_Up"), GetNode<Control>("%DPAD_Down"), GetNode<Control>("%DPAD_Left"), GetNode<Control>("%DPAD_Right"),
		];

		// foreach(var child in nodes) {
		// 	var icon = (ControllerIconTexture)child.GetChild<TextureRect>(0).Texture;
		// 	baseNames.Add(icon.ActionName);
		// }
	}

	public void SetControllerType(StringName name) {
		foreach(var child in nodes) {
			var icon = (ControllerIconTexture)child.GetChild<TextureRect>(0).Texture;
			icon.ControllerType = name;
		}
	}
}
