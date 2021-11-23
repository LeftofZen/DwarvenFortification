using System;
using System.Collections.Generic;

namespace DwarvenFortification.GOAP
{
	public class GOAPAction
	{
		public GOAPAction(string name, int cost)
		{ mName = name; mCost = cost; }

		List<IGOAPState> mPrerequisites = new();
		List<IGOAPState> mEffects = new();
		public int mCost = 0;
		public GOAPAgent mAgent = null;
		public WorldObject mTarget = null;

		string mName;

		public void AddPrerequisite(IGOAPState state)
		{
			UpdateAgent(state.GetAgent());

			Console.WriteLine("[AddPrerequisite] \"{0}\"", state.GetName());
			mPrerequisites.Add(state);
		}

		public void AddEffect(IGOAPState state)
		{
			UpdateAgent(state.GetAgent());

			Console.WriteLine("[AddEffect] \"{0}\"", state.GetName());
			mEffects.Add(state);
		}

		public List<IGOAPState> GetEffects()
		{ return mEffects; }

		void UpdateAgent(GOAPAgent a)
		{
			mAgent = a;
		}

		public bool AreAllPrerequisitesSatisfied()
		{
			bool result = true;
			foreach (var v in mPrerequisites)
			{
				// every prere needs an agent
				if (v.GetAgent() == null)
					v.SetAgent(mAgent);

				result &= v.Evaluate();
			}
			return result;
		}

		public override string ToString()
		{
			return mName;
		}
	}
}
