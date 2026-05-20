using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace DwarvenFortification.Camera
{
	/// <summary>
	/// Standard idiomatic MonoGame 2D camera with pan and zoom support.
	/// Pass <see cref="GetTransformMatrix"/> to <c>SpriteBatch.Begin</c> to apply the
	/// camera transform to all world-space rendering.
	/// </summary>
	public sealed class Camera2D
	{
		Viewport viewport;
		Vector2 position;
		float zoom = 1f;

		const float MinZoom = 0.1f;
		const float MaxZoom = 16f;

		public Camera2D(Viewport viewport) => this.viewport = viewport;

		/// <summary>World-space position the camera is centred on.</summary>
		public Vector2 Position
		{
			get => position;
			set => position = value;
		}

		/// <summary>Zoom level. 1 = 100 %, clamped to [0.1, 16].</summary>
		public float Zoom
		{
			get => zoom;
			set => zoom = Math.Clamp(value, MinZoom, MaxZoom);
		}

		/// <summary>
		/// Returns the transform matrix to pass to <c>SpriteBatch.Begin(transformMatrix:)</c>.
		/// Translates and scales world-space content so the camera's <see cref="Position"/>
		/// appears at the centre of the viewport.
		/// </summary>
		public Matrix GetTransformMatrix()
			=> Matrix.CreateTranslation(-position.X, -position.Y, 0f)
			 * Matrix.CreateScale(zoom, zoom, 1f)
			 * Matrix.CreateTranslation(viewport.Width * 0.5f, viewport.Height * 0.5f, 0f);

		/// <summary>Converts a screen-space position to world-space.</summary>
		public Vector2 ScreenToWorld(Vector2 screenPos)
			=> Vector2.Transform(screenPos, Matrix.Invert(GetTransformMatrix()));

		/// <summary>Converts a world-space position to screen-space.</summary>
		public Vector2 WorldToScreen(Vector2 worldPos)
			=> Vector2.Transform(worldPos, GetTransformMatrix());

		/// <summary>
		/// Pans the camera by the given screen-space pixel delta.
		/// Dragging the mouse right moves the world right (camera moves left).
		/// </summary>
		public void Pan(Vector2 screenDelta)
			=> position -= screenDelta / zoom;

		/// <summary>
		/// Multiplies zoom by <paramref name="factor"/> while keeping the world point
		/// under <paramref name="screenPoint"/> fixed in place.
		/// </summary>
		public void ZoomAtScreenPoint(float factor, Vector2 screenPoint)
		{
			var worldBefore = ScreenToWorld(screenPoint);
			Zoom *= factor;
			var worldAfter = ScreenToWorld(screenPoint);
			position += worldBefore - worldAfter;
		}

		/// <summary>Updates the viewport, e.g. after a window resize.</summary>
		public void SetViewport(Viewport newViewport) => viewport = newViewport;
	}
}
