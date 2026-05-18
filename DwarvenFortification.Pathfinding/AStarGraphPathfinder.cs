namespace DwarvenFortification.Simulation.Pathfinding
{
	public sealed class AStarGraphPathfinder : IGraphPathfinder
	{
		public bool TryFindPath<TNode, TLabel>(GraphPathRequest<TNode, TLabel> request, out GraphPathResult<TNode, TLabel> path)
		{
			ArgumentNullException.ThrowIfNull(request.ExpandEdges);

			var comparer = request.NodeComparer ?? EqualityComparer<TNode>.Default;
			return TryFindPath(
				new GraphSearchRequest<TNode, TLabel>(
					request.StartNode,
					node => comparer.Equals(node, request.DestinationNode),
					request.ExpandEdges,
					request.EstimateRemainingCost is null
						? null
						: node => request.EstimateRemainingCost(node, request.DestinationNode),
					request.NodeComparer),
				out path);
		}

		public bool TryFindPath<TNode, TLabel>(GraphSearchRequest<TNode, TLabel> request, out GraphPathResult<TNode, TLabel> path)
		{
			ArgumentNullException.ThrowIfNull(request.ExpandEdges);
			ArgumentNullException.ThrowIfNull(request.IsGoalNode);

			var comparer = request.NodeComparer ?? EqualityComparer<TNode>.Default;
			var heuristic = request.EstimateRemainingCost ?? ZeroHeuristic;

			if (request.IsGoalNode(request.StartNode))
			{
				path = new GraphPathResult<TNode, TLabel>([request.StartNode], Array.Empty<TLabel>(), 0f);
				return true;
			}

			var frontier = new PriorityQueue<TNode, float>();
			var closed = new HashSet<TNode>(comparer);
			var bestCosts = new Dictionary<TNode, float>(comparer)
			{
				[request.StartNode] = 0f,
			};
			var cameFrom = new Dictionary<TNode, (TNode Parent, TLabel Edge)>(comparer);

			frontier.Enqueue(request.StartNode, heuristic(request.StartNode));

			while (frontier.TryDequeue(out var current, out _))
			{
				if (!closed.Add(current))
				{
					continue;
				}

				if (request.IsGoalNode(current))
				{
					var (pathNodes, pathEdges) = ReconstructPath(current, cameFrom);
					path = new GraphPathResult<TNode, TLabel>(pathNodes, pathEdges, bestCosts[current]);
					return true;
				}

				var currentCost = bestCosts[current];
				var edges = request.ExpandEdges(current);
				ArgumentNullException.ThrowIfNull(edges);

				foreach (var edge in edges)
				{
					if (edge.Cost < 0f)
					{
						throw new InvalidOperationException("A* does not support negative edge costs.");
					}

					var nextCost = currentCost + edge.Cost;
					if (bestCosts.TryGetValue(edge.Destination, out var bestKnownCost) && nextCost >= bestKnownCost)
					{
						continue;
					}

					cameFrom[edge.Destination] = (current, edge.Label);
					bestCosts[edge.Destination] = nextCost;
					frontier.Enqueue(edge.Destination, nextCost + heuristic(edge.Destination));
				}
			}

			path = default;
			return false;

			static float ZeroHeuristic(TNode _)
				=> 0f;
		}

		static (IReadOnlyList<TNode> Nodes, IReadOnlyList<TLabel> Edges) ReconstructPath<TNode, TLabel>(
			TNode destination,
			IReadOnlyDictionary<TNode, (TNode Parent, TLabel Edge)> cameFrom)
		{
			var nodes = new List<TNode> { destination };
			var edges = new List<TLabel>();
			var current = destination;

			while (cameFrom.TryGetValue(current, out var entry))
			{
				edges.Add(entry.Edge);
				nodes.Add(entry.Parent);
				current = entry.Parent;
			}

			nodes.Reverse();
			edges.Reverse();
			return (nodes, edges);
		}
	}
}