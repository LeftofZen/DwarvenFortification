using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.GOAP;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class SimulationGoapIntegrationTests
{
	[Test]
	public void Plan_SelectsLowestCostPlanForGoal()
	{
		var goals = new[]
		{
			new GoalDefinition { Id = "secure-food", Name = "Secure Food", Priority = 10, Effects = ["food.available"] },
		};
		var actions = new[]
		{
			GoapTestSupport.CreateActionDefinition("forage", effects: ["berries.found"], baseCost: 1),
			GoapTestSupport.CreateActionDefinition("harvest-berries", requirements: ["berries.found"], effects: ["food.available"], baseCost: 1),
			GoapTestSupport.CreateActionDefinition("buy-rations", effects: ["food.available"], baseCost: 5),
		};

		var agent = GoapTestSupport.CreateAgent(goals, actions, []);

		var plan = agent.FindPlan();

		Assert.That(plan, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(plan!.Goal.Id, Is.EqualTo("secure-food"));
			Assert.That(plan.Actions.Sum(action => action.Cost(plan.Agent)), Is.EqualTo(2));
			Assert.That(plan.Actions.Select(action => action.GetId()), Is.EqualTo(new[] { "forage", "harvest-berries" }));
		});
	}

	[Test]
	public void Inspect_ReportsMissingFactsForIneligibleGoal()
	{
		var goals = new[]
		{
			new GoalDefinition { Id = "rest", Name = "Rest", Priority = 5, Effects = ["rested"], Requirements = ["bed.available"] },
		};
		var actions = new[]
		{
			GoapTestSupport.CreateActionDefinition("sleep", effects: ["rested"]),
		};

		var agent = GoapTestSupport.CreateAgent(goals, actions, []);

		var goal = agent.Goals.OfType<SimulationGoapGoal>().Single();
		var missingFacts = goal.RequiredFacts.Where(fact => !GoapFactState.IsSatisfied(agent.States, fact)).ToArray();

		Assert.Multiple(() =>
		{
			Assert.That(agent.IsGoalValid(goal, agent.States), Is.False);
			Assert.That(missingFacts, Is.EqualTo(new[] { "bed.available" }));
			Assert.That(agent.FindPlan(), Is.Null);
		});
	}

	[Test]
	public void Plan_RespectsNegatedRequiredFacts()
	{
		var goals = new[]
		{
			new GoalDefinition { Id = "hide", Name = "Hide", Priority = 20, Effects = ["self.hidden"] },
		};
		var actions = new[]
		{
			GoapTestSupport.CreateActionDefinition("hide", requirements: ["!enemy.visible"], effects: ["self.hidden"]),
		};

		var safeAgent = GoapTestSupport.CreateAgent(goals, actions, []);
		var threatenedAgent = GoapTestSupport.CreateAgent(goals, actions, ["enemy.visible"]);

		Assert.Multiple(() =>
		{
			Assert.That(safeAgent.FindPlan(), Is.Not.Null);
			Assert.That(threatenedAgent.FindPlan(), Is.Null);
		});
	}
}
