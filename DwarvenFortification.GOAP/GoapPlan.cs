namespace DwarvenFortification.GOAP;

public class GoapPlan
{
	public GoapAgent Agent { get; set; }

	public GoapGoal Goal { get; set; }

	public List<GoapAction> Actions { get; set; }

	public static GoapPlan? Find(GoapAgent Agent, GoapGoal Goal)
	{
		// Update agent states from sensors
		Agent.SenseStates();

		// todo: Create and return plan

		// otherwise: No plan found
		return null;
	}

	// not sure why this is here - agent should execute plan
	public async Task<bool> ExecuteAsync(bool CancelOnGoalChange = true)
	{
		foreach (var Action in Actions)
		{
			// Cancel if prioritised goal has changed
			if (CancelOnGoalChange && Goal != Agent.CurrentGoals().FirstOrDefault())
			{
				return false;
			}
			// Cancel if action is invalid
			//if (!Agent.IsActionValid(Action))
			//{
			//	return false;
			//}
			// Execute action and cancel if failed
			if (!await Action.ExecuteAsync())
			{
				return false;
			}
			// Apply the action's effects
			Action.UpdateStates(Agent.States);
			// Update states from sensors
			Agent.SenseStates();
		}
		return true;
	}

	public bool Execute(bool CancelOnGoalChange = true) 
		=> ExecuteAsync(CancelOnGoalChange).GetAwaiter().GetResult();
}
