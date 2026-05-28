using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// [Texture2D] proxy for displaying controller icons
//
// A 2D texture representing a controller icon. The underlying system provides
// a [Texture2D] that may react to changes in the current input method, and also detect the user's controller type.
//
// Specify the [member path] property to setup the desired icon and behavior.
// For a more technical overview, this resource functions as a proxy for any
// node that accepts a [Texture2D], redefining draw commands to use an
// underlying plain [Texture2D], which may be swapped by the remapping system.[br]
//
// This resource works out-of-the box with many default nodes, such as [Sprite2D],
// [Sprite3D], [TextureRect], [RichTextLabel], and others. If you are
// integrating this resource on a custom node, you will need to connect to the
// [signal Resource.changed] signal to properly handle changes to the underlying
// texture. You might also need to force a redraw with methods such as
// [method CanvasItem.queue_redraw].

[Tool]
[GlobalClass, Icon("res://addons/controller_icons/icon.svg")]
public partial class ControllerIconTexture : Texture2D {
	// Used to force a type of controller being shown in the texture
	private ControllerIconMapper _mapper;
	private string _controllerType = "Auto";
	private bool _isAuto = false; // will change on Setup()
	[Export(PropertyHint.EnumSuggestion, "Auto,Keyboard Mouse,PS3 Controller,PS4 Controller,PS5 Controller,Xbox 360 Controller,Xbox One,Xbox Series")]
	public string ControllerType {
		get => _controllerType;
		set {
			_controllerType = value;
			if (ControllerIconManager.Instance == null) return; // not ready yet, wait for Setup()

			if (_isAuto) ControllerIconManager.Instance.InputTypeChanged -= OnInputTypeChanged;
			_isAuto = _controllerType == "Auto";
			if (_isAuto) {
				_mapper = ControllerIconManager.Instance.CurrentMapper;
				ControllerIconManager.Instance.InputTypeChanged += OnInputTypeChanged;
			} else _mapper = ControllerIconSettings.MapperFactory.Create(_controllerType);
			OnStateChanged();
		}
	}

	// Group of variables used to select the reference to a button/key for the icon in this texture
	public enum InputType { INPUT_ACTION, INPUT_EVENT }
	private InputType _referenceType;
	private StringName _actionName;
	private InputEvent _inputKeys;

	 // Value of the action name used as reference
	[Export(PropertyHint.InputName, "show_builtin")]
	public StringName ActionName {
		get => _actionName;
		set {
			if (_actionName == value && _referenceType == InputType.INPUT_ACTION) return;
			_actionName = value;

			if (_actionName != "") _referenceType = InputType.INPUT_ACTION;
			else _inputKeys = null;
			OnStateChanged();
		}
	}

	// Value of the input event used as reference
	[Export(PropertyHint.ResourceType, "InputEventKey,InputEventMouseButton,InputEventJoypadMotion,InputEventJoypadButton")]
	public InputEvent InputKeys {
		get => _inputKeys;
		set {
			// TODO better comparison
			if (_inputKeys == value && _referenceType == InputType.INPUT_EVENT) return;
			_inputKeys = value;

			if (_inputKeys != null) _referenceType = InputType.INPUT_EVENT;
			else _actionName = "";
			OnStateChanged();
		}
	}

	// Custom "plus" texture when action has multiple inputs
	private readonly Texture2D _concatTexture = GD.Load<Texture2D>(ControllerIconSettings.CustomConcatTexture);

	// Texture management variables
	private bool _isLoading = false;
	private string[] _textureFiles = [];
	private Texture2D[] _textures = [];
	private Texture2D[] Textures {
		get => _textures;
		set {
			foreach(var tex in _textures) tex.Changed -= ReloadResource;
			_textures = value;
			foreach(var tex in _textures) tex.Changed += ReloadResource;
		}
	}

	public ControllerIconTexture() {
		// Register for delayed setup, but try to do it anyway if already ready.
		RenderingServer.FramePostDraw += Setup;
		Setup();
	}

	private void Setup() {
		// Keeps checking until it's ready
		if (ControllerIconManager.Instance != null) {
			RenderingServer.FramePostDraw -= Setup;

			// Actually Setup()
			ControllerType = _controllerType; // force _mapper initialization
			// OnStateChanged(); // ControllerType also triggers the event
		}
	}

	// Human readable representation of the input reference
	public string AsText() {
		if (_referenceType == InputType.INPUT_ACTION) return ActionName;
		else return InputKeys?.AsText() ?? "";
	}

	// Request textures to be loaded using threads
	private void RequestTextures() {
		foreach (var file in _textureFiles) ResourceLoader.LoadThreadedRequest(file, "Texture2D");
		if (!_isLoading) {
			_isLoading = true;
			RenderingServer.FramePostDraw += RequestTexturesLoop;
		}

		// try check in the same frame, may be on cache or fail early
		RequestTexturesLoop();
	}

