using System.Diagnostics.CodeAnalysis;

namespace DwarvenFortification.GOAP;

/// <summary>
/// A set of state conditions a <see cref="GoapAgent"/> is trying to reach.
/// </summary>
public class GoapGoal(string Name = null)
{
	public string Id { get; set; }

	/// <summary>
	/// An optional identifier.
	/// </summary>
	public string Name { get; set; } = Name;
	
	/// <summary>
	/// The conditions that must be met for the goal to be reached.
	/// </summary>
	public required List<GoapCondition> Objectives { get; set; } = [];
	
	/// <summary>
	/// Goals with higher priorities will be prioritised.<br/>
	/// By default, always returns 1.
	/// </summary>
	public Func<GoapAgent, double> Priority { get; set; } = _ => 1;
	
	/// <summary>
	/// Invalid goals will be ignored.
	/// </summary>
	/// <remarks>By default, always returns null.</remarks>
	public Func<GoapAgent, bool?> IsValidOverride { get; set; } = _ => null;

	/// <summary>
	/// Constructs a <see cref="GoapGoal"/> in-line.
	/// </summary>
	[SetsRequiredMembers]
	public GoapGoal(string Name, List<GoapCondition> Objectives) : this()
	{
		this.Name = Name;
		this.Objectives = Objectives;
	}

	/// <summary>
	/// Returns true if the goal is reached with the given states.
	/// </summary>
	public bool IsReached(IDictionary<object, object?> States)
		=> Objectives.All(Objective => Objective.IsMet(States));

	/// <summary>
	/// Returns true if the goal is reached with the given states, or closer to being reached than with the previous states.
	/// </summary>
	public bool IsReachedWithBestEffort(IDictionary<object, object?> States, IDictionary<object, object?> PreviousStates)
		=> Objectives.All(Objective => Objective.IsMetOrCloser(States, PreviousStates));
}
