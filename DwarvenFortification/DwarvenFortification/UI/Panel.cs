using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DwarvenFortification.UI
{
	public interface IGameUIComponent
	{
		void Update(GameTime gameTime);
		void Draw(SpriteBatch sb);
	}

	public abstract class Control : IGameUIComponent
	{
		public Rectangle Bounds { get; set; }

		public Color Background { get; set; }

		public void Update(GameTime gameTime)
		{
			throw new NotImplementedException();
		}

		public void Draw(SpriteBatch sb)
		{
			//sb.FillRectangle();
		}
	}

	public class Button : Control
	{
		public string Text;

		public void Update(GameTime gameTime)
		{
			throw new NotImplementedException();
		}

		public void Draw(SpriteBatch sb)
		{
			throw new NotImplementedException();
		}
	}

	public class Panel : Control
	{

	}
}