	// Check if all textures have been loaded.
	private void RequestTexturesLoop() {
		foreach(var file in _textureFiles) {
			var status = ResourceLoader.LoadThreadedGetStatus(file);
			if (status == ResourceLoader.ThreadLoadStatus.Loaded) continue;
			if (status == ResourceLoader.ThreadLoadStatus.InProgress) return; // still loading, wait next loop

			// On fail use empty textures
			RenderingServer.FramePostDraw -= RequestTexturesLoop;
			_isLoading = false;
			Textures = [];
			ReloadResource();
			return;
		}

		// All textures are loaded
		RenderingServer.FramePostDraw -= RequestTexturesLoop;
		_isLoading = false;
		Textures = [.. _textureFiles.Select(file => (Texture2D)ResourceLoader.LoadThreadedGet(file))];
		ReloadResource();
	}

	// Mark this texture for redraw
	private void ReloadResource() {
		Dirty = true;
		EmitChanged();
	}

	// Get events regitered in InputMap with the action name
	private InputEvent GetEventFromAction(StringName name) {
		var events = Engine.IsEditorHint()
			? ProjectSettings.GetSetting($"input/{name}").AsGodotDictionary()?.GetValueOrDefault("events").AsGodotArray<InputEvent>()
			: InputMap.ActionGetEvents(name);
		if (events == null) return null;

		foreach(InputEvent evt in events) {
			if (_mapper.IsEventCompatible(evt)) return evt;
		} return null;
	}

	// Properties have changed, reload all dependent resources
	private void OnStateChanged() {
		var evt = _referenceType == InputType.INPUT_ACTION ? GetEventFromAction(ActionName) : InputKeys;
		_textureFiles = _mapper.IsEventCompatible(evt) ? _mapper.GetEventIcons(evt) : ControllerIconManager.Instance.fallbackMapper.GetEventIcons(evt);
		RequestTextures();
	}

	// Called when a the main controller changes
	public void OnInputTypeChanged(ControllerIconMapper mapper) {
		if (!ReferenceEquals(_mapper, mapper)) {
			_mapper = mapper;
			OnStateChanged();
		}
	}

	// -------------- DRAWING FUNCTIONS --------------

	public override bool _HasAlpha() => Textures.Any(t => t?.HasAlpha() ?? false);
	public override bool _IsPixelOpaque(int x, int y) => true; // always true to allow click events

	public override int _GetWidth() {
		int ret = 0;
		foreach(Texture2D texture in Textures) ret += texture?.GetWidth() ?? 0;
		if (Textures.Length > 1) ret += _concatTexture.GetWidth() * (Textures.Length-1);

		// If ret is 0, return a size of 2 to prevent triggering engine checks
		// for null sizes. The correct size will be set at a later frame.
		return ret > 0 ? ret : 2;
	}

	public override int _GetHeight() {
		if (Textures.Length == 0) return 2;

		int ret = Textures.Max(texture => texture?.GetHeight() ?? 0);
		if (Textures.Length > 1) {
			var concatHeight = _concatTexture.GetHeight();
			if (concatHeight > ret) ret = concatHeight;
		}

		// If ret is 0, return a size of 2 to prevent triggering engine checks
		// for null sizes. The correct size will be set at a later frame.
		return ret > 0 ? ret : 2;
	}


	public override void _Draw(Rid toCanvasItem, Vector2 pos, Color modulate, bool transpose) {
		GD.Print("Draw1!");
		for (int i = 0; i < Textures.Length; ++i) {
			Texture2D tex = Textures[i];
			if (tex == null) continue;

			if (i != 0) {
				// Draw "plus" symbol
				_concatTexture.Draw(toCanvasItem, pos, modulate, transpose);
				pos.X += _concatTexture.GetWidth();
				// TODO vertical alignment
			}

			// position += new Vector2(TextSize.X, 0);
			tex.Draw(toCanvasItem, pos, modulate, transpose);
			pos.X += tex.GetWidth();
		}
	}

