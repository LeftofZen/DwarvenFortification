using Arch.Core;
using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP
{
	public interface IWorldQueryService
	{
		HashSet<string> BuildCurrentFacts(Entity agent);
		CandidateQuerySnapshot InspectCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts);
		IEnumerable<ActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts);
	}
}
