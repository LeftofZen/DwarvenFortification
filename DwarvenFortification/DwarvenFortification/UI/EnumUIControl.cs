using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public class EnumUIControl<T> where T : Enum
	{
		public Rectangle Bounds { get; set; }
		public Size Size;
		public int Count => Enum.GetNames(typeof(T)).Length;
		Dictionary<T, Color> Colours;

		public EnumUIControl(Rectangle bounds, Dictionary<T, Color> colours = null)
		{
			Bounds = bounds;
			Colours = colours;
		}

		public void Update(GameTime gameTime, ref T param)
		{
			var mouse = Mouse.GetState();
			if (Bounds.Contains(mouse.Position) && mouse.LeftButton == ButtonState.Pressed)
			{
				var offset = mouse.Position - Bounds.Location;
				var index = offset.X / (Bounds.Width / Count);
				param = (T)(object)index;
			}
		}

		public void Draw(SpriteBatch sb, T param)
		{
			var cellWidth = Bounds.Width / Count;
			var cellHeight = Bounds.Height;
			for (var i = 0; i < Count; ++i)
			{
				var colour = ((T)(object)i).CompareTo(param) == 0 ? Color.LightGray : Color.White;
				if (Colours != null)
				{
					colour = Colours[(T)(object)i];
				}
				sb.FillRectangle(Bounds.X + i * cellWidth, Bounds.Y, cellWidth, cellHeight, colour);
				sb.DrawRectangle(Bounds.X + i * cellWidth, Bounds.Y, cellWidth, cellHeight, Color.Black);
				sb.DrawString(
					GameServices.Fonts["Calibri"],
					((T)(object)i).ToString(),
					new Vector2(Bounds.X + (i * cellWidth) + 4, Bounds.Y + 4),
					Color.Black);
			}

			// selected cell
			sb.DrawRectangle(Bounds.X + ((int)(object)param) * cellWidth, Bounds.Y, cellWidth, cellHeight, Color.Red, 3);
		}
	}
}
