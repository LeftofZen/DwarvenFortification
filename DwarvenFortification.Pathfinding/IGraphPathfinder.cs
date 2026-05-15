#nullable enable

namespace DwarvenFortification.Simulation.Pathfinding
{
	public readonly record struct GraphEdge<TNode>(
		TNode Destination,
		float Cost);

	public readonly record struct GraphPathRequest<TNode>(
		TNode StartNode,
		TNode DestinationNode,
		Func<TNode, IEnumerable<GraphEdge<TNode>>> ExpandEdges,
		Func<TNode, TNode, float>? EstimateRemainingCost = null,
		IEqualityComparer<TNode>? NodeComparer = null);

	public readonly record struct GraphSearchRequest<TNode>(
		TNode StartNode,
		Predicate<TNode> IsGoalNode,
		Func<TNode, IEnumerable<GraphEdge<TNode>>> ExpandEdges,
		Func<TNode, float>? EstimateRemainingCost = null,
		IEqualityComparer<TNode>? NodeComparer = null);

	public readonly record struct GraphPathResult<TNode>(
		IReadOnlyList<TNode> PathNodes,
		float TotalCost);

	public interface IGraphPathfinder
	{
		bool TryFindPath<TNode>(GraphPathRequest<TNode> request, out GraphPathResult<TNode> path);
		bool TryFindPath<TNode>(GraphSearchRequest<TNode> request, out GraphPathResult<TNode> path);
	}
}