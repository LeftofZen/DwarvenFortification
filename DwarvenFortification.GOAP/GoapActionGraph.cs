namespace DwarvenFortification.GOAP;

/// <summary>
/// Precomputed bipartite index of an action set: maps each state id to the actions whose success
/// effect targets it (producers) and the actions whose preconditions or cost callbacks reference it
/// (consumers). Built once over a fully parametric-expanded action list and reused across every
/// <see cref="GoapPlan.Find"/> call so A* never scans the full action list during expansion.
///
/// The planner uses two operations on the graph:
/// <list type="bullet">
/// <item><see cref="BackwardClosure"/> — reverse-BFS from a goal's state ids through producer
/// edges and the resulting actions' precondition / cost-relevant state ids, yielding the
/// (typically small) set of actions on a connected path to the goal. A* iterates only this set
/// per node.</item>
/// <item><see cref="ProducersOf"/> / <see cref="ConsumersOf"/> — indexed lookups so callers never
/// enumerate the full action list.</item>
/// </list>
/// </summary>
public sealed class GoapActionGraph
{
	static readonly IReadOnlyList<GoapAction> Empty = Array.Empty<GoapAction>();

	readonly Dictionary<string, List<GoapAction>> producers;
	readonly Dictionary<string, List<GoapAction>> consumers;

	/// <summary>The fully-substituted action list this graph was built over.</summary>
	public IReadOnlyList<GoapAction> Actions { get; }

	public GoapActionGraph(IReadOnlyList<GoapAction> actions)
	{
		Actions = actions ?? Array.Empty<GoapAction>();
		producers = new Dictionary<string, List<GoapAction>>(StringComparer.OrdinalIgnoreCase);
		consumers = new Dictionary<string, List<GoapAction>>(StringComparer.OrdinalIgnoreCase);

		foreach (var action in Actions)
		{
			if (action is null) continue;

			var effectId = action.SuccessEffect?.StateId;
			if (!string.IsNullOrEmpty(effectId))
			{
				AddTo(producers, effectId, action);
			}

			foreach (var cond in action.Conditions ?? [])
			{
				if (!string.IsNullOrEmpty(cond.StateId))
				{
					AddTo(consumers, cond.StateId, action);
				}
			}

			foreach (var costId in action.CostStateIds ?? [])
			{
				if (!string.IsNullOrEmpty(costId))
				{
					AddTo(consumers, costId, action);
				}
			}
		}
	}

	/// <summary>Actions whose <see cref="GoapAction.SuccessEffect"/> targets <paramref name="stateId"/>.</summary>
	public IReadOnlyList<GoapAction> ProducersOf(string stateId)
		=> producers.TryGetValue(stateId, out var list) ? list : Empty;

	/// <summary>Actions whose preconditions (or declared cost-relevant state) reference <paramref name="stateId"/>.</summary>
	public IReadOnlyList<GoapAction> ConsumersOf(string stateId)
		=> consumers.TryGetValue(stateId, out var list) ? list : Empty;

	/// <summary>
	/// Reverse-BFS through producer edges starting from <paramref name="goalStateIds"/>. Each
	/// producer's preconditions and cost-relevant state ids are added to the frontier, so the
	/// closure follows every transitively-supporting action. Returns the connected action set;
	/// A* iterates only this set per node.
	/// </summary>
	public IReadOnlyList<GoapAction> BackwardClosure(IEnumerable<string> goalStateIds)
	{
		var actions = new HashSet<GoapAction>(ReferenceEqualityComparer.Instance);
		var visitedStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var frontier = new Queue<string>();

		foreach (var id in goalStateIds ?? [])
		{
			if (!string.IsNullOrEmpty(id))
			{
				frontier.Enqueue(id);
			}
		}

		while (frontier.Count > 0)
		{
			var sid = frontier.Dequeue();
			if (!visitedStates.Add(sid)) continue;
			if (!producers.TryGetValue(sid, out var producerList)) continue;

			foreach (var producer in producerList)
			{
				if (!actions.Add(producer)) continue;

				foreach (var cond in producer.Conditions ?? [])
				{
					if (!string.IsNullOrEmpty(cond.StateId))
					{
						frontier.Enqueue(cond.StateId);
					}
				}

				foreach (var costId in producer.CostStateIds ?? [])
				{
					if (!string.IsNullOrEmpty(costId))
					{
						frontier.Enqueue(costId);
					}
				}
			}
		}

		return actions.Count == 0 ? Empty : actions.ToList();
	}

	static void AddTo(Dictionary<string, List<GoapAction>> map, string key, GoapAction action)
	{
		if (!map.TryGetValue(key, out var list))
		{
			map[key] = list = new List<GoapAction>();
		}

		list.Add(action);
	}
}
