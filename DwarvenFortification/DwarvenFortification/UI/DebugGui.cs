using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DwarvenFortification
{
	public class DebugGui
	{
		public ISelectableGameObject BoundObject;

		List<string> details;
		public Rectangle Bounds
		{
			get => bounds;
			set
			{
				bounds = value;
				boundObjectDetailsBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height - logWindowHeight);
				loggingBounds = new Rectangle(bounds.X, bounds.Height - logWindowHeight, bounds.Width, logWindowHeight);
			}
		}
		Rectangle bounds;

		Rectangle boundObjectDetailsBounds;
		Rectangle loggingBounds;

		const int logWindowHeight = 210;

		public DebugGui()
		{
			details = new List<string>();
		}

		// doing this at 60fps probably isn't gonna end well once we start getting complex objects...
		public IEnumerable<string> ReflectObject(object obj)
		{
			var concreteType = obj.GetType();

			yield return $"=== ObjectType={concreteType} ===";

			var properties = concreteType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			if (properties.Length != 0)
				yield return "--- Properties ---";

			foreach (var prop in properties)
			{
				//yield return $"{prop.Name}({prop.PropertyType})={prop.GetValue(obj)}";
				yield return $" - {prop.Name}={prop.GetValue(obj)}";

				if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
				{
					var tempList = new List<string>();
					foreach (var item in (IEnumerable)prop.GetValue(obj, null))
					{
						tempList.Add($"  * {item}");
					}

					//foreach (var line in tempList
					//	.GroupBy(info => info)
					//	.Select(group => new
					//	{
					//		Name = group.Key,
					//		Count = group.Count()
					//	})
					//	.OrderBy(x => x.Name))
					//{
					//	yield return $"{line.Name} x{line.Count}";
					//}

					foreach (var line in tempList)
					{
						yield return $"{line}";
					}
				}
			}

			var fields = concreteType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			if (fields.Length != 0)
				yield return "--- Fields ---";

			foreach (var fld in fields)
			{
				//yield return $"{fld.Name}({fld.FieldType})={fld.GetValue(obj)}";
				yield return $" - {fld.Name}={fld.GetValue(obj)}";

				if (typeof(IEnumerable).IsAssignableFrom(fld.FieldType) && fld.FieldType != typeof(string))
				{
					var tempList = new List<string>();
					var objFields = (IEnumerable)fld.GetValue(obj);

					foreach (var item in objFields ?? Enumerable.Empty<object>())
					{
						tempList.Add($"  * {item}");
						var x = ReflectObject(item);
						foreach (var xx in x)
						{
							yield return xx;
						}
					}

					//foreach (var line in tempList
					//	.GroupBy(info => info)
					//	.Select(group => new
					//	{
					//		Name = group.Key,
					//		Count = group.Count()
					//	})
					//	.OrderBy(x => x.Name))
					//{
					//	yield return $"{line.Name} x{line.Count}";
					//}

					//foreach (var line in tempList)
					//{
					//	yield return ReflectObject(line);
					//}
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
			// backgrounds
			//sb.FillRectangle(Bounds, Color.LightGray);
			sb.FillRectangle(boundObjectDetailsBounds, Color.PaleVioletRed);
			sb.FillRectangle(loggingBounds, Color.LightSkyBlue);

			// can't call these inside Begin(), they need to be called before, ie we need a separate SpriteBatch here
			//sb.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
			//sb.GraphicsDevice.ScissorRectangle = boundObjectDetailsBounds;
			//sb.GraphicsDevice.ScissorRectangle = loggingBounds;
			//sb.GraphicsDevice.RasterizerState.ScissorTestEnable = false;

			// bound object
			var yOffset = 0;
			foreach (var v in details)
			{
				sb.DrawString(GameServices.Fonts["Calibri"], v, new Vector2(boundObjectDetailsBounds.X + 5, boundObjectDetailsBounds.Y + 5 + yOffset), Color.Black);
				yOffset += 20;
			}

			// logs
			yOffset = 0;
			foreach (var log in GameServices.Logger.Logs.TakeLast(10))
			{
				sb.DrawString(GameServices.Fonts["Calibri"], log.ToString(), new Vector2(loggingBounds.X + 5, loggingBounds.Y + 5 + yOffset), Color.Black);
				yOffset += 20;
			}

			// borders
			sb.DrawRectangle(Bounds, Color.Black, 3);
			sb.DrawRectangle(boundObjectDetailsBounds, Color.Red, 3);
			sb.DrawRectangle(loggingBounds, Color.Blue, 3);
		}
	}
}
