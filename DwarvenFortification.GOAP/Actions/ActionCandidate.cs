using Arch.Core;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.GOAP.Actions
{
	public readonly record struct ActionCandidate(
		ActionDefinitionSnapshot Definition,
		Point TargetCell,
		Point DestinationCell,
		Entity? TargetEntity,
		int Cost,
		string[] RequiredFacts,
		string[] AddFacts,
		string[] RemoveFacts);
}
