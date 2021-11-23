using System;

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
