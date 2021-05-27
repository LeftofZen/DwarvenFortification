using System;
using System.Collections.Generic;
using System.Text;

namespace DwarvenFortification.GOAP
{
	public class WorldObject
	{
		public WorldObject(string name)
		{
			mName = name;
		}

		void ApplyAction(Func<WorldObject, bool> func)
		{

		}

		public override string ToString()
		{ return mName; }

		string mName;
	}
}
