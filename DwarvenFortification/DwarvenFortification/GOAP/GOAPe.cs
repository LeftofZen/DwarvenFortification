using System;

namespace DwarvenFortification.GOAP
{
	public interface IGOAPState
	{
		bool Evaluate();

		void SetAgent(GOAPAgent a);

		GOAPAgent GetAgent();

		string GetName();
	}

	#region Variadic Templates

	public abstract class GOAPStateAbstract : IGOAPState
	{
		protected GOAPStateAbstract(string name, GOAPAgent a = null)
		{
			mName = name;
			mAgent = a;
		}

		public abstract bool Evaluate();

		public void SetAgent(GOAPAgent a)
		{ mAgent = a; }

		public GOAPAgent GetAgent()
		{ return mAgent; }

		public string GetName() { return mName; }

		protected GOAPAgent mAgent = null;
		protected string mName;
	}

	public class GOAPState : GOAPStateAbstract
	{
		public GOAPState(string name, Func<GOAPAgent, bool> func, GOAPAgent a = null) : base(name, a)
		{
			mPrerequisite = func;
		}

		public override bool Evaluate() { return mPrerequisite(mAgent); }

		Func<GOAPAgent, bool> mPrerequisite;
	}

	class GOAPState<T1> : GOAPStateAbstract
	{
		public GOAPState(string name, T1 t1, Func<T1, GOAPAgent, bool> func, GOAPAgent a = null) : base(name, a)
		{
			mT1 = t1;
			mPrerequisite = func;
		}

		public override bool Evaluate() { return mPrerequisite(mT1, mAgent); }

		Func<T1, GOAPAgent, bool> mPrerequisite;
		T1 mT1;
	}

	class GOAPState<T1, T2> : GOAPStateAbstract
	{
		public GOAPState(string name, T1 t1, T2 t2, Func<T1, T2, GOAPAgent, bool> func, GOAPAgent a = null) : base(name, a)
		{
			mT1 = t1;
			mT2 = t2;
			mBehaviour = func;
		}

		public override bool Evaluate() { return mBehaviour(mT1, mT2, mAgent); }

		Func<T1, T2, GOAPAgent, bool> mBehaviour;
		T1 mT1;
		T2 mT2;
	}

	class GOAPState<T1, T2, T3> : GOAPStateAbstract
	{
		public GOAPState(string name, T1 t1, T2 t2, T3 t3, Func<T1, T2, T3, GOAPAgent, bool> func, GOAPAgent a = null) : base(name, a)
		{
			mT1 = t1;
			mT2 = t2;
			mT3 = t3;
			mBehaviour = func;
		}

		public override bool Evaluate() { return mBehaviour(mT1, mT2, mT3, mAgent); }

		Func<T1, T2, T3, GOAPAgent, bool> mBehaviour;
		T1 mT1;
		T2 mT2;
		T3 mT3;
	}

	#endregion
}
