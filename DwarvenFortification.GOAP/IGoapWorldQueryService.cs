using Arch.Core;

namespace DwarvenFortification.GOAP
{
	public interface IGoapWorldQueryService
	{
		HashSet<string> BuildCurrentState(Entity agent);
		GoapCandidateQuery InspectCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState);
		IEnumerable<GoapActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState);

		/// <summary>
		/// Returns the effective runtime priority for a goal given the current agent state.
		/// The default implementation returns the static definition priority.
		/// Override to scale priority dynamically based on needs (hunger, thirst, rest, etc.).
		/// </summary>
		int GetEffectivePriority(Entity agent, GoapGoal goal) => goal.Priority;
	}
}
