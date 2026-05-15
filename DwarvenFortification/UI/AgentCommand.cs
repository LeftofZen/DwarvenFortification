using Arch.Core;
using DwarvenFortification.GOAP;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace DwarvenFortification.UI
{
	public enum AgentActionSource
	{
		Autonomous,
		Manual,
		System,
	}

	public sealed class AgentActionMetadata
	{
		public AgentActionMetadata(AgentActionSource origin, params string[] tags)
		{
			Origin = origin;
			Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var tag in tags)
			{
				if (!string.IsNullOrWhiteSpace(tag))
				{
					Tags.Add(tag);
				}
			}
		}

		public AgentActionSource Origin { get; set; }
		public HashSet<string> Tags { get; }
	}

	public static class AgentActionIds
	{
		public const string MoveToCell = "action.move-to-cell";
		public const string Wait = "action.wait";
		public const string PickUpFirstItemAtCell = "action.pick-up-first-item";
		public const string PutDownInventoryAtCell = "action.put-down-inventory";
		public const string DropInventoryItem = "action.drop-inventory-item";
		public const string ExecuteAction = "action.execute";
	}

	public readonly record struct AgentActionRequest(
		string ActionId,
		Point TargetCell,
		int DurationTicks,
		bool ReplaceQueuedActions,
		Entity SelectedItem,
		GoapActionCandidate Candidate,
		AgentActionMetadata Metadata);
}