#nullable enable

namespace DwarvenFortification.Simulation.Pathfinding
{
	public readonly record struct GraphEdge<TNode, TLabel>(
		TNode Destination,
		float Cost,
		TLabel Label = default!);

	public readonly record struct GraphPathRequest<TNode, TLabel>(
		TNode StartNode,
		TNode DestinationNode,
		Func<TNode, IEnumerable<GraphEdge<TNode, TLabel>>> ExpandEdges,
		Func<TNode, TNode, float>? EstimateRemainingCost = null,
		IEqualityComparer<TNode>? NodeComparer = null);

	public readonly record struct GraphSearchRequest<TNode, TLabel>(
		TNode StartNode,
		Predicate<TNode> IsGoalNode,
		Func<TNode, IEnumerable<GraphEdge<TNode, TLabel>>> ExpandEdges,
		Func<TNode, float>? EstimateRemainingCost = null,
		IEqualityComparer<TNode>? NodeComparer = null);

	public readonly record struct GraphPathResult<TNode, TLabel>(
		IReadOnlyList<TNode> PathNodes,
		IReadOnlyList<TLabel> PathEdges,
		float TotalCost);

	public interface IGraphPathfinder
	{
		bool TryFindPath<TNode, TLabel>(GraphPathRequest<TNode, TLabel> request, out GraphPathResult<TNode, TLabel> path);
		bool TryFindPath<TNode, TLabel>(GraphSearchRequest<TNode, TLabel> request, out GraphPathResult<TNode, TLabel> path);
	}
}