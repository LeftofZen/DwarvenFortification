namespace DwarvenFortification.GOAP
{
	public readonly record struct GoapGoal(
		string Id,
		string Name,
		int Priority,
		string[] Effects,
		string[] Requirements);
}
