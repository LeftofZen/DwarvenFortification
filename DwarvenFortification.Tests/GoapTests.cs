using DwarvenFortification.Actions;
using DwarvenFortification.GOAP;

namespace DwarvenFortification.Tests;

/// <summary>
/// Direct exercises of the GOAP library API: <see cref="GoapAgent"/>, <see cref="GoapAction"/>,
/// <see cref="GoapCondition"/>, <see cref="GoapEffect"/>, <see cref="GoapGoal"/>,
/// <see cref="GoapSensor"/>, <see cref="GoapValue"/>, and <see cref="GoapPlan"/>.
/// No simulation glue.
/// </summary>
[TestFixture]
public sealed class GoapTests
{
	static Dictionary<string, GoapValue> State(params (string Key, GoapValue Value)[] entries)
	{
		var dict = new Dictionary<string, GoapValue>(StringComparer.Ordinal);
		foreach (var (key, value) in entries)
		{
			dict[key] = value;
		}

		return dict;
	}

	static GoapAgent Agent(
		Dictionary<string, GoapValue> states,
		List<GoapAction> actions,
		List<GoapGoal>? goals = null,
		List<GoapSensor>? sensors = null,
		Dictionary<string, GoapStateBounds>? bounds = null)
		=> new("test")
		{
			States = states,
			Actions = actions,
			Goals = goals ?? [],
			Sensors = sensors ?? [],
			StateBounds = bounds ?? [],
		};

	static GoapAction Action(
		string name,
		List<GoapCondition>? conditions = null,
		GoapEffect? effect = null,
		double cost = 1d,
		Func<GoapActionResult>? run = null)
		=> new(name, effect)
		{
			Conditions = conditions ?? [],
			Cost = _ => cost,
			Run = run ?? (() => GoapActionResult.Success),
		};

	static GoapGoal Goal(string id, params (string Key, GoapValue Value)[] desired)
		=> new("g_" + id, State(desired)) { Id = id };

	static GoapGoal GoalConditions(string id, params GoapCondition[] conditions)
		=> new("g_" + id, [.. conditions]) { Id = id };

	// ---------------------------------------------------------------- planning

	[Test]
	public void Find_ReturnsEmptyPlan_WhenGoalAlreadySatisfied()
	{
		var agent = Agent(
			State(("rested", true)),
			actions: [Action("sleep", effect: new("rested", GoapOperation.SetTo, true))]);
		var goal = Goal("rest", ("rested", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions, Is.Empty);
		Assert.That(plan.Goal, Is.SameAs(goal));
		Assert.That(plan.Agent, Is.SameAs(agent));
	}

	[Test]
	public void Find_ReturnsNull_WhenNoActionChainAchievesGoal()
	{
		var agent = Agent(
			State(("rested", false)),
			actions: [Action("forage", effect: new("food", GoapOperation.SetTo, true))]);
		var goal = Goal("rest", ("rested", true));

		Assert.That(GoapPlan.Find(agent, goal), Is.Null);
	}

	[Test]
	public void Find_FindsSingleActionPlan()
	{
		var agent = Agent(
			State(("rested", false)),
			actions: [Action("sleep", effect: new("rested", GoapOperation.SetTo, true))]);
		var goal = Goal("rest", ("rested", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "sleep" }));
	}

