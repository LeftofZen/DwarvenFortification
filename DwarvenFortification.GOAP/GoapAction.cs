global using GoapWorldState = System.Collections.Generic.IDictionary<string, DwarvenFortification.GOAP.GoapValue>;

namespace DwarvenFortification.GOAP;

public class GoapAction(string Name = null)
{
	public string Name { get; set; } = Name;
	public List<GoapCondition> Conditions { get; set; } = [];
	public List<GoapEffect> Effects { get; set; }

	public Func<GoapAgent, double> Cost { get; set; } = _ => 1;

	public Func<Task<bool>> ExecuteAsync { get; set; } = () => Task.FromResult(true);

	public GoapAction(string Name, List<GoapEffect> Effects) : this()
	{
		this.Name = Name;
		this.Effects = Effects;
	}

	public void UpdateStates(GoapWorldState States)
	{
		foreach (var Effect in Effects)
		{
			States[Effect.StateId] = Effect.Operate(States);
		}
	}
}