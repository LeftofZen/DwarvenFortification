namespace DwarvenFortification.GOAP;

public class GoapEffect()
{
	public string StateId { get; set; }

	public GoapOperation Operation { get; set; }

	public GoapValue Operand { get; set; }

	public GoapEffect(string StateId, GoapOperation Operation, GoapValue Operand) : this()
	{
		this.StateId = StateId;
		this.Operation = Operation;
		this.Operand = Operand;
	}

	// applies Value to State using Operation
	public GoapValue Operate(GoapWorldState States)
		=> Operation.Operate(States.GetValueOrDefault(StateId), Operand);

	/// <summary>
	/// Computes the post-effect value via <see cref="Operate"/>, clamps it through <paramref name="Bounds"/>
	/// (when one is registered for <see cref="StateId"/>), and writes it back to <paramref name="States"/>.
	/// </summary>
	public void ApplyTo(GoapWorldState States, IDictionary<string, GoapStateBounds>? Bounds = null)
	{
		var value = Operate(States);
		if (Bounds is not null && Bounds.TryGetValue(StateId, out var bounds))
		{
			value = bounds.Clamp(value);
		}
		States[StateId] = value;
	}
}