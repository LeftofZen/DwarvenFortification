using Microsoft.Xna.Framework;

namespace DwarvenFortification.GOAP.Actions
{

	public sealed class ActionDiagnostic
	{
		public ActionDiagnostic(
			ActionDefinitionSnapshot definition,
			ActionDiagnosticStatus status,
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

		public ActionDefinitionSnapshot Definition { get; }
		public ActionDiagnosticStatus Status { get; }
		public string Reason { get; }
		public Point? TargetCell { get; }
		public Point? DestinationCell { get; }
		public string TargetSummary { get; }
		public int? Cost { get; }
	}
}
