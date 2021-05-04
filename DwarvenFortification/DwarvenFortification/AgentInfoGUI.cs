using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DwarvenFortification
{
	public class AgentInfoGUI
	{
		public Agent BoundAgent { set; private get; }

		List<string> details;
		public Rectangle Bounds;

		public AgentInfoGUI()
		{
			details = new List<string>();
		}

		public void Update(GameTime gameTime)
		{
			details.Clear();
			if (BoundAgent != null)
			{
				details.Add($"+ Position={BoundAgent.Position.ToString()}");
				details.Add($"+ CurrentCell={BoundAgent.CurrentCell.ToString()}");
				details.Add($"+ Inventory");
				foreach (var v in BoundAgent.Inventory)
					details.Add($"  - {v}");
			}
		}

		public void Draw(SpriteBatch sb)
		{
			sb.FillRectangle(Bounds, Color.LightGray);

			var offset = 0;
			foreach (var v in details)
			{
				sb.DrawString(GameServices.Fonts["Calibri"], v, new Vector2(Bounds.X + 5, Bounds.Y + 5 + offset) , Color.Black);
				offset += 20;
			}

			sb.DrawRectangle(Bounds, Color.Black, 3);
		}
	}
}
