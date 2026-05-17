using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DwarvenFortification.Input
{
	/// <summary>
	/// Immediate-mode input manager following MonoGame polling conventions.
	/// Call <see cref="Update"/> once per frame (before reading any state) to snapshot
	/// the current and previous keyboard/mouse states.
	/// </summary>
	public sealed class InputManager
	{
		KeyboardState currentKeyboard;
		KeyboardState previousKeyboard;
		MouseState currentMouse;
		MouseState previousMouse;

		// ── Lifecycle ────────────────────────────────────────────────────────────

		/// <summary>
		/// Advances the input snapshot. Must be called exactly once per frame,
		/// before any other InputManager members are read.
		/// </summary>
		public void Update()
		{
			previousKeyboard = currentKeyboard;
			previousMouse    = currentMouse;
			currentKeyboard  = Keyboard.GetState();
			currentMouse     = Mouse.GetState();
		}

		// ── Mouse: position & scroll ─────────────────────────────────────────────

		/// <summary>Current mouse position in screen pixels.</summary>
		public Point MousePosition => currentMouse.Position;

		/// <summary>Mouse position change since the previous frame.</summary>
		public Point MouseDelta => currentMouse.Position - previousMouse.Position;

		/// <summary>
		/// Scroll wheel accumulator change since the previous frame.
		/// Positive values indicate scrolling up/forward.
		/// </summary>
		public int ScrollWheelDelta => currentMouse.ScrollWheelValue - previousMouse.ScrollWheelValue;

		/// <summary>Raw MonoGame scroll wheel accumulator value (not delta).</summary>
		public int ScrollWheelValue => currentMouse.ScrollWheelValue;

		// ── Mouse: button state ───────────────────────────────────────────────────

		/// <summary>True every frame the button is held down.</summary>
		public bool IsMouseButtonDown(MouseButton button)
			=> GetButtonState(currentMouse, button) == ButtonState.Pressed;

		/// <summary>True every frame the button is not held down.</summary>
		public bool IsMouseButtonUp(MouseButton button)
			=> GetButtonState(currentMouse, button) == ButtonState.Released;

		/// <summary>
		/// True for exactly one frame — the frame the button first transitions
		/// from released to pressed.
		/// </summary>
		public bool IsMouseButtonPressed(MouseButton button)
			=> GetButtonState(currentMouse,    button) == ButtonState.Pressed
			&& GetButtonState(previousMouse, button) == ButtonState.Released;

		/// <summary>
		/// True for exactly one frame — the frame the button transitions from
		/// pressed to released (i.e. a click has fully completed).
		/// </summary>
		public bool IsMouseButtonReleased(MouseButton button)
			=> GetButtonState(currentMouse,    button) == ButtonState.Released
			&& GetButtonState(previousMouse, button) == ButtonState.Pressed;

		/// <summary>
		/// True for exactly one frame after a full press-and-release cycle completes.
		/// Equivalent to <see cref="IsMouseButtonReleased"/>.
		/// </summary>
		public bool IsMouseClick(MouseButton button) => IsMouseButtonReleased(button);

		/// <summary>True if any mouse button is currently held down.</summary>
		public bool IsAnyMouseButtonDown()
			=> IsMouseButtonDown(MouseButton.Left)
			|| IsMouseButtonDown(MouseButton.Right)
			|| IsMouseButtonDown(MouseButton.Middle)
			|| IsMouseButtonDown(MouseButton.XButton1)
			|| IsMouseButtonDown(MouseButton.XButton2);

		// ── Keyboard: key state ──────────────────────────────────────────────────

		/// <summary>True every frame the key is held down.</summary>
		public bool IsKeyDown(Keys key) => currentKeyboard.IsKeyDown(key);

		/// <summary>True every frame the key is not held down.</summary>
		public bool IsKeyUp(Keys key) => currentKeyboard.IsKeyUp(key);

		/// <summary>
		/// True for exactly one frame — the frame the key first transitions
		/// from up to down.
		/// </summary>
		public bool IsKeyPressed(Keys key)
			=> currentKeyboard.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);

		/// <summary>
		/// True for exactly one frame — the frame the key transitions from
		/// down to up.
		/// </summary>
		public bool IsKeyReleased(Keys key)
			=> currentKeyboard.IsKeyUp(key) && previousKeyboard.IsKeyDown(key);

		/// <summary>True if any keyboard key is currently held down.</summary>
		public bool IsAnyKeyDown() => currentKeyboard.GetPressedKeys().Length > 0;

		/// <summary>Returns all keys currently held down.</summary>
		public Keys[] GetPressedKeys() => currentKeyboard.GetPressedKeys();

		// ── Raw state access ─────────────────────────────────────────────────────

		/// <summary>Direct access to the raw current-frame keyboard state.</summary>
		public KeyboardState KeyboardState => currentKeyboard;

		/// <summary>Direct access to the raw current-frame mouse state.</summary>
		public MouseState MouseState => currentMouse;

		/// <summary>Direct access to the raw previous-frame keyboard state.</summary>
		public KeyboardState PreviousKeyboardState => previousKeyboard;

		/// <summary>Direct access to the raw previous-frame mouse state.</summary>
		public MouseState PreviousMouseState => previousMouse;

		// ── Private helpers ──────────────────────────────────────────────────────

		static ButtonState GetButtonState(MouseState state, MouseButton button) => button switch
		{
			MouseButton.Left     => state.LeftButton,
			MouseButton.Right    => state.RightButton,
			MouseButton.Middle   => state.MiddleButton,
			MouseButton.XButton1 => state.XButton1,
			MouseButton.XButton2 => state.XButton2,
			_                    => ButtonState.Released,
		};
	}

	/// <summary>Identifies a mouse button for use with <see cref="InputManager"/>.</summary>
	public enum MouseButton
	{
		Left,
		Right,
		Middle,
		XButton1,
		XButton2,
	}
}
