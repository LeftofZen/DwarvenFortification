namespace DwarvenFortification.GOAP;

public class GoapAgent(string Name = null)
{
	public string Name { get; set; } = Name;

	public GoapWorldState States { get; set; }

	public List<GoapGoal> Goals { get; set; } = [];

	public List<GoapAction> Actions { get; set; } = [];

	public List<GoapSensor> Sensors { get; set; } = [];

	/// <summary>
	/// Per-state-id inclusive bounds. When a state has bounds registered, every engine-driven write
	/// (planner simulation, <see cref="SetState"/>, action effect application) clamps the value into
	/// range. This is what gives operator goals over numeric domains a finite reachable state space.
	/// </summary>
	public Dictionary<string, GoapStateBounds> StateBounds { get; set; } = [];

	public GoapValue GetState(string StateId)
		=> States.GetValueOrDefault(StateId);

	public void SetState(string StateId, GoapValue Value)
		=> States[StateId] = StateBounds.TryGetValue(StateId, out var bounds) ? bounds.Clamp(Value) : Value;

	public void SenseStates()
	{
		foreach (var sensor in Sensors)
		{
			SetState(sensor.StateId, sensor.GetValue());
		}
	}

	public IEnumerable<GoapGoal> CurrentGoals()
		=> Goals.OrderByDescending(Goal => Goal.Priority(this));

	public void AddGoal(GoapGoal goal)
		=> Goals.Add(goal);
}