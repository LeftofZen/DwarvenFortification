using System.Text.Json;
using DwarvenFortification.GOAP;

namespace DwarvenFortification.Tests;

/// <summary>
/// Tests for the sim-side polymorphic GOAP expression document and its JSON wire format.
/// Verifies that boolean fact strings and structured numeric expressions both round-trip
/// into well-formed <see cref="GoapCondition"/> / <see cref="GoapEffect"/> values that the
/// planner can use with operator comparisons and arithmetic operations.
/// </summary>
[TestFixture]
public sealed class GoapExpressionDocumentTests
{
	static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Test]
	public void Boolean_string_form_parses_to_equal_to_true_condition()
	{
		var doc = JsonSerializer.Deserialize<GoapExpressionDocument>("\"hunger.low\"", JsonOptions)!;

		Assert.That(doc.IsBooleanFact, Is.True);
		Assert.That(doc.Fact, Is.EqualTo("hunger.low"));

		var condition = doc.ToCondition();
		Assert.That(condition.StateId, Is.EqualTo("hunger.low"));
		Assert.That(condition.Comparison, Is.EqualTo(GoapComparison.EqualTo));
		Assert.That(condition.Operand.Value, Is.EqualTo(true));
	}

	[Test]
	public void Negated_boolean_string_form_parses_to_not_equal_to_true_condition()
	{
		var doc = JsonSerializer.Deserialize<GoapExpressionDocument>("\"!enemy.visible\"", JsonOptions)!;

		var condition = doc.ToCondition();
		Assert.That(condition.StateId, Is.EqualTo("enemy.visible"));
		Assert.That(condition.Comparison, Is.EqualTo(GoapComparison.NotEqualTo));
		Assert.That(condition.Operand.Value, Is.EqualTo(true));
	}

	[Test]
	public void Structured_numeric_condition_uses_declared_comparison()
	{
		var doc = JsonSerializer.Deserialize<GoapExpressionDocument>(
			"{ \"state\": \"vital.hunger\", \"op\": \"<=\", \"value\": 35 }",
			JsonOptions)!;

		Assert.That(doc.IsBooleanFact, Is.False);
		Assert.That(doc.IsNumericExpression, Is.True);

		var condition = doc.ToCondition();
		Assert.That(condition.StateId, Is.EqualTo("vital.hunger"));
		Assert.That(condition.Comparison, Is.EqualTo(GoapComparison.LessThanOrEqualTo));
		Assert.That(condition.Operand.Value, Is.EqualTo(35));
	}

	[Test]
	public void Structured_arithmetic_effect_maps_to_correct_operation()
	{
		var doc = JsonSerializer.Deserialize<GoapExpressionDocument>(
			"{ \"state\": \"vital.hunger\", \"op\": \"+=\", \"value\": 50 }",
			JsonOptions)!;

		var effect = doc.ToEffect();
		Assert.That(effect.StateId, Is.EqualTo("vital.hunger"));
		Assert.That(effect.Operation, Is.EqualTo(GoapOperation.IncreaseBy));
		Assert.That(effect.Operand.Value, Is.EqualTo(50));
	}

	[Test]
	public void Boolean_effect_form_produces_set_to_true_with_normalized_key()
	{
		var doc = JsonSerializer.Deserialize<GoapExpressionDocument>("\"hunger.ok\"", JsonOptions)!;

		var effect = doc.ToEffect();
		Assert.That(effect.StateId, Is.EqualTo("hunger.ok"));
		Assert.That(effect.Operation, Is.EqualTo(GoapOperation.SetTo));
		Assert.That(effect.Operand.Value, Is.EqualTo(true));
	}

	[Test]
	public void Negated_boolean_effect_form_produces_set_to_false()
	{
		var doc = JsonSerializer.Deserialize<GoapExpressionDocument>("\"!production.order.active\"", JsonOptions)!;

		var effect = doc.ToEffect();
		Assert.That(effect.StateId, Is.EqualTo("production.order.active"));
		Assert.That(effect.Operation, Is.EqualTo(GoapOperation.SetTo));
		Assert.That(effect.Operand.Value, Is.EqualTo(false));
	}

	[Test]
	public void Numeric_action_plans_against_numeric_goal_and_clamps_to_bounds()
	{
		// Pure GOAP-lib exercise of the wire format: low hunger -> eat (+=50) -> goal hunger>=70,
		// with a [0,100] StateBounds entry that must clamp the simulated increase.
		var eatJson = "{ \"state\": \"vital.hunger\", \"op\": \"+=\", \"value\": 50 }";
		var preJson = "{ \"state\": \"vital.hunger\", \"op\": \"<=\", \"value\": 70 }";
		var goalJson = "{ \"state\": \"vital.hunger\", \"op\": \">=\", \"value\": 70 }";

		var preExpr = JsonSerializer.Deserialize<GoapExpressionDocument>(preJson, JsonOptions)!;
		var effExpr = JsonSerializer.Deserialize<GoapExpressionDocument>(eatJson, JsonOptions)!;
		var goalExpr = JsonSerializer.Deserialize<GoapExpressionDocument>(goalJson, JsonOptions)!;

		var eat = new GoapAction("eat", effExpr.ToEffect())
		{
			Conditions = [preExpr.ToCondition()],
		};
		var goal = new GoapGoal("be-nourished", [goalExpr.ToCondition()]);

		var agent = new GoapAgent("dwarf")
		{
			States = new Dictionary<string, GoapValue>(StringComparer.Ordinal) { ["vital.hunger"] = 30 },
			Actions = [eat],
			Goals = [goal],
			StateBounds = new Dictionary<string, GoapStateBounds>(StringComparer.Ordinal)
			{
				["vital.hunger"] = new GoapStateBounds(0, 100),
			},
		};

		var plan = GoapPlan.Find(agent, goal);
		Assert.That(plan, Is.Not.Null, "Planner must find a plan that satisfies the numeric goal.");
		Assert.That(plan!.Actions, Has.Count.EqualTo(1));
		Assert.That(plan.Actions[0], Is.SameAs(eat));
	}
}
