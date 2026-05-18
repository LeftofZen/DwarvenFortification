using Arch.Core;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.GOAP
{
	public readonly record struct GoapActionCandidate(
		GoapAction Definition,
		Point TargetCell,
		Point DestinationCell,
		Entity? TargetEntity,
		int Cost,
		string[] Requirements,
		string[] Effects);
}
