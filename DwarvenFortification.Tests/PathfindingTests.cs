using DwarvenFortification.Simulation.Pathfinding;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class PathfindingTests
{
	readonly AStarGraphPathfinder graphPathfinder = new();
	readonly EpPathFindingGridPathfinder gridPathfinder = new();

	[Test]
	public void GraphPathfinder_UsesLowestTotalEdgeCost()
	{
		var edges = new Dictionary<string, GraphEdge<string, string>[]>
		{
			["start"] = [new("direct", 10f), new("cheap-1", 2f)],
			["cheap-1"] = [new("cheap-2", 2f)],
			["cheap-2"] = [new("goal", 2f)],
			["direct"] = [new("goal", 1f)],
			["goal"] = [],
		};

		var request = new GraphPathRequest<string, string>(
			"start",
			"goal",
			node => edges[node],
			EstimateRemainingCost: static (from, to) => 0f,
			NodeComparer: StringComparer.OrdinalIgnoreCase);

		var found = graphPathfinder.TryFindPath(request, out var result);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.True);
			Assert.That(result.TotalCost, Is.EqualTo(6f));
			Assert.That(result.PathNodes, Is.EqualTo(new[] { "start", "cheap-1", "cheap-2", "goal" }));
		});
	}

	[Test]
	public void GraphPathfinder_ReturnsFalseWhenDestinationIsUnreachable()
	{
		var edges = new Dictionary<string, GraphEdge<string, string>[]>
		{
			["start"] = [new("middle", 1f)],
			["middle"] = [],
			["goal"] = [],
		};

		var request = new GraphPathRequest<string, string>(
			"start",
			"goal",
			node => edges[node],
			EstimateRemainingCost: static (from, to) => 0f,
			NodeComparer: StringComparer.OrdinalIgnoreCase);

		var found = graphPathfinder.TryFindPath(request, out var result);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.False);
			Assert.That(result, Is.EqualTo(default(GraphPathResult<string, string>)));
		});
	}

	[Test]
	public void GraphPathfinder_ReturnsSingleNodePathWhenStartMatchesDestination()
	{
		var request = new GraphPathRequest<string, string>(
			"start",
			"start",
			static _ => Array.Empty<GraphEdge<string, string>>(),
			EstimateRemainingCost: static (from, to) => 0f,
			NodeComparer: StringComparer.OrdinalIgnoreCase);

		var found = graphPathfinder.TryFindPath(request, out var result);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.True);
			Assert.That(result.TotalCost, Is.Zero);
			Assert.That(result.PathNodes, Is.EqualTo(new[] { "start" }));
		});
	}

	[Test]
	public void GraphPathfinder_ThrowsWhenEdgeExpansionDelegateIsNull()
	{
		var request = new GraphPathRequest<string, string>(
			"start",
			"goal",
			null!,
			EstimateRemainingCost: static (from, to) => 0f,
			NodeComparer: StringComparer.OrdinalIgnoreCase);

		Assert.That(
			() => graphPathfinder.TryFindPath(request, out _),
			Throws.ArgumentNullException);
	}

	[Test]
	public void GraphPathfinder_ThrowsWhenExpandedEdgesSequenceIsNull()
	{
		var request = new GraphPathRequest<string, string>(
			"start",
			"goal",
			static _ => null!,
			EstimateRemainingCost: static (from, to) => 0f,
			NodeComparer: StringComparer.OrdinalIgnoreCase);

		Assert.That(
			() => graphPathfinder.TryFindPath(request, out _),
			Throws.ArgumentNullException);
	}

	[Test]
	public void GridPathfinder_FindsSimplePathAcrossWalkableCells()
	{
		var request = new GridPathRequest(
			new bool[,]
			{
				{ true, true, true },
				{ true, true, true },
				{ true, true, true },
			},
			new Point(0, 0),
			new Point(2, 2));

		var found = gridPathfinder.TryFindPath(request, out var pathCells);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.True);
			Assert.That(pathCells, Is.Not.Empty);
			Assert.That(pathCells[0], Is.EqualTo(new Point(0, 0)));
			Assert.That(pathCells[^1], Is.EqualTo(new Point(2, 2)));
		});
	}

	[Test]
	public void GridPathfinder_ReturnsFalseWhenNoPathExists()
	{
		var request = new GridPathRequest(
			new bool[,]
			{
				{ true, false, true },
				{ false, false, false },
				{ true, false, true },
			},
			new Point(0, 0),
			new Point(2, 2));

		var found = gridPathfinder.TryFindPath(request, out var pathCells);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.False);
			Assert.That(pathCells, Is.Empty);
		});
	}

	[Test]
	public void GridPathfinder_ReturnsSingleCellPathWhenStartMatchesDestination()
	{
		var request = new GridPathRequest(
			new bool[,]
			{
				{ true, true },
				{ true, true },
			},
			new Point(1, 1),
			new Point(1, 1));

		var found = gridPathfinder.TryFindPath(request, out var pathCells);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.True);
			Assert.That(pathCells, Has.Count.GreaterThanOrEqualTo(1));
			Assert.That(pathCells[0], Is.EqualTo(new Point(1, 1)));
			Assert.That(pathCells[^1], Is.EqualTo(new Point(1, 1)));
		});
	}

	[Test]
	public void GridPathfinder_ReturnsFalseWhenCoordinatesAreOutOfBounds()
	{
		var request = new GridPathRequest(
			new bool[,]
			{
				{ true, true },
				{ true, true },
			},
			new Point(-1, 0),
			new Point(1, 1));

		var found = gridPathfinder.TryFindPath(request, out var pathCells);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.False);
			Assert.That(pathCells, Is.Empty);
		});
	}

	[Test]
	public void GridPathfinder_ThrowsWhenWalkableCellsAreNull()
	{
		var request = new GridPathRequest(
			null!,
			new Point(0, 0),
			new Point(0, 0));

		Assert.That(
			() => gridPathfinder.TryFindPath(request, out _),
			Throws.ArgumentNullException);
	}
}