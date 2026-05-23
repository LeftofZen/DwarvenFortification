namespace DwarvenFortification.GOAP;

public interface IGoapDefinitionSource
{
	IReadOnlyList<GoapAction> GetActionDefinitions();
	IReadOnlyList<GoapGoal> GetGoalDefinitions();
}
