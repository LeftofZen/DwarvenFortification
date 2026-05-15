using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP
{
	public interface IDefinitionSource
	{
		IReadOnlyList<ActionDefinitionSnapshot> GetActionDefinitions();
		IReadOnlyList<Goal> GetGoalDefinitions();
	}
}
