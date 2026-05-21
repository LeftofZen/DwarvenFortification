namespace DwarvenFortification.GOAP;

public class GoapGoal(string Name = null)
{
	public string Id { get; set; }

	public string Name { get; set; } = Name;

	public GoapWorldState Goals { get; set; }

	public Func<GoapAgent, double> Priority { get; set; } = _ => 1;

	public GoapGoal(string Name, GoapWorldState Goals) : this()
	{
		this.Name = Name;
		this.Goals = Goals;
	}

	public bool IsGoalAchieved(GoapWorldState States)
		=> Goals.All(x => x.Value == States[x.Key]); // object comparison here :|
}
