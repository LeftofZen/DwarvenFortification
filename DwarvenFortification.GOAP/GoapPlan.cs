namespace DwarvenFortification.GOAP;

public class GoapPlan
{
	public required GoapAgent Agent { get; set; }

	public required GoapGoal Goal { get; set; }

	public required List<GoapAction> Actions { get; set; }

	public required Dictionary<object, object?> PredictedStates { get; set; }

	public required bool IsBestEffort { get; set; }

	public static GoapPlan? Find(GoapAgent Agent, GoapGoal Goal, GoapPlanSettings? Settings = null)
	{
		Settings ??= new GoapPlanSettings();

		// Update agent states from sensors
		Agent.SenseStates();

		// Create queue that puts cheapest steps first
		PriorityQueue<GoapStep, double> OpenQueue = new();

		// Create first step from initial states
		GoapStep FirstStep = new()
		{
			Previous = null,
			Action = null,
			PredictedStates = new(Agent.States),
			TotalCost = 0,
			TotalSteps = 0,
		};
		OpenQueue.Enqueue(FirstStep, 0);

		// Track cost (including heuristics) to reach each state
		Dictionary<Dictionary<object, object?>, double> StateCosts = new(new DictionaryComparer<object, object?>());
		// Track step that gets the closest
		var BestStep = FirstStep;

		// Repeatedly find next steps
		for (int Iteration = 0; Iteration < Settings.Value.MaxIterations; Iteration++)
		{
			// Get most promising step
			if (!OpenQueue.TryDequeue(out var CurrentStep, out _))
			{
				break;
			}

			// Plan found
			if (Goal.IsReached(CurrentStep.PredictedStates))
			{
				return new GoapPlan()
				{
					Agent = Agent,
					Goal = Goal,
					Actions = CurrentStep.GetActions(),
					PredictedStates = CurrentStep.PredictedStates,
					IsBestEffort = false,
				};
			}

			// Set step as best if it gets the closest to the goal
			if (Goal.IsReachedWithBestEffort(CurrentStep.PredictedStates, BestStep.PredictedStates))
			{
				BestStep = CurrentStep;
			}

			// Ensure total actions (plus next action) is under maximum
			if (CurrentStep.TotalSteps < Settings.Value.MaxActions)
			{
				// Check possible continuations
				foreach (var Action in Agent.GetValidActions(CurrentStep.PredictedStates))
				{
					// Create step to continue most promising step
					GoapStep NextStep = new()
					{
						Previous = CurrentStep,
						Action = Action,
						PredictedStates = Action.PredictStates(CurrentStep.PredictedStates),
						TotalCost = Action.Cost(Agent) + (CurrentStep.Previous?.TotalCost ?? 0),
						TotalSteps = CurrentStep.TotalSteps + 1,
					};

					// Ensure total cost is under maximum
					if (NextStep.TotalCost > Settings.Value.MaxCost)
					{
						continue;
					}

					// Get heuristic distance to goal
					double HeuristicDistance = NextStep.EstimateDistance(Goal);
					if (HeuristicDistance > Settings.Value.MaxDistanceEstimate)
					{
						continue;
					}
					double TotalCost = NextStep.TotalCost + HeuristicDistance;

					// Skip if there's a cheaper path to this state already
					if (StateCosts.TryGetValue(NextStep.PredictedStates, out double CurrentCost) && CurrentCost <= TotalCost)
					{
						continue;
					}
					// Otherwise set as cheapest path
					else
					{
						StateCosts[NextStep.PredictedStates] = TotalCost;
					}

					// Submit step in order of priority
					OpenQueue.Enqueue(NextStep, TotalCost);
				}
			}
		}

		// Plan found that makes some progress toward the goal
		if (Goal.IsReachedWithBestEffort(BestStep.PredictedStates, FirstStep.PredictedStates))
		{
			return new GoapPlan()
			{
				Agent = Agent,
				Goal = Goal,
				Actions = BestStep.GetActions(),
				PredictedStates = BestStep.PredictedStates,
				IsBestEffort = true,
			};
		}

		// No plan found
		return null;
	}

	public async Task<bool> ExecuteAsync(bool CancelOnGoalChange = true)
	{
		foreach (var Action in Actions)
		{
			// Cancel if prioritised goal has changed
			if (CancelOnGoalChange && Goal != Agent.ChooseGoals().FirstOrDefault())
			{
				return false;
			}
			// Cancel if action is invalid
			if (!Agent.IsActionValid(Action))
			{
				return false;
			}
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
