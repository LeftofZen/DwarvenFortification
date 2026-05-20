namespace DwarvenFortification.GOAP;

internal sealed class GoapStep
{
	public required GoapStep? Previous;
	public required GoapAction? Action;
	public required Dictionary<object, object?> PredictedStates;
	public required double TotalCost;
	public required int TotalSteps;

	public List<GoapAction> GetActions()
	{
		Stack<GoapAction> Actions = new(TotalSteps);
		var CurrentStep = this;
		
		while (CurrentStep is not null)
		{
			if (CurrentStep.Action is not null)
			{
				Actions.Push(CurrentStep.Action);
			}
			CurrentStep = CurrentStep.Previous;
		}

		return [.. Actions];
	}

	public double EstimateDistance(GoapGoal Goal)
	{
		// Get distance of resultant states to desired states
		double Distance = 0;
		foreach (var Objective in Goal.Objectives)
		{
			if (Objective.EstimateDistance is null)
			{
				Distance += Objective.IsMet(PredictedStates) ? 0 : 2;
			}
			else
			{
				Distance += Math.Abs(Objective.EstimateDistance(PredictedStates.GetValueOrDefault(Objective.State), Objective.Value.Evaluate(PredictedStates)));
			}
		}

		return Distance;
	}
}