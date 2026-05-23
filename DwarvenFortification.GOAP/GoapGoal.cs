namespace DwarvenFortification.GOAP;

public class GoapGoal(string Name = null)
{
	public string Id { get; set; }

	public string Name { get; set; } = Name;

	/// <summary>
	/// Conditions that must all hold for the goal to be considered achieved. Each entry is a
	/// <see cref="GoapCondition"/>, so goals can use any <see cref="GoapComparison"/> operator
	/// (equality, ordering, inequality), not just equality.
	/// </summary>
	public List<GoapCondition> Goals { get; set; } = [];

	public Func<GoapAgent, double> Priority { get; set; } = _ => 1;

	public GoapGoal(string Name, List<GoapCondition> Goals) : this()
	{
		this.Name = Name;
		this.Goals = Goals ?? [];
	}

	/// <summary>
	/// Convenience constructor: each (key, value) pair is wrapped as an equality condition.
	/// </summary>
	public GoapGoal(string Name, GoapWorldState Goals) : this()
	{
		this.Name = Name;
		this.Goals = Goals is null
			? []
			: [.. Goals.Select(pair => new GoapCondition(pair.Key, GoapComparison.EqualTo, pair.Value))];
	}

	public void Validate()
	{
		if (Goals is null || Goals.Count == 0)
		{
			throw new InvalidOperationException($"GoapGoal '{Name}' must declare at least one desired world state.");
		}
	}

	public bool IsGoalAchieved(GoapWorldState States)
		=> Goals.All(condition => condition.Evaluate(States));
}
