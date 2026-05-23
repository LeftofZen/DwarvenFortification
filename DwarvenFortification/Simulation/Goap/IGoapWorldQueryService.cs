using Arch.Core;
using DwarvenFortification.GOAP;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

public interface IGoapWorldQueryService
{
	HashSet<string> BuildCurrentState(Entity agent);
	IReadOnlyDictionary<string, GoapValue> BuildNumericState(Entity agent);
	bool TryFindActionTarget(Entity agent, GoapAction action, HashSet<string> state,
		out Entity? targetEntity, out Point targetCell, out Point destinationCell, out string actionContext);
}