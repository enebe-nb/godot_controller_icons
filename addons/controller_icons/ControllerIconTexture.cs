using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
			foreach(var tex in _textures) tex.Changed -= EmitChanged;
			_textures = value;
			foreach(var tex in _textures) tex.Changed += EmitChanged;
			Clear3DView();
			EmitChanged();
		}
	}

	public ControllerIconTexture() {
		// Register for delayed setup, but try to do it anyway if already ready.
		RenderingServer.FramePostDraw += Setup;
		Setup();
	}

	protected override void Dispose(bool disposing) {
		Clear3DView();
		base.Dispose(disposing);
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
			return;
		}

		// All textures are loaded
		RenderingServer.FramePostDraw -= RequestTexturesLoop;
		_isLoading = false;
		Textures = [.. _textureFiles.Select(file => (Texture2D)ResourceLoader.LoadThreadedGet(file))];
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
		int height = _GetHeight();
		for (int i = 0; i < Textures.Length; ++i) {
			Texture2D tex = Textures[i];
			if (tex == null) continue;

			if (i != 0) {
				// Draw "plus" symbol
				Vector2 offset = new(pos.X, pos.Y + (height - _concatTexture.GetHeight())/2);
				_concatTexture.Draw(toCanvasItem, offset, modulate, transpose);
				pos.X += _concatTexture.GetWidth();
			}

			tex.Draw(toCanvasItem, new(pos.X, pos.Y + (height - tex.GetHeight())/2), modulate, transpose);
			pos.X += tex.GetWidth();
		}
	}

	public override void _DrawRect(Rid toCanvasItem, Rect2 rect, bool tile, Color modulate, bool transpose) {
		Vector2 pos = rect.Position;
		float widthRatio = rect.Size.X / _GetWidth();
		float heightRatio = rect.Size.Y / _GetHeight();

		for (int i = 0; i < Textures.Length; ++i){
			Texture2D tex = Textures[i];
			if (tex == null) continue;

			if (i != 0) {
				// Draw "plus" symbol
				Vector2 concatSize = _concatTexture.GetSize() * new Vector2(widthRatio, heightRatio);
				Vector2 offset = new(pos.X, pos.Y + (rect.Size.Y - concatSize.Y)/2);
				_concatTexture.DrawRect(toCanvasItem, new Rect2(offset, concatSize), tile, modulate, transpose);
				pos.X += concatSize.X;
			}

			Vector2 size = tex.GetSize() * new Vector2(widthRatio, heightRatio);
			Rect2 frame = new (new (pos.X, pos.Y + (rect.Size.Y - size.Y)/2), size);
			tex.DrawRect(toCanvasItem, frame, tile, modulate, transpose);
			pos.X += size.X;
		}
	}

	public override void _DrawRectRegion(Rid toCanvasItem, Rect2 rect, Rect2 srcRect, Color modulate, bool transpose, bool clipUV) {
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
				Rect2 concatDstRect = new(new(pos.X, pos.Y + (rect.Size.Y - concatSize.Y)/2), concatSize);
				_concatTexture.DrawRectRegion(toCanvasItem, concatDstRect, concatSrcRect, modulate, transpose, clipUV);
				pos.X += concatSize.X;
			}

			Vector2 size = tex.GetSize() * new Vector2(widthRatio, heightRatio);

			Vector2 srcRectRatio = new(
				tex.GetWidth() / (float)_GetWidth(),
				tex.GetHeight() / (float)_GetHeight()
			);
			Rect2 texSrcRect = new(srcRect.Position * srcRectRatio, srcRect.Size * srcRectRatio);
			Rect2 texDstRect = new(new(pos.X, pos.Y + (rect.Size.Y - size.Y)/2), size);
			tex.DrawRectRegion(toCanvasItem, texDstRect, texSrcRect, modulate, transpose, clipUV);
			pos.X += size.X;
		}
	}

	// Using Viewport as workaround for 3D
	private SubViewport HelperViewport = null;
	private async void Create3DView() {
		if (HelperViewport != null) return;
		HelperViewport = new SubViewport {
			RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
			RenderTargetClearMode = SubViewport.ClearMode.Once,
			TransparentBg = true,
			Size = new Vector2I(GetWidth(), GetHeight()),
		};

		HelperViewport.AddChild(new TextureRect{ Texture = this });
		ControllerIconManager.Instance.AddChild(HelperViewport);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		EmitChanged();
	}

	private void Clear3DView() {
		if (HelperViewport == null) return;
		ControllerIconManager.Instance.RemoveChild(HelperViewport);
		HelperViewport.QueueFree();
		HelperViewport = null;
	}

	public override Rid _GetRid() {
		// ignore empty results
		if (Textures.Length == 0) return new Rid(null);
		// If there's only one, we don't need to do anything
		if (Textures.Length == 1) return Textures[0].GetRid();

		if (HelperViewport == null) Create3DView();
		return HelperViewport.GetTexture().GetRid();
	}
}
