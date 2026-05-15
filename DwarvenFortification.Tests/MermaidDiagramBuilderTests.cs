using Arch.Core;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.GOAP;
using DwarvenFortification.GOAP.Actions;
using DwarvenFortification.GOAP.Plans;
using Microsoft.Xna.Framework;
using System.Text.Json;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class MermaidDiagramBuilderTests
{
	[Test]
	public void BuildTreemapDiagram()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagram = PlanMermaidDiagramBuilder.BuildTreemapDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("treemap-beta"));
			Assert.That(diagram, Does.Contain("\"Goal: Secure Food (cost 40)\""));
			Assert.That(diagram, Does.Contain("    \"Requirement: Require fact 'food.available' (cost 40)\""));
			Assert.That(diagram, Does.Contain("        \"Requirement: Require fact 'seasoning.ready' (cost 6)\""));
			Assert.That(diagram, Does.Contain("            \"Requirement: Require fact 'seasoning.salt' (cost 1)\""));
			Assert.That(diagram, Does.Contain("                \"Action: quarry-salt (cost 1)\": 1"));
			Assert.That(diagram, Does.Contain("            \"Requirement: Require fact 'seasoning.herbs' (cost 2)\""));
			Assert.That(diagram, Does.Contain("                \"Action: gather-herbs (cost 2)\": 2"));
			Assert.That(diagram, Does.Contain("            \"Action: grind-seasoning (cost 3)\": 3"));
			Assert.That(diagram, Does.Contain("        \"Requirement: Require fact 'berries.found' (cost 24)\""));
			Assert.That(diagram, Does.Contain("            \"Requirement: Require fact 'basket.ready' (cost 15)\""));
			Assert.That(diagram, Does.Contain("                \"Requirement: Require fact 'fiber.collected' (cost 7)\""));
			Assert.That(diagram, Does.Contain("                    \"Action: collect-fiber (cost 7)\": 7"));
			Assert.That(diagram, Does.Contain("                \"Action: weave-basket (cost 8)\": 8"));
			Assert.That(diagram, Does.Contain("            \"Action: forage-berries (cost 9)\": 9"));
			Assert.That(diagram, Does.Contain("        \"Action: cook-feast (cost 10)\": 10"));
		});
	}

	[Test]
	public void BuildGanttDiagram()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagram = PlanMermaidDiagramBuilder.BuildGanttDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("gantt"));
			Assert.That(diagram, Does.Contain("title Plan- Secure Food"));
			Assert.That(diagram, Does.Contain("dateFormat X"));
			Assert.That(diagram, Does.Contain("section Actions"));
			Assert.That(diagram, Does.Contain("1. quarry-salt :task1, 0, 1s"));
			Assert.That(diagram, Does.Contain("2. gather-herbs :task2, after task1, 2s"));
			Assert.That(diagram, Does.Contain("3. grind-seasoning :task3, after task2, 3s"));
			Assert.That(diagram, Does.Contain("4. collect-fiber :task4, after task3, 7s"));
			Assert.That(diagram, Does.Contain("5. weave-basket :task5, after task4, 8s"));
			Assert.That(diagram, Does.Contain("6. forage-berries :task6, after task5, 9s"));
			Assert.That(diagram, Does.Contain("7. cook-feast :task7, after task6, 10s"));
		});
	}

	[Test]
	public void BuildBothDiagrams()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagrams = PlanMermaidDiagramBuilder.BuildDiagrams(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagrams.Treemap, Does.StartWith("treemap-beta"));
			Assert.That(diagrams.Gantt, Does.StartWith("gantt"));
			Assert.That(diagrams.Gantt, Does.Contain("7. cook-feast :task7, after task6, 10s"));
		});
	}

	static Plan CreatePlannedSecureFoodPlan()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				DesiredFacts = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("quarry-salt", ["seasoning.salt"], durationTicks: 1, baseCost: 0),
			CreateActionDefinition("gather-herbs", ["seasoning.herbs"], durationTicks: 2, baseCost: 0),
			CreateActionDefinition("grind-seasoning", ["seasoning.ready"], requiredFacts: ["seasoning.salt", "seasoning.herbs"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("collect-fiber", ["fiber.collected"], durationTicks: 7, baseCost: 0),
			CreateActionDefinition("weave-basket", ["basket.ready"], requiredFacts: ["fiber.collected"], durationTicks: 8, baseCost: 0),
			CreateActionDefinition("forage-berries", ["berries.found"], requiredFacts: ["basket.ready"], durationTicks: 9, baseCost: 0),
			CreateActionDefinition("cook-feast", ["food.available"], requiredFacts: ["berries.found", "seasoning.ready"], durationTicks: 10, baseCost: 0),
		};

		var (planner, agent) = CreatePlanner(goals, actions, [], _ => ["quarry-salt", "gather-herbs", "grind-seasoning", "collect-fiber", "weave-basket", "forage-berries", "cook-feast"]);
		return planner.BuildCandidatePlans(agent).Single();
	}

	static (Planner Planner, Entity Agent) CreatePlanner(
		GoalDefinition[] goals,
		ActionDefinition[] actions,
		IEnumerable<string> currentFacts,
		Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
	{
		var definitions = TestSimulationDefinitions.CreateRegistry(actions, goals);
		var world = World.Create();
		var agent = world.Create();
		var queryService = new StubGoapWorldQueryService(currentFacts, availableActionIdsFactory);
		return (new Planner(definitions, queryService), agent);
	}

	static ActionDefinition CreateActionDefinition(string id, string[] addFacts, string[]? requiredFacts = null, int durationTicks = 0, int baseCost = 1)
		=> new()
		{
			Id = id,
			Name = id,
			TargetKind = "self",
			DestinationMode = "current",
			BaseCost = baseCost,
			DurationTicks = durationTicks,
			Requires = new ActionRequirementDefinition
			{
				RequiredFacts = requiredFacts ?? [],
			},
			Effects = new ActionEffectDefinition
			{
				AddFacts = addFacts,
			},
		};

	sealed class StubGoapWorldQueryService : IWorldQueryService
	{
		readonly HashSet<string> currentFacts;
		readonly Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory;

		public StubGoapWorldQueryService(IEnumerable<string> currentFacts, Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
		{
			this.currentFacts = new HashSet<string>(currentFacts, StringComparer.OrdinalIgnoreCase);
			this.availableActionIdsFactory = availableActionIdsFactory;
		}

		public HashSet<string> BuildCurrentFacts(Entity agent)
			=> new(currentFacts, StringComparer.OrdinalIgnoreCase);

		public CandidateQuerySnapshot InspectCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts)
			=> new(BuildCandidates(agent, actions, currentFacts).ToArray(), Array.Empty<ActionDiagnostic>());

		public IEnumerable<ActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts)
		{
			var availableActionIds = availableActionIdsFactory(new HashSet<string>(currentFacts, StringComparer.OrdinalIgnoreCase));
			foreach (var actionId in availableActionIds)
			{
				var action = actions.First(definition => string.Equals(definition.Id, actionId, StringComparison.OrdinalIgnoreCase));
				yield return new ActionCandidate(
					action,
					Point.Zero,
					Point.Zero,
					null,
					action.BaseCost + action.DurationTicks,
					action.RequiredFacts,
					action.AddFacts,
					action.RemoveFacts);
			}
		}
	}

	static class TestSimulationDefinitions
	{
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = false,
		};

		public static SimulationDefinitionRegistry CreateRegistry(ActionDefinition[] actions, GoalDefinition[] goals)
		{
			var contentRoot = Path.Combine(Path.GetTempPath(), $"df-goap-tests-{Guid.NewGuid():N}");
			Directory.CreateDirectory(contentRoot);

			try
			{
				WriteDocument(Path.Combine(contentRoot, "items.json"), new ItemDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "actions.json"), new ActionDefinitionDocument { Actions = actions });
				WriteDocument(Path.Combine(contentRoot, "objects.json"), new WorldObjectDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "resources.json"), new ResourceNodeDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "agents.json"), new AgentDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "goals.json"), new GoalDefinitionDocument { Goals = goals });

				return SimulationDefinitionRegistry.LoadFromContentDirectory(contentRoot);
			}
			finally
			{
				Directory.Delete(contentRoot, recursive: true);
			}
		}

		static void WriteDocument<T>(string path, T document)
		{
			var json = JsonSerializer.Serialize(document, JsonOptions);
			File.WriteAllText(path, json);
		}
	}
}