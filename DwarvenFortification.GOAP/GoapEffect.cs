namespace DwarvenFortification.GOAP;

//public class GoapState
//{
//	public string Id { get; set; }
//	public GoapValue Value { get; set; }
//}

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
	public bool Operate(GoapWorldState States)
		=> Operation.Operate(States.GetValueOrDefault(StateId), Operand);
}