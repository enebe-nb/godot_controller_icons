#if TOOLS
using System.Linq;
using Godot;

public partial class ControllerIconEditorInspector : EditorInspectorPlugin {
	private static readonly string[] handledProperties = ["ActionName", "InputKeys"];

	partial class TexturePreview : MarginContainer {
		private TextureRect nBackground;
		private TextureRect nIconTexture;

		public Texture2D Texture {
			get => nIconTexture.Texture;
			set => nIconTexture.Texture = value;
		}

		public TexturePreview() {
			AddChild(nBackground = new() {
				StretchMode = TextureRect.StretchModeEnum.Tile,
				Texture = EditorInterface.Singleton.GetBaseControl().GetThemeIcon("Checkerboard", "EditorIcons"),
				TextureRepeat = TextureRepeatEnum.Enabled,
				CustomMinimumSize = new Vector2(0, 256)
			});

			AddChild(nIconTexture = new() {
				TextureFilter = TextureFilterEnum.NearestWithMipmaps,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			});
			nIconTexture.SetAnchorsPreset(LayoutPreset.FullRect);
		}
	}

	public override bool _CanHandle (GodotObject obj) {
		return obj is ControllerIconTexture;
	}

	public override void _ParseBegin (GodotObject obj) {
		var preview = new TexturePreview();
		AddCustomControl(preview);
		preview.Texture = (ControllerIconTexture)obj;
		AddPropertyEditorForMultipleProperties("Input", handledProperties, new ControllerIconEditorProperty());
	}

	public override bool _ParseProperty(GodotObject obj, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide) {
		foreach (var prop in handledProperties) if (name == prop) return true;
		return false;
	}
}
#endif
