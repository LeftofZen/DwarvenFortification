namespace DwarvenFortification.GOAP;

/// <summary>
/// Outcome of a runtime action execution.
/// </summary>
public enum GoapActionResult
{
	/// <summary>The action completed its task.</summary>
	Success,

	/// <summary>The action stopped but could continue in a future attempt (resumable).</summary>
	Interrupted,

	/// <summary>The action stopped and cannot complete (terminal).</summary>
	Failed,
}
