using System.Diagnostics.CodeAnalysis;

namespace DwarvenFortification.GOAP;

public class GoapAction(string Name = null)
{
	public string Name { get; set; } = Name;

	public required List<GoapEffect> Effects { get; set; }	

	public List<GoapCondition> Requirements { get; set; } = [];
	
	public Func<GoapAgent, double> Cost { get; set; } = _ => 1;	

	public Func<GoapAgent, bool?> IsValidOverride { get; set; } = _ => null;	

	public Func<Task<bool>> ExecuteAsync { get; set; } = () => Task.FromResult(true);

	[SetsRequiredMembers]
	public GoapAction(string Name, List<GoapEffect> Effects) : this()
	{
		this.Name = Name;
		this.Effects = Effects;
	}

	public void UpdateStates(IDictionary<object, object?> States)
	{
		foreach (var Effect in Effects)
		{
			States[Effect.State] = Effect.PredictState(States);
		}
	}

	public Dictionary<object, object?> PredictStates(IDictionary<object, object?> States)
	{
		Dictionary<object, object?> PredictedStates = new(States);
		UpdateStates(PredictedStates);
		return PredictedStates;
	}
}