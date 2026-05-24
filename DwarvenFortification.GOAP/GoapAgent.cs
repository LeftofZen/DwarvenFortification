namespace DwarvenFortification.GOAP;

public class GoapAgent(string Name = null)
{
	public string Name { get; set; } = Name;

	public GoapWorldState States { get; set; }

	public List<GoapGoal> Goals { get; set; } = [];

	public List<GoapAction> Actions { get; set; } = [];

	public List<GoapSensor> Sensors { get; set; } = [];

	/// <summary>
	/// Precomputed action graph built once over the parametric-expanded <see cref="Actions"/>.
	/// <see cref="GoapPlan.Find"/> lazy-builds this on first plan call and reuses it across every
	/// subsequent call on the same agent so A* never re-scans the full action list per node.
	/// Set to <c>null</c> to force a rebuild (e.g. if <see cref="Actions"/> is replaced).
	/// </summary>
	public GoapActionGraph ActionGraph { get; set; }

	/// <summary>
	/// Per-state-id inclusive bounds. When a state has bounds registered, every engine-driven write
	/// (planner simulation, <see cref="SetState"/>, action effect application) clamps the value into
	/// range. This is what gives operator goals over numeric domains a finite reachable state space.
	/// </summary>
	public Dictionary<string, GoapStateBounds> StateBounds { get; set; } = [];

	/// <summary>
	/// Resolves a parameter binding pattern (e.g. <c>has.item-tag.{tool}</c>) to the set of captured
	/// values discoverable in the fact registry. The captured substring corresponds to the
	/// <c>{paramName}</c> placeholder in the pattern. Returns an empty enumerable when no values match.
	/// When null, the planner skips parametric actions entirely (they will not appear in plans).
	/// </summary>
	public Func<string, IEnumerable<string>> ResolveParameterBindings { get; set; }

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