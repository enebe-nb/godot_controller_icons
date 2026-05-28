#if TOOLS
using Godot;

public partial class ControllerIconEditorProperty : EditorProperty {
	private LineEdit viewField = new() {
		Editable = false,
		SelectingEnabled = false,
		SizeFlagsHorizontal = SizeFlags.ExpandFill,
	};

	private Button btnField = new() {
		Icon = EditorInterface.Singleton.GetBaseControl().GetThemeIcon("Edit", "EditorIcons"),
		TooltipText = "Select a input",
	};

	private ControllerIconEditorDialog dialogContainer =
		GD.Load<PackedScene>("res://addons/controller_icons/inspector/ControllerIconEditorDialog.tscn")
		.Instantiate<ControllerIconEditorDialog>();

	public ControllerIconEditorProperty() {
		btnField.Pressed += OnButtonPressed;
		dialogContainer.Confirmed += OnDialogConfirmed;

		var root = new HBoxContainer();
		root.AddChild(viewField);
		root.AddChild(btnField);
		root.AddChild(dialogContainer);
		AddChild(root);
	}

	private void OnButtonPressed() {
		dialogContainer.PopupCentered();
	}

	private void OnDialogConfirmed() {
		if (dialogContainer.TabIndex == 0) {
			EmitChanged("ActionName", dialogContainer.currentActionName);
		} else if (dialogContainer.TabIndex == 1) {
			EmitChanged("InputKeys", dialogContainer.currentInputEvent);
		}
	}

	public override void _UpdateProperty() {
		var icon = GetEditedObject() as ControllerIconTexture;
		viewField.Text = icon.AsText();
		if (viewField.Text == "") viewField.Text = "<Empty>";
	}

}
#endif
