using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public class GOAPAgent : WorldObject
	{
		public GOAPAgent(string name) : base(name)
		{
		}

		public bool AddObjectToInventory(WorldObject wo)
		{ mInventory[wo] = true; return true; }

		public bool RemoveObjectFromInventory(WorldObject wo)
		{ mInventory[wo] = false; return true; }

		public bool HasWorldObject(WorldObject wo)
		{
			if (mInventory.ContainsKey(wo))
			{
				return mInventory[wo];
			}
			return false;
		}

		public void AddAction(GOAPAction a)
		{
			if (a.mAgent == null)
				a.mAgent = this;

			// for all preconditions and effects, set agent to this one

			mActions.Add(a);

			Console.WriteLine("[AddAction] \"{0}\"", a);
		}

		Dictionary<WorldObject, bool> mInventory = new Dictionary<WorldObject, bool>();
		List<GOAPAction> mActions = new List<GOAPAction>(); // should be prio queue

		public GOAPAction FindNextAction()
		{
			if (mActions.Count() == 0)
				return null;

			// get lowest energy, valid task
			GOAPAction currentAction = mActions[0];
			GOAPAction replenish = mActions[0];

			foreach (GOAPAction a in mActions)
			{
				if (a.AreAllPrerequisitesSatisfied() && a.mCost < currentAction.mCost)
				{
					if (a.mCost > 0)
						currentAction = a;
					else
						replenish = a;
				}
			}

			if (currentAction == null)
			{
				Console.WriteLine("[Agent] [RunPlanner] Info=\"No available tasks\"");
				return null;
			}

			Console.WriteLine("[Agent] [RunPlanner] ActionSelected={0}", currentAction);

			// if no energy try to find a task that replenishes energy
			if ((mEnergy - currentAction.mCost) < 0)
			{
				Console.WriteLine(
					"[Agent] [RunPlanner] Info=\"No energy for selected task. Running replenish task\" CurrentEnergy={0} RequiredEnergy={1} ReplenishEnergy={2}",
					mEnergy, currentAction.mCost, replenish.mCost);

				currentAction = replenish;
			}

			return currentAction;
		}

		public bool RunPlanner()
		{
			GOAPAction currentAction = FindNextAction();

			// we can't find any valid actions, exit completely (in a real game we would 'idle').
			if (currentAction == null)
				return false;

			// execute task
			foreach (var v in currentAction.GetEffects())
			{
				if (v.GetAgent() == null)
					v.SetAgent(this);

				// run the effect on the owner
				v.Evaluate();
			}

			mEnergy -= currentAction.mCost;

			return true;
		}

		public void PrintInventory()
		{
			Console.WriteLine("[PrintInventory]");

			foreach (var kv in mInventory)
			{
				Console.WriteLine("{0} - {1}", this, kv);
			}
		}

		public override string ToString()
		{
			return string.Format("[ToString] Base={0} Energy={1}", base.ToString(), mEnergy);
		}

		public int mEnergy = 20;
	}
}
