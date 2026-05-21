namespace DwarvenFortification.GOAP;

public class GoapCondition()
{
	public string StateId { get; set; }

	public GoapComparison Comparison { get; set; }

	public GoapValue Operand { get; set; }

	public GoapCondition(string StateId, GoapComparison Comparison, GoapValue Operand) : this()
	{
		this.StateId = StateId;
		this.Comparison = Comparison;
		this.Operand = Operand;
	}

	public bool Evaluate(GoapWorldState States)
		=> Comparison.IsMet(States.GetValueOrDefault(StateId), Operand);
}