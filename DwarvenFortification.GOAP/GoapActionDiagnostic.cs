using Microsoft.Xna.Framework;

namespace DwarvenFortification.GOAP
{
	public enum GoapActionDiagnosticStatus
	{
		Available,
		Rejected,
	}

	public sealed class GoapActionDiagnostic
	{
		public GoapActionDiagnostic(
			GoapAction definition,
			GoapActionDiagnosticStatus status,
			string reason,
			Point? targetCell,
			Point? destinationCell,
			string targetSummary,
			int? cost)
		{
			Definition = definition;
			Status = status;
			Reason = reason;
			TargetCell = targetCell;
			DestinationCell = destinationCell;
			TargetSummary = targetSummary;
			Cost = cost;
		}

		public GoapAction Definition { get; }
		public GoapActionDiagnosticStatus Status { get; }
		public string Reason { get; }
		public Point? TargetCell { get; }
		public Point? DestinationCell { get; }
		public string TargetSummary { get; }
		public int? Cost { get; }
	}
}
