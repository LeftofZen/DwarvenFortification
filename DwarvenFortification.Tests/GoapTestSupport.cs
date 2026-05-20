using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.GOAP;
using System.Text.Json;

namespace DwarvenFortification.Tests;

internal static class GoapTestSupport
{
	static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

	public static GoapAgent CreateAgent(
		GoalDefinition[] goals,
		ActionDefinition[] actions,
		IEnumerable<string> currentState)
	{
		var definitions = CreateRegistry(actions, goals);
		return SimulationGoapAgentFactory.CreateAgent(definitions, "test-agent", new HashSet<string>(currentState, StringComparer.OrdinalIgnoreCase));
	}

	public static ActionDefinition CreateActionDefinition(
		string id,
		string[]? requirements = null,
		string[]? effects = null,
		int durationTicks = 1,
		int baseCost = 1)
		=> new()
		{
			Id = id,
			Name = id,
			TargetKind = "self",
			DestinationMode = "current",
			BaseCost = baseCost,
			DurationTicks = durationTicks,
			Requirements = requirements ?? [],
			Effects = effects ?? [],
		};

	static SimulationDefinitionRegistry CreateRegistry(ActionDefinition[] actions, GoalDefinition[] goals)
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
			WriteDocument(Path.Combine(contentRoot, "facts.json"), new FactDefinitionDocument());
			WriteDocument(Path.Combine(contentRoot, "skills.json"), new SkillDefinitionDocument());

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