	public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose) {
		GD.Print("Draw2!");
		Vector2 pos = rect.Position;
		float widthRatio = rect.Size.X / _GetWidth();
		float heightRatio = rect.Size.Y / _GetHeight();

		for (int i = 0; i < Textures.Length; ++i){
			Texture2D tex = Textures[i];
			if (tex == null) continue;

			if (i != 0) {
				// Draw "plus" symbol
				Vector2 concatSize = _concatTexture.GetSize() * new Vector2(widthRatio, heightRatio);
				_concatTexture.DrawRect(toCanvasItem, new Rect2(pos, concatSize), tile, modulate, transpose);
				pos.X += concatSize.X;
				// TODO vertical alignment
				// Vector2 font_position = new Vector2(
				// 	position.X,
				// 	position.Y + (GetHeight() - TextSize.Y) / 2.0f
				// );
			}

			Vector2 size = tex.GetSize() * new Vector2(widthRatio, heightRatio);
			tex.DrawRect(toCanvasItem, new Rect2(pos, size), tile, modulate, transpose);
			pos.X += size.X;
		}
	}

	public override void _DrawRectRegion(Rid toCanvasItem, Rect2 rect, Rect2 srcRect, Color modulate, bool transpose, bool clipUV) {
		GD.Print("Draw3!");
		Vector2 pos = rect.Position;
		float widthRatio = rect.Size.X / _GetWidth();
		float heightRatio = rect.Size.Y / _GetHeight();

		for (int i = 0; i < Textures.Length; ++i) {
			Texture2D tex = Textures[i];
			if (tex == null) continue;

			if (i != 0) {
				// Draw "plus" symbol
				Vector2 concatSize = _concatTexture.GetSize() * new Vector2(widthRatio, heightRatio);
				Vector2 concatRectRatio = new(
					tex.GetWidth() / (float)_GetWidth(),
					tex.GetHeight() / (float)_GetHeight()
				);
				Rect2 concatSrcRect = new(srcRect.Position * concatRectRatio, srcRect.Size * concatRectRatio);
				_concatTexture.DrawRectRegion(toCanvasItem, new Rect2(pos, concatSize), concatSrcRect, modulate, transpose, clipUV);
				pos.X += concatSize.X;
				// TODO vertical alignment
				// Vector2 fontPosition = new(
				// 	position.X + (TextSize.X * widthRatio) / 2 - (TextSize.X / 2),
				// 	position.Y + (rect.Size.Y - TextSize.Y) / 2.0f
				// );
			}

			Vector2 size = tex.GetSize() * new Vector2(widthRatio, heightRatio);

			Vector2 srcRectRatio = new(
				tex.GetWidth() / (float)_GetWidth(),
				tex.GetHeight() / (float)_GetHeight()
			);
			Rect2 texSrcRect = new(srcRect.Position * srcRectRatio, srcRect.Size * srcRectRatio);

			tex.DrawRectRegion(toCanvasItem, new Rect2(pos, size), texSrcRect, modulate, transpose, clipUV);
			pos.X += size.X;
		}
	}

	private SubViewport HelperViewport;
	private bool IsStitchingTexture = false;
	private async void StitchTexture() {
		GD.Print("Draw4!");
		if (Textures.Length == 0) return;
		IsStitchingTexture = true;
		Image fontImage = null;
		if (Textures.Length > 1) {
			// Generate a viewport to draw the text
			HelperViewport = new SubViewport {
				// FIXME: We need a 3px margin for some reason
				// Size = (Vector2I)(TextSize + new Vector2(3, 0)),

				RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
				RenderTargetClearMode = SubViewport.ClearMode.Once,
				TransparentBg = true
			};

			// Label label = new() {
			// 	LabelSettings = LabelSettings,
			// 	Text = "+",
			// 	Position = Vector2.Zero
			// };

			// HelperViewport.AddChild(label);

			ControllerIconManager.Instance.AddChild(HelperViewport);
			//await RenderingServer.FramePostDraw;
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			fontImage = HelperViewport.GetTexture().GetImage();

			ControllerIconManager.Instance.RemoveChild(HelperViewport);
			HelperViewport.Free();
		}

		Vector2I position = new(0, 0);

		Image img = new();
		for (int i = 0; i < Textures.Length; ++i) {
			if (Textures[i] == null) continue;

			if (i != 0) {
				// Draw text char '+'
				Rect2I region = fontImage.GetUsedRect();
				Vector2I fontPosition = new(
					position.X,
					position.Y + (GetHeight() - region.Size.Y) / 2
				);

				img.BlitRect( fontImage, region, fontPosition );
				position += new Vector2I( region.Size.X, 0 );
			}

			Image textureRaw = Textures[i].GetImage();
			textureRaw.Decompress();
			img ??= Image.CreateEmpty(_GetWidth(), _GetHeight(), true, textureRaw.GetFormat());
			img.BlitRect(textureRaw, new Rect2I(0, 0, textureRaw.GetWidth(), textureRaw.GetHeight()), position);

			position += new Vector2I( textureRaw.GetWidth(), 0 );
		}

		IsStitchingTexture = false;

		Dirty = false;
		Texture3D = ImageTexture.CreateFromImage(img);
		EmitChanged();
	}

	// This is necessary for 3D sprites, as the texture is assigned to a material, and not drawn directly.
	// For multi prompts, we need to generate a texture
	private bool Dirty = true;

	private Texture Texture3D;
	public override Rid _GetRid() {
		if (Dirty) {
			if (!IsStitchingTexture) StitchTexture();
				// FIXME: Function may await, but because this is an internal engine call, we can't do anything about it.
				// This results in a one-frame white texture being displayed, which is not ideal. Investigate later.

			if (IsStitchingTexture) return new Rid(null);
			else return new Rid(null);
		}
		return Textures.Length > 0 ? Texture3D.GetRid() : new Rid(null);
	}
}
