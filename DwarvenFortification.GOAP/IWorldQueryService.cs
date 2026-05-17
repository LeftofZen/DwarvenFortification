using Arch.Core;
using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP
{
	public interface IWorldQueryService
	{
		HashSet<string> BuildCurrentFacts(Entity agent);
		CandidateQuerySnapshot InspectCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts);
		IEnumerable<ActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts);

		/// <summary>
		/// Returns the effective runtime priority for a goal given the current agent state.
		/// The default implementation returns the static definition priority.
		/// Override to scale priority dynamically based on needs (hunger, thirst, rest, etc.).
		/// </summary>
		int GetEffectivePriority(Entity agent, Goal goal) => goal.Priority;
	}
}
