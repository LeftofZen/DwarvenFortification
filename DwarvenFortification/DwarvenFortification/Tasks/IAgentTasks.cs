using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public interface IAgentTask
	{
		bool Update();
		void Draw(SpriteBatch sb);
	}
}
