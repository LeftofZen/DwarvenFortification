using System.Diagnostics.CodeAnalysis;

namespace DwarvenFortification.GOAP;

public class GoapEffect()
{

	public required object State { get; set; }

	public required GoapOperation Operation { get; set; }

	public required GoapValue Value { get; set; }

	[SetsRequiredMembers]
	public GoapEffect(object State, GoapOperation Operation, GoapValue Value) : this()
	{
		this.State = State;
		this.Operation = Operation;
		this.Value = Value;
	}

	public object? PredictState(IDictionary<object, object?> States)
		=> Operation.Operate(States[State], Value.Evaluate(States));
}