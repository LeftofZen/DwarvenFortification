using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DwarvenFortification
{
	public class DebugGui
	{
		public ISelectableGameObject BoundObject;

		List<string> details;
		public Rectangle Bounds;

		public DebugGui()
		{
			details = new List<string>();
		}

		// doing this at 60fps probably isn't gonna end well once we start getting complex objects...
		public IEnumerable<string> ReflectObject(ISelectableGameObject obj)
		{
			var concreteType = obj.GetType();

			yield return "=== Properties ===";
			foreach (var prop in concreteType.GetProperties())
			{
				yield return $"{prop.Name}({prop.PropertyType})={prop.GetValue(obj)}";
				if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
				{
					var tempList = new List<string>();
					foreach (var item in (IEnumerable)prop.GetValue(obj, null))
					{
						tempList.Add($"  - {item}");
					}

					foreach (var line in tempList
						.GroupBy(info => info)
						.Select(group => new
						{
							Name = group.Key,
							Count = group.Count()
						})
						.OrderBy(x => x.Name))
					{
						yield return $"{line.Name} x{line.Count}";
					}
				}
			}

			yield return "=== Fields ===";
			foreach (var fld in concreteType.GetFields())
			{
				yield return $"{fld.Name}({fld.FieldType})={fld.GetValue(obj)}";
				if (typeof(IEnumerable).IsAssignableFrom(fld.FieldType))
				{
					var tempList = new List<string>();
					foreach (var item in (IEnumerable)fld.GetValue(obj))
					{
						tempList.Add($"  - {item}");
					}

					foreach (var line in tempList
						.GroupBy(info => info)
						.Select(group => new
						{
							Name = group.Key,
							Count = group.Count()
						})
						.OrderBy(x => x.Name))
					{
						yield return $"{line.Name} x{line.Count}";
					}
				}
			}
		}

		public void Update(GameTime gameTime)
		{
			details.Clear();
			if (BoundObject != null)
			{
				details.AddRange(ReflectObject(BoundObject));
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
