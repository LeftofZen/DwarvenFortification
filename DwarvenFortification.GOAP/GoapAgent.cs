using System.Collections.Concurrent;

namespace DwarvenFortification.GOAP;

public class GoapAgent(string Name = null)
{
	public string Name { get; set; } = Name;

	public required ConcurrentDictionary<object, object?> States { get; set; }

	public required List<GoapGoal> Goals { get; set; }

	public required List<GoapAction> Actions { get; set; }

	public List<GoapSensor> Sensors { get; set; } = [];

	public T GetState<T>(object State)
		=> (T)States.GetValueOrDefault(State)!;

	public dynamic? GetState(object State) 
		=> States.GetValueOrDefault(State);

	public void SetState(object State, GoapValue Value)
		=> States[State] = Value.Evaluate(States);

	public void SenseStates()
	{
		foreach (var Sensor in Sensors)
		{
			SetState(Sensor.State, Sensor.GetValue());
		}
	}

	public IEnumerable<GoapGoal> ChooseGoals()
		=> GetValidGoals().OrderByDescending(Goal => Goal.Priority(this));

	public GoapPlan? FindPlan(GoapGoal Goal, GoapPlanSettings? Settings = null)
		=> GoapPlan.Find(this, Goal, Settings);

	public GoapPlan? FindPlan(GoapPlanSettings? Settings = null)
	{
		// Try find plan for highest priority goals first
		foreach (var Goal in ChooseGoals())
		{
			var Plan = FindPlan(Goal, Settings);
			if (Plan is not null)
			{
				return Plan;
			}
		}

		// No plan found for any goal
		return null;
	}

	public bool IsGoalValid(GoapGoal Goal, IDictionary<object, object?> States)
	{
		if (Goal.IsValidOverride(this) is bool Override)
		{
			return Override;
		}

		if (Goal.IsReached(States))
		{
			return false;
		}

		return true;
	}

	public bool IsGoalValid(GoapGoal Goal)
	{
		SenseStates();
		return IsGoalValid(Goal, States);
	}

	public bool IsActionValid(GoapAction Action, IDictionary<object, object?> States)
	{
		if (Action.IsValidOverride(this) is bool Override)
		{
			return Override;
		}

		if (!Action.Requirements.All(Requirement => Requirement.IsMet(States)))
		{
			return false;
		}

		return true;
	}

	public bool IsActionValid(GoapAction Action)
	{
		SenseStates();
		return IsActionValid(Action, States);
	}

	public IEnumerable<GoapGoal> GetValidGoals(IDictionary<object, object?> States)
		=> Goals.Where(Goal => IsGoalValid(Goal, States));

	public IEnumerable<GoapGoal> GetValidGoals()
	{
		SenseStates();
		return GetValidGoals(States);
	}

	public IEnumerable<GoapAction> GetValidActions(IDictionary<object, object?> States)
		=> Actions.Where(Action => IsActionValid(Action, States));

	public IEnumerable<GoapAction> GetValidActions()
	{
		SenseStates();
		return GetValidActions(States);
	}
}