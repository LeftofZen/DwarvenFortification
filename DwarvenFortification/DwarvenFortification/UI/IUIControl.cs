using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public interface IUIControl
	{
		public Rectangle Bounds { get; set; }
		public void Update(GameTime gameTime, ref object param);
		public void Draw(SpriteBatch sb, ref object param);
	}
}
