using System.Collections.Concurrent;

namespace DwarvenFortification.GOAP;

public class GoapAgent(string Name = null)
{
	public string Name { get; set; } = Name;

	public GoapWorldState States { get; set; }

	public List<GoapGoal> Goals { get; set; }

	public List<GoapAction> Actions { get; set; }

	public List<GoapSensor> Sensors { get; set; } = [];
		
	public object? GetState(string StateId)
		=> States.GetValueOrDefault(StateId)!;

	public void SetState(string StateId, GoapValue Value)
		=> States[StateId] = Value;

	public void SenseStates()
	{
		foreach (var sensor in Sensors)
		{
			SetState(sensor.StateId, sensor.GetValue());
		}
	}

	public IEnumerable<GoapGoal> CurrentGoals()
		=> Goals.OrderByDescending(Goal => Goal.Priority(this));

	public void AddGoal(GoapGoal goal)
		=> Goals.Add(goal);
}