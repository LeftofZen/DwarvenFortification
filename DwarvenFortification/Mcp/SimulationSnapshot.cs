#nullable enable
using System.Collections.Generic;

namespace DwarvenFortification.Mcp
{
	/// <summary>
	/// Immutable, thread-safe snapshot of the simulation captured on the game thread each tick.
	/// MCP tools read from the latest <see cref="SimulationSnapshotProvider.Current"/> without
	/// touching the live ECS world, eliminating cross-thread races between the game loop and
	/// the embedded HTTP server.
	/// </summary>
	public sealed record SimulationSnapshot(
		long Tick,
		long CapturedUnixMilliseconds,
		IReadOnlyList<AgentSnapshot> Agents,
		IReadOnlyList<WorldItemSnapshot> Items,
		IReadOnlyList<WorldStructureSnapshot> Structures);

	public sealed record AgentSnapshot(
		int EntityId,
		string Name,
		int X,
		int Y,
		IReadOnlyList<string> BooleanFacts,
		IReadOnlyDictionary<string, NumericFactSnapshot> NumericFacts,
		IReadOnlyList<GoalSnapshot> Goals,
		PlanSnapshot? ActivePlan,
		IReadOnlyList<ActionAvailabilitySnapshot> AvailableActions,
		IReadOnlyList<ActionAvailabilitySnapshot> UnavailableActions,
		IReadOnlyList<QueuedActionSnapshot> ActionQueue);

	public sealed record NumericFactSnapshot(double Value, double? Min, double? Max);

	public sealed record GoalSnapshot(
		string Id,
		string Name,
		double Priority,
		string Status,
		IReadOnlyList<string> UnsatisfiedObjectives,
		IReadOnlyList<string> UnsatisfiedEntryRequirements);

	public sealed record PlanSnapshot(
		string GoalId,
		string GoalName,
		double TotalCost,
		IReadOnlyList<PlanStepSnapshot> Steps);

	public sealed record PlanStepSnapshot(
		string ActionId,
		string ActionName,
		double Cost,
		string SuccessEffect);

	public sealed record ActionAvailabilitySnapshot(
		string ActionId,
		string ActionName,
		string SuccessEffect,
		IReadOnlyList<string> UnsatisfiedConditions);

	public sealed record QueuedActionSnapshot(
		string ActionId,
		string Name,
		string Status,
		int Progress,
		int Cost);

	public sealed record WorldItemSnapshot(
		string ItemDefinitionId,
		int X,
		int Y,
		IReadOnlyList<string> Tags);

	public sealed record WorldStructureSnapshot(
		string DefinitionId,
		int X,
		int Y,
		IReadOnlyList<string> Tags);
}