	[Test]
	public void Find_ChainsActionsThroughPreconditions()
	{
		// chop-tree -> build-bed -> sleep
		var agent = Agent(
			State(
				("logs", false),
				("bed", false),
				("rested", false)),
			actions:
			[
				Action("chop-tree", effect: new("logs", GoapOperation.SetTo, true)),
				Action("build-bed",
					conditions: [new("logs", GoapComparison.EqualTo, true)],
					effect: new("bed", GoapOperation.SetTo, true)),
				Action("sleep",
					conditions: [new("bed", GoapComparison.EqualTo, true)],
					effect: new("rested", GoapOperation.SetTo, true)),
			]);
		var goal = Goal("rest", ("rested", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "chop-tree", "build-bed", "sleep" }));
	}

	[Test]
	public void Find_PrefersLowerCostPath_AmongAlternatives()
	{
		// Two routes to food: forage+harvest (cost 2) vs buy-rations (cost 5).
		var agent = Agent(
			State(("berries", false), ("food", false)),
			actions:
			[
				Action("forage", effect: new("berries", GoapOperation.SetTo, true), cost: 1),
				Action("harvest",
					conditions: [new("berries", GoapComparison.EqualTo, true)],
					effect: new("food", GoapOperation.SetTo, true),
					cost: 1),
				Action("buy-rations", effect: new("food", GoapOperation.SetTo, true), cost: 5),
			]);
		var goal = Goal("eat", ("food", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "forage", "harvest" }));
		Assert.That(plan.Actions.Sum(a => a.Cost(agent)), Is.EqualTo(2d));
	}

	[Test]
	public void Find_RespectsNotEqualToCondition()
	{
		var actions = new List<GoapAction>
		{
			Action("hide",
				conditions: [new("enemy.visible", GoapComparison.NotEqualTo, true)],
				effect: new("self.hidden", GoapOperation.SetTo, true)),
		};
		var goal = Goal("be-hidden", ("self.hidden", true));

		var safe = Agent(State(("enemy.visible", false), ("self.hidden", false)), actions);
		var threatened = Agent(State(("enemy.visible", true), ("self.hidden", false)), actions);

		Assert.Multiple(() =>
		{
			Assert.That(GoapPlan.Find(safe, goal), Is.Not.Null, "should plan when enemy not visible");
			Assert.That(GoapPlan.Find(threatened, goal), Is.Null, "should fail when enemy visible");
		});
	}

	[Test]
	public void Find_HandlesGreaterThanOrEqualCondition_WithIncreaseByEffect()
	{
		// `wood += 1` action; gate uses wood >= 3, then build-house sets house = true.
		var agent = Agent(
			State(("wood", 0), ("house", false)),
			actions:
			[
				Action("chop", effect: new("wood", GoapOperation.IncreaseBy, 1)),
				Action("build",
					conditions: [new("wood", GoapComparison.GreaterThanOrEqualTo, 3)],
					effect: new("house", GoapOperation.SetTo, true)),
			]);
		var goal = Goal("shelter", ("house", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "chop", "chop", "chop", "build" }));
	}

	[Test]
	public void Find_CostCallback_SeesSimulatedMidPlanState()
	{
		// Two routes to the goal:
		//   A) `expensive-direct`                                            cost 50
		//   B) `prep` (cost 5) -> `cheap-after-prep`                         cost 5 + (1 if prep done else 100)
		//
		// Without state-aware costing, `cheap-after-prep` would always be sampled against the
		// real pre-plan state where `prepped == false`, so route B looks like 5 + 100 = 105 and
		// the planner picks the 50-cost direct action.
		//
		// With state-aware costing, the planner samples `cheap-after-prep` after `prep` is
		// simulated, sees `prepped == true`, costs it at 1, and route B (= 6) wins.
		var prep = Action("prep",
			effect: new("prepped", GoapOperation.SetTo, true),
			cost: 5d);
		var cheapAfterPrep = new GoapAction("cheap-after-prep", new GoapEffect("goal", GoapOperation.SetTo, true))
		{
			Conditions = [],
			// Declared cost-relevance: the planner's action graph follows this edge during backward
			// closure so `prep` (the producer of `prepped`) is included in the connected subgraph
			// even though no precondition gates this action on it.
			CostStateIds = ["prepped"],
			Cost = a => a.States.TryGetValue("prepped", out var v) && v.Value is bool b && b ? 1d : 100d,
			Run = () => GoapActionResult.Success,
		};
		var expensiveDirect = Action("expensive-direct",
			effect: new("goal", GoapOperation.SetTo, true),
			cost: 50d);

		var agent = Agent(
			State(("prepped", false), ("goal", false)),
			actions: [prep, cheapAfterPrep, expensiveDirect]);
		var goal = Goal("finish", ("goal", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "prep", "cheap-after-prep" }),
			"planner must pick prep+cheap-after-prep (cost 6) over expensive-direct (cost 50) " +
			"by sampling cheap-after-prep against the simulated post-prep state");
	}

	[Test]
	public void Find_RestoresAgentStateAfterPlanning()
	{
		var preStates = State(("a", false));
		var agent = Agent(preStates, actions:
		[
			Action("flip", effect: new("a", GoapOperation.SetTo, true)),
		]);
		var goal = Goal("a-true", ("a", true));

		_ = GoapPlan.Find(agent, goal);

		Assert.That(agent.States, Is.SameAs(preStates), "Find must put the original state dict back");
		Assert.That(agent.States["a"].Value, Is.EqualTo(false), "real state must not be mutated by planning");
	}

	[Test]
	public void Find_ExploresStateSpace_WithoutInfiniteLoopOnNoOpActions()
	{
		// Action whose effects don't change the canonical state should not cause non-termination.
		var agent = Agent(
			State(("x", 1)),
			actions:
			[
				Action("reset-x", effect: new("x", GoapOperation.SetTo, 1)),
				Action("set-x-2", effect: new("x", GoapOperation.SetTo, 2)),
			]);
		var goal = Goal("x-is-2", ("x", 2));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "set-x-2" }));
	}

	// ---------------------------------------------------------- value semantics

	[Test]
	public void GoapValue_StructuralEquality_OnPrimitives()
	{
		GoapValue a = 5;
		GoapValue b = 5;
		GoapValue c = 6;

		Assert.Multiple(() =>
		{
			Assert.That(a == b, Is.True);
			Assert.That(a.Equals(b), Is.True);
			Assert.That(a == c, Is.False);
			Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
		});
	}

	[Test]
	public void GoapValue_DefaultStruct_HasNullInnerValue()
	{
		GoapValue defaulted = default;

		Assert.That(defaulted.Value, Is.Null);
	}

	[Test]
	public void GoapGoal_IsGoalAchieved_WithStructEquality_RecognizesMatchingPrimitives()
	{
		var goal = Goal("x-is-7", ("x", 7));
		var states = State(("x", 7));

		Assert.That(goal.IsGoalAchieved(states), Is.True);
	}

	// ---------- operator-based goals (GoapGoal.Goals is List<GoapCondition>) ----------

	[Test]
	public void GoapGoal_IsGoalAchieved_WithLessThanOrEqual_True_AtBoundary()
	{
		var goal = GoalConditions("fed",
			new GoapCondition("hunger", GoapComparison.LessThanOrEqualTo, 2));

		Assert.That(goal.IsGoalAchieved(State(("hunger", 2))), Is.True);
		Assert.That(goal.IsGoalAchieved(State(("hunger", 0))), Is.True);
		Assert.That(goal.IsGoalAchieved(State(("hunger", 3))), Is.False);
	}

	[Test]
	public void GoapGoal_IsGoalAchieved_WithGreaterThan_RecognizesStrictOrdering()
	{
		var goal = GoalConditions("rich",
			new GoapCondition("gold", GoapComparison.GreaterThan, 100));

		Assert.That(goal.IsGoalAchieved(State(("gold", 100))), Is.False);
		Assert.That(goal.IsGoalAchieved(State(("gold", 101))), Is.True);
	}

	[Test]
	public void GoapGoal_IsGoalAchieved_WithNotEqualTo()
	{
		var goal = GoalConditions("not-idle",
			new GoapCondition("status", GoapComparison.NotEqualTo, "idle"));

		Assert.That(goal.IsGoalAchieved(State(("status", "idle"))), Is.False);
		Assert.That(goal.IsGoalAchieved(State(("status", "working"))), Is.True);
	}

	[Test]
	public void GoapGoal_IsGoalAchieved_AllConditionsMustHold()
	{
		var goal = GoalConditions("ready",
			new GoapCondition("energy", GoapComparison.GreaterThanOrEqualTo, 5),
			new GoapCondition("armed", GoapComparison.EqualTo, true));

		Assert.That(goal.IsGoalAchieved(State(("energy", 5), ("armed", true))), Is.True);
		Assert.That(goal.IsGoalAchieved(State(("energy", 4), ("armed", true))), Is.False);
		Assert.That(goal.IsGoalAchieved(State(("energy", 5), ("armed", false))), Is.False);
	}

	[Test]
	public void Find_PlansAgainstLessThanOrEqualGoal_WithoutGatingAction()
	{
		// Same scenario as the prior "gating action" workaround, now expressed directly
		// on the goal: hunger starts at 10, eat decreases by 4, goal is hunger <= 2.
		var agent = Agent(
			State(("hunger", 10)),
			actions:
			[
				Action("eat", effect: new("hunger", GoapOperation.DecreaseBy, 4)),
			]);
		var goal = GoalConditions("fed",
			new GoapCondition("hunger", GoapComparison.LessThanOrEqualTo, 2));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "eat", "eat" }));
	}

	[Test]
	public void Find_PlansAgainstGreaterThanGoal_PicksMinimumActions()
	{
		// Need level > 2; start at 0; each study adds 1 — three studies to exceed 2.
		var agent = Agent(
			State(("level", 0)),
			actions:
			[
				Action("study", effect: new("level", GoapOperation.IncreaseBy, 1)),
			]);
		var goal = GoalConditions("graduated",
			new GoapCondition("level", GoapComparison.GreaterThan, 2));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "study", "study", "study" }));
	}

	[Test]
	public void Find_ReturnsEmptyPlan_WhenOperatorGoalAlreadySatisfied()
	{
		var agent = Agent(
			State(("hunger", 1)),
			actions: [Action("eat", effect: new("hunger", GoapOperation.DecreaseBy, 4))]);
		var goal = GoalConditions("fed",
			new GoapCondition("hunger", GoapComparison.LessThanOrEqualTo, 2));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions, Is.Empty);
	}

	[Test]
	public void Find_MixedOperatorAndEqualityConditions_OnSameGoal()
	{
		// Goal requires armed == true AND energy >= 3.
		var agent = Agent(
			State(("armed", false), ("energy", 0)),
			actions:
			[
				Action("arm", effect: new("armed", GoapOperation.SetTo, true)),
				Action("rest", effect: new("energy", GoapOperation.IncreaseBy, 1)),
			]);
		var goal = GoalConditions("ready",
			new GoapCondition("armed", GoapComparison.EqualTo, true),
			new GoapCondition("energy", GoapComparison.GreaterThanOrEqualTo, 3));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		var names = plan!.Actions.Select(a => a.Name).ToList();
		Assert.That(names, Has.Count.EqualTo(4));
		Assert.That(names.Count(n => n == "rest"), Is.EqualTo(3));
		Assert.That(names.Count(n => n == "arm"), Is.EqualTo(1));
	}

	[Test]
	public void Find_ReturnsNull_WhenOperatorGoalIsUnreachable()
	{
		// Bounded hunger [0..100] with a goal of > 100 — the engine clamps every successor to the
		// bounded range, so the reachable state space is finite and the planner can prove no plan
		// exists rather than expanding forever.
		var agent = Agent(
			State(("hunger", 1)),
			actions: [Action("eat", effect: new("hunger", GoapOperation.DecreaseBy, 4))],
			bounds: new() { ["hunger"] = new(Min: 0, Max: 100) });
		var goal = GoalConditions("starve",
			new GoapCondition("hunger", GoapComparison.GreaterThan, 100));

		Assert.That(GoapPlan.Find(agent, goal), Is.Null);
	}

	[Test]
	public void GoapGoal_DictionaryConstructor_ProducesEqualityConditions()
	{
		// Back-compat sugar: GoapGoal(name, GoapWorldState) wraps each entry as EqualTo.
		var goal = new GoapGoal("legacy", State(("x", 7), ("flag", true))) { Id = "legacy" };

		Assert.That(goal.Goals, Has.Count.EqualTo(2));
		Assert.That(goal.Goals.All(c => c.Comparison == GoapComparison.EqualTo), Is.True);
		Assert.That(goal.IsGoalAchieved(State(("x", 7), ("flag", true))), Is.True);
		Assert.That(goal.IsGoalAchieved(State(("x", 8), ("flag", true))), Is.False);
	}

	// ---------------------------------------------------------------- state bounds

	[Test]
	public void GoapStateBounds_Clamp_ClampsBelowMin_AboveMax_AndPassesThroughInRange()
	{
		var bounds = new GoapStateBounds(Min: 0, Max: 100);

		Assert.That(bounds.Clamp(-5).Value, Is.EqualTo(0));
		Assert.That(bounds.Clamp(150).Value, Is.EqualTo(100));
		Assert.That(bounds.Clamp(50).Value, Is.EqualTo(50));
	}

	[Test]
	public void GoapStateBounds_Clamp_RespectsOpenEndedBounds()
	{
		var minOnly = new GoapStateBounds(Min: 0);
		Assert.That(minOnly.Clamp(-5).Value, Is.EqualTo(0));
		Assert.That(minOnly.Clamp(9999).Value, Is.EqualTo(9999));

		var maxOnly = new GoapStateBounds(Max: 10);
		Assert.That(maxOnly.Clamp(-9999).Value, Is.EqualTo(-9999));
		Assert.That(maxOnly.Clamp(50).Value, Is.EqualTo(10));
	}

	[Test]
	public void GoapAgent_SetState_ClampsThroughRegisteredBounds()
	{
		var agent = Agent(
			State(("hunger", 50)),
			actions: [],
			bounds: new() { ["hunger"] = new(Min: 0, Max: 100) });

		agent.SetState("hunger", 250);
		Assert.That(agent.GetState("hunger").Value, Is.EqualTo(100));

		agent.SetState("hunger", -10);
		Assert.That(agent.GetState("hunger").Value, Is.EqualTo(0));
	}

	[Test]
	public void GoapEffect_ApplyTo_ClampsThroughBounds()
	{
		var states = State(("hunger", 1));
		var bounds = new Dictionary<string, GoapStateBounds> { ["hunger"] = new(Min: 0, Max: 100) };

		new GoapEffect("hunger", GoapOperation.DecreaseBy, 4).ApplyTo(states, bounds);
		Assert.That(states["hunger"].Value, Is.EqualTo(0));

		new GoapEffect("hunger", GoapOperation.IncreaseBy, 9999).ApplyTo(states, bounds);
		Assert.That(states["hunger"].Value, Is.EqualTo(100));
	}

	[Test]
	public void Find_BoundedState_TerminatesAndPlansAgainstClampedRange()
	{
		// energy starts at 0, rest IncreaseBy 1, capped at 5; goal of energy >= 3 still reachable.
		var agent = Agent(
			State(("energy", 0)),
			actions: [Action("rest", effect: new("energy", GoapOperation.IncreaseBy, 1))],
			bounds: new() { ["energy"] = new(Min: 0, Max: 5) });
		var goal = GoalConditions("rested",
			new GoapCondition("energy", GoapComparison.GreaterThanOrEqualTo, 3));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions, Has.Count.EqualTo(3));
	}

	[Test]
	public void Find_BoundedState_GoalAboveMaxIsUnreachable()
	{
		// energy cap is 5; goal of energy >= 10 is unreachable; planner must return null.
		var agent = Agent(
			State(("energy", 0)),
			actions: [Action("rest", effect: new("energy", GoapOperation.IncreaseBy, 1))],
			bounds: new() { ["energy"] = new(Min: 0, Max: 5) });
		var goal = GoalConditions("impossible",
			new GoapCondition("energy", GoapComparison.GreaterThanOrEqualTo, 10));

		Assert.That(GoapPlan.Find(agent, goal), Is.Null);
	}

	// ---------------------------------------------------------------- agent

	[Test]
	public void SenseStates_PopulatesAgentStatesFromSensors()
	{
		var agent = Agent(
			State(),
			actions: [],
			sensors:
			[
				new GoapSensor("clock.hour", () => 12),
				new GoapSensor("hungry", () => true),
			]);

		agent.SenseStates();

		Assert.Multiple(() =>
		{
			Assert.That(agent.States["clock.hour"], Is.EqualTo((GoapValue)12));
			Assert.That(agent.States["hungry"], Is.EqualTo((GoapValue)true));
		});
	}

	[Test]
	public void CurrentGoals_OrdersByPriorityDescending()
	{
		var low = new GoapGoal("low", State()) { Id = "low", Priority = _ => 1 };
		var high = new GoapGoal("high", State()) { Id = "high", Priority = _ => 100 };
		var mid = new GoapGoal("mid", State()) { Id = "mid", Priority = _ => 50 };

		var agent = Agent(State(), actions: [], goals: [low, high, mid]);

		Assert.That(agent.CurrentGoals().Select(g => g.Id),
			Is.EqualTo(new[] { "high", "mid", "low" }));
	}

	// ------------------------------------------------------------- execution

	[Test]
	public void Execute_AppliesEffects_AndReturnsTrue()
	{
		var executed = new List<string>();
		var sleep = Action("sleep",
			effect: new("rested", GoapOperation.SetTo, true),
			run: () => { executed.Add("sleep"); return GoapActionResult.Success; });

		var goal = Goal("rest", ("rested", true));
		var agent = Agent(State(("rested", false)), actions: [sleep], goals: [goal]);
		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		var ok = plan!.Execute(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(ok, Is.True);
			Assert.That(executed, Is.EqualTo(new[] { "sleep" }));
			Assert.That(agent.States["rested"].Value, Is.EqualTo(true));
		});
	}

	[Test]
	public void Execute_StopsOnFailure_AndDoesNotApplyRemainingActions()
	{
		var executed = new List<string>();
		var first = Action("first",
			effect: new("a", GoapOperation.SetTo, true),
			run: () => { executed.Add("first"); return GoapActionResult.Success; });
		var second = Action("second",
			conditions: [new("a", GoapComparison.EqualTo, true)],
			effect: new("b", GoapOperation.SetTo, true),
			run: () => { executed.Add("second"); return GoapActionResult.Failed; });
		var third = Action("third",
			conditions: [new("b", GoapComparison.EqualTo, true)],
			effect: new("c", GoapOperation.SetTo, true),
			run: () => { executed.Add("third"); return GoapActionResult.Success; });

		var goal = Goal("c-done", ("c", true));
		var agent = Agent(
			State(("a", false), ("b", false), ("c", false)),
			actions: [first, second, third],
			goals: [goal]);

		var plan = GoapPlan.Find(agent, goal);
		Assert.That(plan, Is.Not.Null);

		var ok = plan!.Execute(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(ok, Is.False, "execution should report failure");
			Assert.That(executed, Is.EqualTo(new[] { "first", "second" }), "third must not run after failure");
			Assert.That(agent.States["b"].Value, Is.EqualTo(false), "failed action's success effect must not be applied");
			Assert.That(agent.States["c"].Value, Is.EqualTo(false));
		});
	}

	[Test]
	public void Execute_CancelsWhenPriorityGoalChanges_AndFlagSet()
	{
		var executed = new List<string>();
		var step1 = Action("step1",
			effect: new("done1", GoapOperation.SetTo, true),
			run: () => { executed.Add("step1"); return GoapActionResult.Success; });
		var step2 = Action("step2",
			conditions: [new("done1", GoapComparison.EqualTo, true)],
			effect: new("done2", GoapOperation.SetTo, true),
			run: () => { executed.Add("step2"); return GoapActionResult.Success; });

		// `primary` initially has higher priority. Once step1 has executed (done1==true),
		// `rival` outranks it, so the executor should cancel before running step2.
		var primary = new GoapGoal("primary", State(("done2", true)))
		{
			Id = "primary",
			Priority = a => a.States.TryGetValue("done1", out var v) && v.Value is bool b && b ? 1 : 100,
		};
		var rival = new GoapGoal("rival", State(("done2", true)))
		{
			Id = "rival",
			Priority = a => a.States.TryGetValue("done1", out var v) && v.Value is bool b && b ? 100 : 1,
		};

		var agent = Agent(
			State(("done1", false), ("done2", false)),
			actions: [step1, step2],
			goals: [primary, rival]);

		var plan = GoapPlan.Find(agent, primary);
		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "step1", "step2" }));

		var ok = plan.Execute(CancelOnGoalChange: true);

		Assert.Multiple(() =>
		{
			Assert.That(ok, Is.False, "should report cancelled when active goal changes mid-plan");
			Assert.That(executed, Is.EqualTo(new[] { "step1" }), "step2 must not run after goal switch");
		});
	}

	// ============================================================ validation

	[Test]
	public void GoapAction_Validate_Throws_WhenSuccessEffectIsNull()
	{
		var action = new GoapAction("nameless")
		{
			Conditions = [new("x", GoapComparison.EqualTo, true)],
			// SuccessEffect intentionally null
		};

		var ex = Assert.Throws<InvalidOperationException>(() => action.Validate());
		Assert.That(ex!.Message, Does.Contain("success effect"));
	}

	[Test]
	public void GoapAction_Validate_Succeeds_WithNoConditions()
	{
		// Unconditional actions are legitimate (sensor-driven autonomous actions, primitives).
		var action = new GoapAction("flip", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			Conditions = [],
		};

		Assert.DoesNotThrow(() => action.Validate());
	}

	[Test]
	public void GoapAction_Validate_Succeeds_WhenSuccessEffectAndConditionsPresent()
	{
		var action = new GoapAction("flip", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			Conditions = [new("y", GoapComparison.EqualTo, true)],
		};

		Assert.DoesNotThrow(() => action.Validate());
	}

	[Test]
	public void GoapGoal_Validate_Throws_WhenGoalsEmpty()
	{
		var goal = new GoapGoal("empty", State());
		var ex = Assert.Throws<InvalidOperationException>(() => goal.Validate());
		Assert.That(ex!.Message, Does.Contain("desired world state"));
	}

	// =================================================== HTN compound actions

	[Test]
	public void GoapAction_DefaultChildren_IsEmpty_AndNotCompound()
	{
		var action = new GoapAction("primitive", new GoapEffect("x", GoapOperation.SetTo, true));

		Assert.Multiple(() =>
		{
			Assert.That(action.Children, Is.Empty);
			Assert.That(action.IsCompound, Is.False);
		});
	}

	[Test]
	public void GoapAction_IsCompound_IsTrue_WhenChildrenPopulated()
	{
		var child = Action("child", effect: new("x", GoapOperation.SetTo, true));
		var compound = new GoapAction("compound", new GoapEffect("goal", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Children = [child],
		};

		Assert.That(compound.IsCompound, Is.True);
		Assert.That(compound.Children, Has.Count.EqualTo(1));
	}

	[Test]
	public void Find_TreatsCompoundActionAtomically_UsingSuccessEffect()
	{
		// Compound action's children are never visible to the planner — only its SuccessEffect is.
		// The planner should produce a single-step plan containing just the compound.
		var hiddenChild = Action("hidden-child", effect: new("never-used", GoapOperation.SetTo, true));
		var compound = new GoapAction("eat-meal", new GoapEffect("hunger.ok", GoapOperation.SetTo, true))
		{
			Conditions = [new("hunger.low", GoapComparison.EqualTo, true)],
			Children = [hiddenChild],
			Cost = _ => 1d,
		};

		var agent = Agent(
			State(("hunger.low", true), ("hunger.ok", false)),
			actions: [compound]);
		var goal = Goal("fed", ("hunger.ok", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "eat-meal" }),
			"planner must select the compound atomically, not expand its children");
	}

	[Test]
	public void Find_PrefersPrimitiveChain_OverCompound_WhenCheaper()
	{
		// Two routes to hunger.ok: a 2-step primitive chain (cost 1+1=2) and a single compound (cost 10).
		var fetchFood = Action("fetch-food", effect: new("food.ready", GoapOperation.SetTo, true), cost: 1d);
		var eatPrimitive = Action("eat",
			conditions: [new("food.ready", GoapComparison.EqualTo, true)],
			effect: new("hunger.ok", GoapOperation.SetTo, true),
			cost: 1d);
		var compound = new GoapAction("eat-meal", new GoapEffect("hunger.ok", GoapOperation.SetTo, true))
		{
			Conditions = [new("hunger.low", GoapComparison.EqualTo, true)],
			Children = [Action("placeholder", effect: new("noop", GoapOperation.SetTo, true))],
			Cost = _ => 10d,
		};

		var agent = Agent(
			State(("hunger.low", true), ("food.ready", false), ("hunger.ok", false)),
			actions: [fetchFood, eatPrimitive, compound]);
		var goal = Goal("fed", ("hunger.ok", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "fetch-food", "eat" }));
	}

	[Test]
	public void Find_PrefersCompound_OverPrimitiveChain_WhenCheaper()
	{
		// Same shape as above but with compound priced much lower.
		var fetchFood = Action("fetch-food", effect: new("food.ready", GoapOperation.SetTo, true), cost: 10d);
		var eatPrimitive = Action("eat",
			conditions: [new("food.ready", GoapComparison.EqualTo, true)],
			effect: new("hunger.ok", GoapOperation.SetTo, true),
			cost: 10d);
		var compound = new GoapAction("eat-meal", new GoapEffect("hunger.ok", GoapOperation.SetTo, true))
		{
			Conditions = [new("hunger.low", GoapComparison.EqualTo, true)],
			Children = [Action("placeholder", effect: new("noop", GoapOperation.SetTo, true))],
			Cost = _ => 1d,
		};

		var agent = Agent(
			State(("hunger.low", true), ("food.ready", false), ("hunger.ok", false)),
			actions: [fetchFood, eatPrimitive, compound]);
		var goal = Goal("fed", ("hunger.ok", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "eat-meal" }));
	}

	[Test]
	public void Find_SkipsCompound_WhenItsOwnPreconditionsNotMet()
	{
		// Compound has unmet preconditions; even though its SuccessEffect would satisfy the goal,
		// the planner must skip it and find another path.
		var directAction = Action("emergency-snack",
			effect: new("hunger.ok", GoapOperation.SetTo, true),
			cost: 100d);
		var compound = new GoapAction("eat-meal", new GoapEffect("hunger.ok", GoapOperation.SetTo, true))
		{
			Conditions = [new("memory.capable", GoapComparison.EqualTo, true)],
			Children = [Action("child", effect: new("noop", GoapOperation.SetTo, true))],
			Cost = _ => 1d,
		};

		var agent = Agent(
			State(("memory.capable", false), ("hunger.ok", false)),
			actions: [directAction, compound]);
		var goal = Goal("fed", ("hunger.ok", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name), Is.EqualTo(new[] { "emergency-snack" }),
			"compound must not be selected when its own conditions are unmet");
	}

	// ============================================ three-outcome effect model

	[Test]
	public void GoapActionResult_HasThreeExpectedValues()
	{
		var values = Enum.GetValues<GoapActionResult>();
		Assert.That(values, Is.EquivalentTo(new[]
		{
			GoapActionResult.Success,
			GoapActionResult.Interrupted,
			GoapActionResult.Failed,
		}));
	}

	[Test]
	public void GoapAction_InterruptedAndFailedEffects_DefaultToNull()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true));

		Assert.Multiple(() =>
		{
			Assert.That(action.InterruptedEffect, Is.Null);
			Assert.That(action.FailedEffect, Is.Null);
		});
	}

	[Test]
	public void GoapAction_UpdateStates_AppliesSuccessEffectOnly()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			InterruptedEffect = new GoapEffect("y", GoapOperation.SetTo, true),
			FailedEffect = new GoapEffect("z", GoapOperation.SetTo, true),
		};
		var states = State(("x", false), ("y", false), ("z", false));

		action.UpdateStates(states);

		Assert.Multiple(() =>
		{
			Assert.That(states["x"].Value, Is.EqualTo(true));
			Assert.That(states["y"].Value, Is.EqualTo(false));
			Assert.That(states["z"].Value, Is.EqualTo(false));
		});
	}

	[Test]
	public void ApplyResult_Success_AppliesSuccessEffect()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			InterruptedEffect = new GoapEffect("y", GoapOperation.SetTo, true),
			FailedEffect = new GoapEffect("z", GoapOperation.SetTo, true),
		};
		var states = State(("x", false), ("y", false), ("z", false));

		action.ApplyResult(GoapActionResult.Success, states);

		Assert.Multiple(() =>
		{
			Assert.That(states["x"].Value, Is.EqualTo(true));
			Assert.That(states["y"].Value, Is.EqualTo(false));
			Assert.That(states["z"].Value, Is.EqualTo(false));
		});
	}

	[Test]
	public void ApplyResult_Interrupted_AppliesInterruptedEffect_WhenPresent()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			InterruptedEffect = new GoapEffect("y", GoapOperation.SetTo, true),
		};
		var states = State(("x", false), ("y", false));

		action.ApplyResult(GoapActionResult.Interrupted, states);

		Assert.Multiple(() =>
		{
			Assert.That(states["x"].Value, Is.EqualTo(false), "success effect must not fire on Interrupted");
			Assert.That(states["y"].Value, Is.EqualTo(true));
		});
	}

	[Test]
	public void ApplyResult_Interrupted_NoOp_WhenInterruptedEffectNull()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true));
		var states = State(("x", false));

		Assert.DoesNotThrow(() => action.ApplyResult(GoapActionResult.Interrupted, states));
		Assert.That(states["x"].Value, Is.EqualTo(false));
	}

	[Test]
	public void ApplyResult_Failed_AppliesFailedEffect_WhenPresent()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			FailedEffect = new GoapEffect("z", GoapOperation.SetTo, true),
		};
		var states = State(("x", false), ("z", false));

		action.ApplyResult(GoapActionResult.Failed, states);

		Assert.Multiple(() =>
		{
			Assert.That(states["x"].Value, Is.EqualTo(false), "success effect must not fire on Failed");
			Assert.That(states["z"].Value, Is.EqualTo(true));
		});
	}

	[Test]
	public void ApplyResult_Failed_NoOp_WhenFailedEffectNull()
	{
		var action = new GoapAction("a", new GoapEffect("x", GoapOperation.SetTo, true));
		var states = State(("x", false));

		Assert.DoesNotThrow(() => action.ApplyResult(GoapActionResult.Failed, states));
		Assert.That(states["x"].Value, Is.EqualTo(false));
	}

	// ============================================== status → result mapping

	[Test]
	public void AgentActionStatus_ToGoapResult_MapsTerminalStates()
	{
		Assert.Multiple(() =>
		{
			Assert.That(AgentActionStatus.Succeeded.ToGoapResult(),
				Is.EqualTo(GoapActionResult.Success));
			Assert.That(AgentActionStatus.Cancelled.ToGoapResult(),
				Is.EqualTo(GoapActionResult.Interrupted));
			Assert.That(AgentActionStatus.Failed.ToGoapResult(),
				Is.EqualTo(GoapActionResult.Failed));
			Assert.That(AgentActionStatus.Pending.ToGoapResult(),
				Is.EqualTo(GoapActionResult.Failed),
				"non-terminal statuses default to Failed");
			Assert.That(AgentActionStatus.Running.ToGoapResult(),
				Is.EqualTo(GoapActionResult.Failed),
				"non-terminal statuses default to Failed");
		});
	}

	// ===================================== additional operator / comparison coverage

	[Test]
	public void Find_HandlesDecreaseByEffect_WithLessThanOrEqualCondition()
	{
		// Start hunger at 10; eat decreases by 4; goal is hunger <= 2 -> need two eats.
		var agent = Agent(
			State(("hunger", 10)),
			actions:
			[
				Action("eat", effect: new("hunger", GoapOperation.DecreaseBy, 4)),
			]);
		var goal = new GoapGoal("fed", State(("hunger", 2))) { Id = "fed" };
		// Use a custom condition-style goal via a gating action instead, since GoapGoal uses equality.
		var gateAgent = Agent(
			State(("hunger", 10), ("fed", false)),
			actions:
			[
				Action("eat", effect: new("hunger", GoapOperation.DecreaseBy, 4)),
				Action("declare-fed",
					conditions: [new("hunger", GoapComparison.LessThanOrEqualTo, 2)],
					effect: new("fed", GoapOperation.SetTo, true)),
			]);
		var fedGoal = Goal("fed", ("fed", true));

		var plan = GoapPlan.Find(gateAgent, fedGoal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "eat", "eat", "declare-fed" }));
	}

	[Test]
	public void Find_HandlesLessThanCondition()
	{
		var agent = Agent(
			State(("temperature", 5), ("comfortable", false)),
			actions:
			[
				Action("heat", effect: new("temperature", GoapOperation.IncreaseBy, 1)),
				Action("relax",
					conditions: [new("temperature", GoapComparison.LessThan, 8)],
					effect: new("comfortable", GoapOperation.SetTo, true)),
			]);
		var goal = Goal("cosy", ("comfortable", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.First().Name, Is.EqualTo("relax"),
			"LessThan(8) is already satisfied at 5 — no heat needed");
	}

	[Test]
	public void Find_HandlesGreaterThanCondition()
	{
		var agent = Agent(
			State(("level", 0), ("graduated", false)),
			actions:
			[
				Action("study", effect: new("level", GoapOperation.IncreaseBy, 1)),
				Action("graduate",
					conditions: [new("level", GoapComparison.GreaterThan, 2)],
					effect: new("graduated", GoapOperation.SetTo, true)),
			]);
		var goal = Goal("done", ("graduated", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions.Select(a => a.Name),
			Is.EqualTo(new[] { "study", "study", "study", "graduate" }));
	}

	// ============================================ runtime three-outcome flow

	[Test]
	public void Run_AppliesSuccessEffect_AndReturnsSuccess()
	{
		var sleep = Action("sleep", effect: new("rested", GoapOperation.SetTo, true));
		var goal = Goal("rest", ("rested", true));
		var agent = Agent(State(("rested", false)), actions: [sleep], goals: [goal]);

		var plan = GoapPlan.Find(agent, goal)!;
		var outcome = plan.Run(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(outcome, Is.EqualTo(GoapActionResult.Success));
			Assert.That(agent.States["rested"].Value, Is.EqualTo(true));
		});
	}

	[Test]
	public void Run_AppliesInterruptedEffect_OnInterruptedOutcome_AndStops()
	{
		var first = new GoapAction("first", new GoapEffect("a", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			InterruptedEffect = new GoapEffect("a.partial", GoapOperation.SetTo, true),
			Run = () => GoapActionResult.Interrupted,
		};
		var second = Action("second",
			conditions: [new("a", GoapComparison.EqualTo, true)],
			effect: new("b", GoapOperation.SetTo, true));

		var goal = Goal("b-true", ("b", true));
		var agent = Agent(
			State(("ready", true), ("a", false), ("a.partial", false), ("b", false)),
			actions: [first, second], goals: [goal]);

		var plan = GoapPlan.Find(agent, goal)!;
		var outcome = plan.Run(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(outcome, Is.EqualTo(GoapActionResult.Interrupted));
			Assert.That(agent.States["a"].Value, Is.EqualTo(false), "success effect must not fire on Interrupted");
			Assert.That(agent.States["a.partial"].Value, Is.EqualTo(true), "interrupted effect must fire");
			Assert.That(agent.States["b"].Value, Is.EqualTo(false), "subsequent step must not run");
		});
	}

	[Test]
	public void Run_AppliesFailedEffect_OnFailedOutcome_AndStops()
	{
		var attempt = new GoapAction("attempt", new GoapEffect("done", GoapOperation.SetTo, true))
		{
			Conditions = [new("can-try", GoapComparison.EqualTo, true)],
			FailedEffect = new GoapEffect("attempts.exhausted", GoapOperation.SetTo, true),
			Run = () => GoapActionResult.Failed,
		};

		var goal = Goal("done", ("done", true));
		var agent = Agent(
			State(("can-try", true), ("done", false), ("attempts.exhausted", false)),
			actions: [attempt], goals: [goal]);

		var plan = GoapPlan.Find(agent, goal)!;
		var outcome = plan.Run(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(outcome, Is.EqualTo(GoapActionResult.Failed));
			Assert.That(agent.States["done"].Value, Is.EqualTo(false));
			Assert.That(agent.States["attempts.exhausted"].Value, Is.EqualTo(true));
		});
	}

	[Test]
	public void Run_CancelOnGoalChange_ReturnsInterrupted()
	{
		var executed = new List<string>();
		var step1 = new GoapAction("step1", new GoapEffect("done1", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Run = () => { executed.Add("step1"); return GoapActionResult.Success; },
		};
		var step2 = new GoapAction("step2", new GoapEffect("done2", GoapOperation.SetTo, true))
		{
			Conditions = [new("done1", GoapComparison.EqualTo, true)],
			Run = () => { executed.Add("step2"); return GoapActionResult.Success; },
		};

		var primary = new GoapGoal("primary", State(("done2", true)))
		{
			Id = "primary",
			Priority = a => a.States.TryGetValue("done1", out var v) && v.Value is bool b && b ? 1 : 100,
		};
		var rival = new GoapGoal("rival", State(("done2", true)))
		{
			Id = "rival",
			Priority = a => a.States.TryGetValue("done1", out var v) && v.Value is bool b && b ? 100 : 1,
		};

		var agent = Agent(
			State(("ready", true), ("done1", false), ("done2", false)),
			actions: [step1, step2], goals: [primary, rival]);
		var plan = GoapPlan.Find(agent, primary)!;

		var outcome = plan.Run(CancelOnGoalChange: true);

		Assert.Multiple(() =>
		{
			Assert.That(outcome, Is.EqualTo(GoapActionResult.Interrupted));
			Assert.That(executed, Is.EqualTo(new[] { "step1" }));
		});
	}

	// =================================================== compound execution

	[Test]
	public void Run_DecomposesCompoundIntoChildren_AndAppliesEachChildSuccess()
	{
		var executed = new List<string>();
		var c1 = new GoapAction("child-1", new GoapEffect("a", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Run = () => { executed.Add("child-1"); return GoapActionResult.Success; },
		};
		var c2 = new GoapAction("child-2", new GoapEffect("b", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Run = () => { executed.Add("child-2"); return GoapActionResult.Success; },
		};
		var compound = new GoapAction("compound", new GoapEffect("goal", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Children = [c1, c2],
		};

		var goal = Goal("done", ("goal", true));
		var agent = Agent(
			State(("ready", true), ("a", false), ("b", false), ("goal", false)),
			actions: [compound], goals: [goal]);

		var plan = GoapPlan.Find(agent, goal)!;
		var outcome = plan.Run(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(outcome, Is.EqualTo(GoapActionResult.Success));
			Assert.That(executed, Is.EqualTo(new[] { "child-1", "child-2" }));
			Assert.That(agent.States["a"].Value, Is.EqualTo(true), "child-1 success effect must apply");
			Assert.That(agent.States["b"].Value, Is.EqualTo(true), "child-2 success effect must apply");
		});
	}

	[Test]
	public void Run_CompoundChildInterruption_PropagatesAndStops()
	{
		var executed = new List<string>();
		var c1 = new GoapAction("c1", new GoapEffect("a", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Run = () => { executed.Add("c1"); return GoapActionResult.Success; },
		};
		var c2 = new GoapAction("c2", new GoapEffect("b", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			InterruptedEffect = new GoapEffect("b.partial", GoapOperation.SetTo, true),
			Run = () => { executed.Add("c2"); return GoapActionResult.Interrupted; },
		};
		var c3 = new GoapAction("c3", new GoapEffect("c", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Run = () => { executed.Add("c3"); return GoapActionResult.Success; },
		};
		var compound = new GoapAction("compound", new GoapEffect("goal", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Children = [c1, c2, c3],
		};

		var goal = Goal("done", ("goal", true));
		var agent = Agent(
			State(
				("ready", true),
				("a", false), ("b", false), ("b.partial", false), ("c", false), ("goal", false)),
			actions: [compound], goals: [goal]);

		var plan = GoapPlan.Find(agent, goal)!;
		var outcome = plan.Run(CancelOnGoalChange: false);

		Assert.Multiple(() =>
		{
			Assert.That(outcome, Is.EqualTo(GoapActionResult.Interrupted));
			Assert.That(executed, Is.EqualTo(new[] { "c1", "c2" }), "c3 must not run after interruption");
			Assert.That(agent.States["a"].Value, Is.EqualTo(true));
			Assert.That(agent.States["b"].Value, Is.EqualTo(false));
			Assert.That(agent.States["b.partial"].Value, Is.EqualTo(true));
			Assert.That(agent.States["c"].Value, Is.EqualTo(false));
		});
	}

	// ==================================================== compound validation

	[Test]
	public void Validate_Throws_OnSelfReferentialCompound()
	{
		var compound = new GoapAction("self", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
		};
		compound.Children.Add(compound);

		var ex = Assert.Throws<InvalidOperationException>(() => compound.Validate());
		Assert.That(ex!.Message, Does.Contain("cycle"));
	}

	[Test]
	public void Validate_Throws_OnCompoundCycle()
	{
		var a = new GoapAction("a", new GoapEffect("xa", GoapOperation.SetTo, true))
		{ Conditions = [new("ready", GoapComparison.EqualTo, true)] };
		var b = new GoapAction("b", new GoapEffect("xb", GoapOperation.SetTo, true))
		{ Conditions = [new("ready", GoapComparison.EqualTo, true)] };
		a.Children.Add(b);
		b.Children.Add(a);

		Assert.Throws<InvalidOperationException>(() => a.Validate());
	}

	[Test]
	public void Validate_Throws_OnNullChild()
	{
		var compound = new GoapAction("c", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Children = [null!],
		};

		var ex = Assert.Throws<InvalidOperationException>(() => compound.Validate());
		Assert.That(ex!.Message, Does.Contain("null child"));
	}

	[Test]
	public void Validate_Allows_NonCyclicCompoundTree()
	{
		var leaf = new GoapAction("leaf", new GoapEffect("y", GoapOperation.SetTo, true))
		{ Conditions = [new("ready", GoapComparison.EqualTo, true)] };
		var mid = new GoapAction("mid", new GoapEffect("z", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Children = [leaf],
		};
		var root = new GoapAction("root", new GoapEffect("x", GoapOperation.SetTo, true))
		{
			Conditions = [new("ready", GoapComparison.EqualTo, true)],
			Children = [mid, leaf], // same leaf reachable twice is allowed
		};

		Assert.DoesNotThrow(() => root.Validate());
	}

	// =================================================== plan still-validity

	[Test]
	public void IsStillValid_ReturnsTrue_ForFreshlyBuiltPlan()
	{
		var agent = Agent(
			State(("a", false), ("b", false)),
			actions:
			[
				Action("first", effect: new("a", GoapOperation.SetTo, true)),
				Action("second",
					conditions: [new("a", GoapComparison.EqualTo, true)],
					effect: new("b", GoapOperation.SetTo, true)),
			]);
		var goal = Goal("done", ("b", true));
		var plan = GoapPlan.Find(agent, goal)!;

		Assert.That(plan.IsStillValid(), Is.True);
	}

	[Test]
	public void IsStillValid_ReturnsFalse_WhenPreconditionsBecomeUnsatisfiable()
	{
		var first = Action("first",
			conditions: [new("gate", GoapComparison.EqualTo, true)],
			effect: new("a", GoapOperation.SetTo, true));
		var second = Action("second",
			conditions: [new("a", GoapComparison.EqualTo, true)],
			effect: new("b", GoapOperation.SetTo, true));

		var agent = Agent(State(("gate", true), ("a", false), ("b", false)), actions: [first, second]);
		var goal = Goal("done", ("b", true));
		var plan = GoapPlan.Find(agent, goal)!;
		Assert.That(plan.IsStillValid(), Is.True, "should be valid before world mutation");

		// World shifts: gate slammed shut.
		agent.States["gate"] = false;

		Assert.That(plan.IsStillValid(), Is.False);
	}

	// ============================================ goal-achieved consistency

	[Test]
	public void GoapGoal_IsGoalAchieved_DoesNotThrow_OnMissingStateKey()
	{
		var goal = Goal("x-true", ("x", true));
		var partialState = State(); // no "x" key at all

		Assert.DoesNotThrow(() => goal.IsGoalAchieved(partialState));
		Assert.That(goal.IsGoalAchieved(partialState), Is.False);
	}

	// ============================================ parametric actions

	[Test]
	public void ParametricExpansion_SubstitutesNameConditionAndEffect()
	{
		var template = new GoapAction("equip-{tool}")
		{
			Parameters = ["tool"],
			ParameterBindings = { ["tool"] = "has.item-tag.{tool}" },
			Conditions = [new GoapCondition("has.item-tag.{tool}", GoapComparison.EqualTo, true)],
			SuccessEffect = new GoapEffect("tool.{tool}.equipped", GoapOperation.SetTo, true),
		};

		var expanded = GoapParameterSubstitution.ExpandParametricActions(
			[template],
			_ => new[] { "mining", "woodcutting" });

		Assert.That(expanded, Has.Count.EqualTo(2));
		var byName = expanded.ToDictionary(a => a.Name);
		Assert.That(byName.Keys, Is.EquivalentTo(new[] { "equip-mining", "equip-woodcutting" }));
		var mining = byName["equip-mining"];
		Assert.That(mining.Conditions[0].StateId, Is.EqualTo("has.item-tag.mining"));
		Assert.That(mining.SuccessEffect.StateId, Is.EqualTo("tool.mining.equipped"));
		Assert.That(mining.Bindings["tool"], Is.EqualTo("mining"));
	}

	[Test]
	public void ParametricExpansion_OmitsTemplate_WhenResolverYieldsNothing()
	{
		var template = new GoapAction("equip-{tool}")
		{
			Parameters = ["tool"],
			ParameterBindings = { ["tool"] = "has.item-tag.{tool}" },
			SuccessEffect = new GoapEffect("tool.{tool}.equipped", GoapOperation.SetTo, true),
		};

		var expanded = GoapParameterSubstitution.ExpandParametricActions([template], _ => []);

		Assert.That(expanded, Is.Empty);
	}

	[Test]
	public void ParametricExpansion_PassesThroughNonParametricActions()
	{
		var plain = Action("walk", effect: new("at.destination", GoapOperation.SetTo, true));
		var template = new GoapAction("equip-{tool}")
		{
			Parameters = ["tool"],
			ParameterBindings = { ["tool"] = "has.item-tag.{tool}" },
			SuccessEffect = new GoapEffect("tool.{tool}.equipped", GoapOperation.SetTo, true),
		};

		var expanded = GoapParameterSubstitution.ExpandParametricActions(
			[plain, template],
			_ => new[] { "mining" });

		Assert.That(expanded, Has.Count.EqualTo(2));
		Assert.That(expanded[0], Is.SameAs(plain), "non-parametric action should be reused unchanged");
		Assert.That(expanded[1].Name, Is.EqualTo("equip-mining"));
	}

	[Test]
	public void ParametricExpansion_ProducesCartesianProduct_AcrossMultipleParameters()
	{
		var template = new GoapAction("place-{material}-on-{spot}")
		{
			Parameters = ["material", "spot"],
			ParameterBindings =
			{
				["material"] = "has.item.{material}",
				["spot"] = "site.{spot}",
			},
			SuccessEffect = new GoapEffect("placed.{material}.{spot}", GoapOperation.SetTo, true),
		};

		var expanded = GoapParameterSubstitution.ExpandParametricActions(
			[template],
			pattern => pattern.StartsWith("has.item.") ? new[] { "stone", "wood" } : new[] { "north", "south" });

		Assert.That(expanded.Select(a => a.Name), Is.EquivalentTo(new[]
		{
			"place-stone-on-north",
			"place-stone-on-south",
			"place-wood-on-north",
			"place-wood-on-south",
		}));
	}

	[Test]
	public void Find_PlansThroughInstantiatedParametricAction()
	{
		// Single parametric template; agent has only a pickaxe (mining), so only the mining
		// instantiation should be usable. Goal asks for tool.mining.equipped.
		var template = new GoapAction("equip-{tool}-tool")
		{
			Parameters = ["tool"],
			ParameterBindings = { ["tool"] = "has.item-tag.{tool}" },
			Conditions = [new GoapCondition("has.item-tag.{tool}", GoapComparison.EqualTo, true)],
			SuccessEffect = new GoapEffect("tool.{tool}.equipped", GoapOperation.SetTo, true),
		};

		var states = State(
			("has.item-tag.mining", true),
			("tool.mining.equipped", false));
		var agent = Agent(states, actions: [template]);
		agent.ResolveParameterBindings = pattern =>
			pattern == "has.item-tag.{tool}" ? new[] { "mining" } : [];

		var goal = Goal("equip-mining", ("tool.mining.equipped", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions, Has.Count.EqualTo(1));
		var step = plan.Actions[0];
		Assert.That(step.Name, Is.EqualTo("equip-mining-tool"));
		Assert.That(step.Bindings["tool"], Is.EqualTo("mining"));
	}

	[Test]
	public void Find_ChoosesCorrectBinding_WhenMultipleAreAvailable()
	{
		// Agent has both mining + woodcutting tags. Goal wants woodcutting equipped only; planner
		// must pick the binding that satisfies the goal.
		var template = new GoapAction("equip-{tool}")
		{
			Parameters = ["tool"],
			ParameterBindings = { ["tool"] = "has.item-tag.{tool}" },
			Conditions = [new GoapCondition("has.item-tag.{tool}", GoapComparison.EqualTo, true)],
			SuccessEffect = new GoapEffect("tool.{tool}.equipped", GoapOperation.SetTo, true),
		};

		var states = State(
			("has.item-tag.mining", true),
			("has.item-tag.woodcutting", true),
			("tool.mining.equipped", false),
			("tool.woodcutting.equipped", false));
		var agent = Agent(states, actions: [template]);
		agent.ResolveParameterBindings = _ => new[] { "mining", "woodcutting" };

		var goal = Goal("woodcut", ("tool.woodcutting.equipped", true));

		var plan = GoapPlan.Find(agent, goal);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Actions, Has.Count.EqualTo(1));
		Assert.That(plan.Actions[0].Bindings["tool"], Is.EqualTo("woodcutting"));
		Assert.That(plan.Actions[0].SuccessEffect.StateId, Is.EqualTo("tool.woodcutting.equipped"));
	}

	[Test]
	public void Find_SkipsParametricActions_WhenResolverIsUnset()
	{
		var template = new GoapAction("equip-{tool}")
		{
			Parameters = ["tool"],
			ParameterBindings = { ["tool"] = "has.item-tag.{tool}" },
			Conditions = [new GoapCondition("has.item-tag.{tool}", GoapComparison.EqualTo, true)],
			SuccessEffect = new GoapEffect("tool.{tool}.equipped", GoapOperation.SetTo, true),
		};

		var agent = Agent(
			State(("has.item-tag.mining", true), ("tool.mining.equipped", false)),
			actions: [template]);
		// ResolveParameterBindings deliberately left null.

		var goal = Goal("equip-mining", ("tool.mining.equipped", true));

		Assert.That(GoapPlan.Find(agent, goal), Is.Null);
	}
}
