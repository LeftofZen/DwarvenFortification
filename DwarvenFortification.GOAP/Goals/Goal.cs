namespace DwarvenFortification.GOAP
{
	public readonly record struct Goal(
		string Id,
		string Name,
		int Priority,
		string[] DesiredFacts,
		string[] ForbiddenFacts,
		string[] RequiredFacts,
		string[] BlockedByFacts);
}
