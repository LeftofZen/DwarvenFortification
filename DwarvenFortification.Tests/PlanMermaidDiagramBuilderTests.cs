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
public sealed class PlanMermaidDiagramBuilderTests
{
	[Test]
	public void BuildTreemapDiagram_UsesGoalNodeAsHierarchyRoot()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagram = PlanMermaidDiagramBuilder.BuildTreemapDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("treemap-beta"));
			Assert.That(diagram, Does.Contain("\"Goal: Secure Food (cost 12)\""));
			Assert.That(diagram, Does.Contain("    \"Requirement: Require fact 'food.available' (cost 12)\""));
			Assert.That(diagram, Does.Contain("        \"Requirement: Require fact 'berries.found' (cost 5)\""));
			Assert.That(diagram, Does.Contain("            \"Action: forage (cost 5)\": 5"));
			Assert.That(diagram, Does.Contain("        \"Action: harvest-berries (cost 7)\": 7"));
		});
	}

	[Test]
	public void BuildGanttDiagram_UsesFlattenedActionSequence()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagram = PlanMermaidDiagramBuilder.BuildGanttDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("gantt"));
			Assert.That(diagram, Does.Contain("title Plan- Secure Food"));
			Assert.That(diagram, Does.Contain("dateFormat X"));
			Assert.That(diagram, Does.Contain("section Actions"));
			Assert.That(diagram, Does.Contain("1. forage :task1, 0, 4s"));
			Assert.That(diagram, Does.Contain("2. harvest-berries :task2, after task1, 6s"));
		});
	}

	[Test]
	public void BuildDiagrams_ProducesBothMermaidRepresentations()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagrams = PlanMermaidDiagramBuilder.BuildDiagrams(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagrams.Treemap, Does.StartWith("treemap-beta"));
			Assert.That(diagrams.Gantt, Does.StartWith("gantt"));
			Assert.That(diagrams.Gantt, Does.Contain("1. forage :task1, 0, 4s"));
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
			CreateActionDefinition("forage", ["berries.found"], durationTicks: 4),
			CreateActionDefinition("harvest-berries", ["food.available"], requiredFacts: ["berries.found"], durationTicks: 6),
		};

		var (planner, agent) = CreatePlanner(goals, actions, [], _ => ["forage", "harvest-berries"]);
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