using Arch.Core;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public interface IGoapWorldQueryService
	{
		HashSet<string> BuildCurrentFacts(Entity agent);
		IEnumerable<GoapActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts);
	}
}