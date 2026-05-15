using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP;
using DwarvenFortification.GOAP.Actions;
using DwarvenFortification.GOAP.Plans;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.World;
using ImGuiNET;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector4 = System.Numerics.Vector4;

namespace DwarvenFortification.UI
{
	public sealed class ImGuiSimulationUi
	{
		readonly SimulationDefinitionRegistry definitions;
		readonly ILogger logger;
		object boundObject;
		Entity? boundEntity;
		int taskTargetCellX;
		int taskTargetCellY;
		int taskDurationTicks = 60;
		bool replaceQueuedActions = true;
		string lastIssuedActionMessage = string.Empty;
		string lastPlanDiagramExportMessage = string.Empty;
		readonly Dictionary<string, int> selectedActionManifestationIndices = new(StringComparer.OrdinalIgnoreCase);
		int selectedDropInventoryItemIndex;

		public ImGuiSimulationUi(SimulationDefinitionRegistry definitions, ILogger logger)
		{
			this.definitions = definitions;
			this.logger = logger;
		}

		public MouseClickMode SelectedMouseClickMode { get; set; } = MouseClickMode.None;
		public CellType SelectedCellType { get; set; } = CellType.Dirt;
		public string SelectedOccupantId { get; set; } = string.Empty;
		public bool WantsMouseCapture { get; private set; }
		public Func<Entity, AgentActionRequest, string> ActionRequestHandler { get; set; }
		public Func<Entity, PlanningSnapshot> PlanningSnapshotProvider { get; set; }

		public void BindObject(object obj)
		{
			boundObject = obj;
			boundEntity = null;
		}

		public void BindEntity(Entity entity)
		{
			boundEntity = entity;
			boundObject = null;

			if (entity.IsAgent())
			{
				var position = entity.GetPosition();
				taskTargetCellX = Math.Max(0, position.X / 16);
				taskTargetCellY = Math.Max(0, position.Y / 16);
			}
		}

		public bool TryGetBoundEntity(out Entity entity)
		{
			if (boundEntity.HasValue)
			{
				entity = boundEntity.Value;
				return true;
			}

			entity = default;
			return false;
		}

		public void Render()
		{
			DrawToolsWindow();
			DrawInspectorWindow();
			DrawLogWindow();

			var io = ImGui.GetIO();
			WantsMouseCapture = io.WantCaptureMouse || io.WantCaptureKeyboard;
		}

		void DrawToolsWindow()
		{
			ImGui.SetNextWindowPos(new NumericsVector2(12, 12), ImGuiCond.Once);
			ImGui.SetNextWindowSize(new NumericsVector2(380, 520), ImGuiCond.Once);
			if (!ImGui.Begin("Simulation Tools"))
			{
				ImGui.End();
				return;
			}

			ImGui.Text("Mouse Mode");
			foreach (var mode in Enum.GetValues<MouseClickMode>())
			{
				var selected = SelectedMouseClickMode == mode;
				if (ImGui.RadioButton(mode.ToString(), selected))
				{
					SelectedMouseClickMode = mode;
				}

				if (mode != MouseClickMode.Select)
				{
					ImGui.SameLine();
				}
			}

			ImGui.Separator();
			ImGui.Text("Surface Palette");
			foreach (var cellType in Enum.GetValues<CellType>())
			{
				DrawPaletteEntry(
					$"surface-{cellType}",
					cellType.ToString(),
					GridCell.CellLookup[cellType],
					SelectedCellType == cellType && string.IsNullOrWhiteSpace(SelectedOccupantId),
					() =>
					{
						SelectedCellType = cellType;
						SelectedOccupantId = string.Empty;
						SelectedMouseClickMode = MouseClickMode.Paint;
					});
			}

			ImGui.Separator();
			ImGui.Text("Occupant Palette");
			foreach (var occupant in definitions.GetPaintableOccupants())
			{
				DrawPaletteEntry(
					$"occupant-{occupant.Id}",
					occupant.Name,
					occupant.Color,
					string.Equals(SelectedOccupantId, occupant.Id, StringComparison.OrdinalIgnoreCase),
					() =>
					{
						SelectedOccupantId = occupant.Id;
						SelectedMouseClickMode = MouseClickMode.Paint;
					});
			}

			ImGui.End();
		}

		void DrawInspectorWindow()
		{
			ImGui.SetNextWindowPos(new NumericsVector2(404, 12), ImGuiCond.Once);
			ImGui.SetNextWindowSize(new NumericsVector2(420, 640), ImGuiCond.Once);
			if (!ImGui.Begin("Inspector"))
			{
				ImGui.End();
				return;
			}

			if (boundEntity.HasValue)
			{
				DrawEntityInspector(boundEntity.Value);
			}
			else if (boundObject != null)
			{
				foreach (var line in ReflectObject(boundObject))
				{
					ImGui.TextUnformatted(line);
				}
			}
			else
			{
				ImGui.TextUnformatted("Nothing selected.");
			}

			ImGui.End();
		}

		void DrawEntityInspector(Entity entity)
		{
			var planningSnapshot = entity.IsAgent() && PlanningSnapshotProvider != null
				? PlanningSnapshotProvider(entity)
				: null;

			DrawEntityOverview(entity);
			DrawAgentActionControls(entity);

			if (!entity.IsAgent())
			{
				DrawEntityReflectionSection(entity);
				return;
			}

			DrawGoalSection(entity, planningSnapshot);
			DrawInventorySection(entity);
			DrawActionSection(entity, planningSnapshot);
			DrawPlanningSection(planningSnapshot);
			DrawEntityReflectionSection(entity);
		}

		void DrawEntityOverview(Entity entity)
		{
			foreach (var line in ReflectEntityOverview(entity))
			{
				ImGui.TextUnformatted(line);
			}

			ImGui.Separator();
		}

		void DrawGoalSection(Entity entity, PlanningSnapshot planningSnapshot)
		{
			if (!ImGui.CollapsingHeader("Current Goals", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (planningSnapshot == null || planningSnapshot.Goals.Count == 0)
			{
				ImGui.TextUnformatted("No goals available.");
				return;
			}

			foreach (var goal in planningSnapshot.Goals)
			{
				var status = goal.IsSatisfied
					? "Satisfied"
					: goal.CandidatePlan != null
						? "Candidate"
						: goal.IsEligible
							? "Eligible"
							: "Blocked";
				var statusColor = GetGoalStatusColor(goal);
				var hasCandidatePlan = goal.CandidatePlan != null;
				ImGui.PushID($"goal-{goal.Goal.Id}");
				ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
				if (ImGui.TreeNodeEx($"{goal.Goal.Name} [{status}]##goal", hasCandidatePlan ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
				{
					ImGui.PopStyleColor();
					ImGui.TextUnformatted($"Id: {goal.Goal.Id}");
					ImGui.TextUnformatted($"Priority: {goal.Goal.Priority}");
					ImGui.TextWrapped($"Desired facts: {FormatList(goal.Goal.DesiredFacts)}");
					if (goal.Goal.ForbiddenFacts.Length > 0)
					{
						ImGui.TextWrapped($"Forbidden facts: {FormatList(goal.Goal.ForbiddenFacts)}");
					}

					if (goal.MissingRequiredFacts.Count > 0)
					{
						ImGui.TextWrapped($"Missing required facts: {FormatList(goal.MissingRequiredFacts)}");
					}

					if (goal.ActiveBlockingFacts.Count > 0)
					{
						ImGui.TextWrapped($"Blocking facts: {FormatList(goal.ActiveBlockingFacts)}");
					}

					if (goal.CandidatePlan != null)
					{
						ImGui.TextUnformatted($"Candidate plan cost: {goal.CandidatePlan.Cost}");
						DrawPlanDiagramExportButtons(goal.CandidatePlan, $"goal-{goal.Goal.Id}");
						if (ImGui.TreeNode("Plan tree"))
						{
							DrawPlanNode(goal.CandidatePlan.Root);
							ImGui.TreePop();
						}

						ImGui.TextUnformatted("Flattened execution order:");
						for (var i = 0; i < goal.CandidatePlan.Steps.Count; ++i)
						{
							ImGui.BulletText(FormatCandidate(goal.CandidatePlan.Steps[i], i + 1));
						}
					}

					ImGui.TreePop();
				}
				else
				{
					ImGui.PopStyleColor();
				}

				ImGui.PopID();
			}
		}

		void DrawInventorySection(Entity entity)
		{
			if (!ImGui.CollapsingHeader("Inventory / Items", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (!entity.Has<InventoryComponent>())
			{
				ImGui.TextUnformatted("No inventory component.");
				return;
			}

			var inventory = entity.Get<InventoryComponent>();
			ImGui.TextUnformatted($"Capacity: {inventory.Items.Count}/{inventory.Capacity}");
			if (inventory.Items.Count == 0)
			{
				ImGui.TextUnformatted("Inventory empty.");
				return;
			}

			for (var i = 0; i < inventory.Items.Count; ++i)
			{
				var item = inventory.Items[i];
				var itemDefinition = item.Get<ItemDefinitionComponent>();
				var itemKind = itemDefinition.IsTool ? "Tool" : "Item";
				ImGui.BulletText($"{item.GetName()} [{item.GetItemDefinitionId()}] {itemKind} weight={itemDefinition.WeightKg:0.##}kg");
			}
		}

		void DrawActionSection(Entity entity, PlanningSnapshot planningSnapshot)
		{
			if (!ImGui.CollapsingHeader("Actions", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			var plannerFacts = planningSnapshot?.CurrentFacts != null
				? new HashSet<string>(planningSnapshot.CurrentFacts, StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var candidatesByAction = planningSnapshot?.ActionManifestations
				.GroupBy(candidate => candidate.Definition.Id, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase)
				?? new Dictionary<string, List<ActionCandidate>>(StringComparer.OrdinalIgnoreCase);

			DrawDirectActions(entity);

			foreach (var definition in definitions.GetActionDefinitions())
			{
				var missingPrerequisiteFacts = GetMissingPrerequisiteFacts(definition, plannerFacts);
				var isAvailableAction = missingPrerequisiteFacts.Count == 0;
				var actionColor = isAvailableAction ? ColorOk : ColorBlocked;
				var manifestations = candidatesByAction.TryGetValue(definition.Id, out var actionCandidates)
					? actionCandidates
					: null;
				var manifestationOptions = manifestations ?? [];
				var manifestationCount = manifestationOptions.Count;
				var header = $"{definition.Name} [{(isAvailableAction ? "Available" : "Unavailable")}] - {manifestationCount} manifestations";

				ImGui.PushID($"action-{definition.Id}");
				ImGui.PushStyleColor(ImGuiCol.Text, actionColor);
				if (ImGui.TreeNode(header))
				{
					ImGui.PopStyleColor();
					ImGui.TextUnformatted($"Action Id: {definition.Id}");
					ImGui.TextWrapped($"Target kind: {definition.TargetKind}; destination mode: {definition.DestinationMode}");
					if (missingPrerequisiteFacts.Count > 0)
					{
						ImGui.TextWrapped($"Missing prerequisite facts: {FormatList(missingPrerequisiteFacts)}");
					}
					else
					{
						ImGui.TextUnformatted("Prerequisite facts satisfied.");
					}

					if (manifestationCount == 0)
					{
						ImGui.TextUnformatted("No current action manifestations in the world.");
					}
					else
					{
						for (var i = 0; i < manifestationOptions.Count; ++i)
						{
							ImGui.BulletText(FormatCandidate(manifestationOptions[i], i + 1));
						}

						DrawManualActionControls(entity, definition, manifestationOptions);
					}

					ImGui.TreePop();
				}
				else
				{
					ImGui.PopStyleColor();
				}

				ImGui.PopID();
			}
		}

		void DrawDirectActions(Entity entity)
		{
			if (!ImGui.TreeNodeEx("Direct Actions", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			DrawMoveToCellDirectAction(entity);
			DrawWaitDirectAction(entity);
			DrawPickUpFirstItemDirectAction(entity);
			DrawPutDownInventoryDirectAction(entity);
			DrawDropItemDirectAction(entity);

			ImGui.TreePop();
		}

		void DrawMoveToCellDirectAction(Entity entity)
		{
			if (!ImGui.TreeNode("Move To Cell [Direct Action]"))
			{
				return;
			}

			ImGui.InputInt("Move Cell X", ref taskTargetCellX);
			ImGui.InputInt("Move Cell Y", ref taskTargetCellY);
			if (ImGui.Button("Queue Move To Cell Action"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.MoveToCell, tags: new[] { "manual", "direct-action" });
			}

			ImGui.TreePop();
		}

		void DrawWaitDirectAction(Entity entity)
		{
			if (!ImGui.TreeNode("Wait [Direct Action]"))
			{
				return;
			}

			ImGui.InputInt("Wait Duration Ticks", ref taskDurationTicks);
			taskDurationTicks = Math.Max(1, taskDurationTicks);
			if (ImGui.Button("Queue Wait Action"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.Wait, durationTicks: taskDurationTicks, tags: new[] { "manual", "direct-action" });
			}

			ImGui.TreePop();
		}

		void DrawPickUpFirstItemDirectAction(Entity entity)
		{
			if (!ImGui.TreeNode("Pick Up First Item [Direct Action]"))
			{
				return;
			}

			ImGui.InputInt("Pick Up Cell X", ref taskTargetCellX);
			ImGui.InputInt("Pick Up Cell Y", ref taskTargetCellY);
			if (ImGui.Button("Queue Pick Up Action"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PickUpFirstItemAtCell, tags: new[] { "manual", "direct-action" });
			}

			ImGui.TreePop();
		}

		void DrawPutDownInventoryDirectAction(Entity entity)
		{
			if (!ImGui.TreeNode("Put Down Inventory [Direct Action]"))
			{
				return;
			}

			ImGui.InputInt("Put Down Cell X", ref taskTargetCellX);
			ImGui.InputInt("Put Down Cell Y", ref taskTargetCellY);
			if (ImGui.Button("Queue Put Down Inventory Action"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PutDownInventoryAtCell, tags: new[] { "manual", "direct-action" });
			}

			ImGui.TreePop();
		}

		void DrawDropItemDirectAction(Entity entity)
		{
			if (!ImGui.TreeNode("Drop Item [Direct Action]"))
			{
				return;
			}

			var inventory = entity.GetInventory();
			if (inventory.Count == 0)
			{
				ImGui.TextUnformatted("Inventory empty.");
				ImGui.TreePop();
				return;
			}

			selectedDropInventoryItemIndex = Math.Clamp(selectedDropInventoryItemIndex, 0, inventory.Count - 1);
			var preview = FormatInventoryItemLabel(inventory[selectedDropInventoryItemIndex]);
			if (ImGui.BeginCombo("Inventory Item", preview))
			{
				for (var i = 0; i < inventory.Count; ++i)
				{
					var isSelected = i == selectedDropInventoryItemIndex;
					if (ImGui.Selectable(FormatInventoryItemLabel(inventory[i]), isSelected))
					{
						selectedDropInventoryItemIndex = i;
					}

					if (isSelected)
					{
						ImGui.SetItemDefaultFocus();
					}
				}

				ImGui.EndCombo();
			}

			if (ImGui.Button("Queue Drop Item Action"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.DropInventoryItem, selectedItem: inventory[selectedDropInventoryItemIndex], tags: new[] { "manual", "direct-action", "inventory" });
			}

			ImGui.TreePop();
		}

		void DrawManualActionControls(Entity entity, ActionDefinitionSnapshot definition, List<ActionCandidate> manifestations)
		{
			ImGui.Separator();
			ImGui.TextUnformatted("Issue action manually");

			if (ActionRequestHandler == null)
			{
				ImGui.TextUnformatted("Action request handler is not configured.");
				return;
			}

			if (manifestations.Count == 0)
			{
				ImGui.TextUnformatted("No queueable manifestations are currently available.");
				return;
			}

			selectedActionManifestationIndices.TryGetValue(definition.Id, out var selectedIndex);
			selectedIndex = Math.Clamp(selectedIndex, 0, manifestations.Count - 1);

			if (manifestations.Count == 1)
			{
				ImGui.TextWrapped($"Target: {FormatActionManifestationLabel(manifestations[0])}");
			}
			else
			{
				var preview = FormatActionManifestationLabel(manifestations[selectedIndex]);
				if (ImGui.BeginCombo("Action Target", preview))
				{
					for (var i = 0; i < manifestations.Count; ++i)
					{
						var isSelected = i == selectedIndex;
						if (ImGui.Selectable(FormatActionManifestationLabel(manifestations[i]), isSelected))
						{
							selectedIndex = i;
						}

						if (isSelected)
						{
							ImGui.SetItemDefaultFocus();
						}
					}

					ImGui.EndCombo();
				}
			}

			selectedActionManifestationIndices[definition.Id] = selectedIndex;
			var selectedCandidate = manifestations[selectedIndex];
			ImGui.TextWrapped($"Selected manifestation: {FormatActionManifestationLabel(selectedCandidate)}");
			if (ImGui.Button($"Queue Action##{definition.Id}"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.ExecuteAction, selectedCandidate: selectedCandidate, tags: new[] { "manual", "goap-action" });
			}
		}

		void DrawPlanningSection(PlanningSnapshot planningSnapshot)
		{
			if (!ImGui.CollapsingHeader("Goal Planning", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (planningSnapshot == null)
			{
				ImGui.TextUnformatted("Planning snapshot unavailable.");
				return;
			}

			ImGui.TextUnformatted($"Planner facts: {planningSnapshot.CurrentFacts.Count}");
			if (planningSnapshot.CurrentFacts.Count > 0 && ImGui.TreeNode("Planner facts"))
			{
				foreach (var fact in planningSnapshot.CurrentFacts)
				{
					ImGui.BulletText(fact);
				}

				ImGui.TreePop();
			}

			if (planningSnapshot.CandidatePlans.Count == 0)
			{
				ImGui.TextColored(ColorBlocked, "No candidate plans built.");
			}
			else
			{
				ImGui.TextColored(ColorPlanned, $"Candidate plans: {planningSnapshot.CandidatePlans.Count}");
				if (!string.IsNullOrWhiteSpace(lastPlanDiagramExportMessage))
				{
					ImGui.TextWrapped(lastPlanDiagramExportMessage);
				}

				if (ImGui.TreeNode("Candidate plan trees"))
				{
					for (var i = 0; i < planningSnapshot.CandidatePlans.Count; ++i)
					{
						var candidatePlan = planningSnapshot.CandidatePlans[i];
						if (ImGui.TreeNode($"{i + 1}. {candidatePlan.Goal.Name} (cost {candidatePlan.Cost})"))
						{
							DrawPlanDiagramExportButtons(candidatePlan, $"candidate-{i}");
							DrawPlanNode(candidatePlan.Root);
							ImGui.TreePop();
						}
					}
					ImGui.TreePop();
				}
			}

			var plannerFacts = new HashSet<string>(planningSnapshot.CurrentFacts, StringComparer.OrdinalIgnoreCase);
			var immediatelyAvailable = planningSnapshot.ActionManifestations
				.Where(candidate => GetCurrentStateBlockers(candidate, plannerFacts).Count == 0)
				.ToList();
			var deferredCandidates = planningSnapshot.ActionManifestations
				.Select(candidate => new CandidateBlockersView(candidate, GetCurrentStateBlockers(candidate, plannerFacts)))
				.Where(view => view.Blockers.Count > 0)
				.ToList();
			var rejectedDiagnostics = planningSnapshot.ActionDiagnostics
				.Where(diagnostic => diagnostic.Status == ActionDiagnosticStatus.Rejected)
				.ToList();

			ImGui.TextUnformatted($"Available now: {immediatelyAvailable.Count}");
			ImGui.TextUnformatted($"Deferred by planner facts: {deferredCandidates.Count}");
			ImGui.TextUnformatted($"Rejected by world query: {rejectedDiagnostics.Count}");

			if (ImGui.TreeNode($"Available manifestations ({immediatelyAvailable.Count})"))
			{
				for (var i = 0; i < immediatelyAvailable.Count; ++i)
				{
					ImGui.TextColored(ColorOk, FormatCandidate(immediatelyAvailable[i], i + 1));
				}

				ImGui.TreePop();
			}

			if (ImGui.TreeNode($"Deferred by planner facts ({deferredCandidates.Count})"))
			{
				for (var i = 0; i < deferredCandidates.Count; ++i)
				{
					var view = deferredCandidates[i];
					ImGui.TextColored(ColorDeferred, FormatCandidate(view.Candidate, i + 1));
					ImGui.TextWrapped($"Blocked by: {FormatList(view.Blockers)}");
				}

				ImGui.TreePop();
			}

			if (ImGui.TreeNode($"Rejected during world query ({rejectedDiagnostics.Count})"))
			{
				for (var i = 0; i < rejectedDiagnostics.Count; ++i)
				{
					ImGui.TextColored(ColorBlocked, FormatDiagnostic(rejectedDiagnostics[i], i + 1));
				}

				ImGui.TreePop();
			}
		}

		void DrawEntityReflectionSection(Entity entity)
		{
			if (!ImGui.CollapsingHeader("Raw Entity Data"))
			{
				return;
			}

			foreach (var line in ReflectEntity(entity))
			{
				ImGui.TextUnformatted(line);
			}
		}

		void DrawAgentActionControls(Entity entity)
		{
			if (!entity.IsAgent())
			{
				return;
			}

			ImGui.Separator();
			ImGui.Text("Direct Actions");

			ImGui.InputInt("Target Cell X", ref taskTargetCellX);
			ImGui.InputInt("Target Cell Y", ref taskTargetCellY);
			ImGui.InputInt("Duration Ticks", ref taskDurationTicks);
			taskDurationTicks = Math.Max(1, taskDurationTicks);
			ImGui.Checkbox("Replace queued actions", ref replaceQueuedActions);

			if (ImGui.Button("Queue Move To Cell"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.MoveToCell, tags: new[] { "manual", "direct-action" });
			}

			if (ImGui.Button("Queue Wait"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.Wait, durationTicks: taskDurationTicks, tags: new[] { "manual", "direct-action" });
			}

			if (ImGui.Button("Queue Pick Up First Item"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PickUpFirstItemAtCell, tags: new[] { "manual", "direct-action" });
			}

			if (ImGui.Button("Queue Put Down Inventory"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PutDownInventoryAtCell, tags: new[] { "manual", "direct-action" });
			}

			if (ImGui.Button("Clear Queue"))
			{
				entity.ClearQueuedActions();
				lastIssuedActionMessage = $"Cleared queued actions for {entity.GetName()}.";
			}

			if (!string.IsNullOrWhiteSpace(lastIssuedActionMessage))
			{
				ImGui.TextWrapped(lastIssuedActionMessage);
			}

			ImGui.Separator();
		}

		string RequestAction(
			Entity entity,
			string actionId,
			Point? targetCell = null,
			int? durationTicks = null,
			Entity selectedItem = default,
			ActionCandidate selectedCandidate = default,
			string[] tags = null)
		{
			if (ActionRequestHandler == null)
			{
				return "Action request handler is not configured.";
			}

			var metadata = new AgentActionMetadata(AgentActionSource.Manual, tags ?? new[] { "manual" });
			var actionRequest = new AgentActionRequest(
				actionId,
				targetCell ?? new Point(taskTargetCellX, taskTargetCellY),
				durationTicks ?? taskDurationTicks,
				replaceQueuedActions,
				selectedItem,
				selectedCandidate,
				metadata);

			return ActionRequestHandler(entity, actionRequest);
		}

		void DrawLogWindow()
		{
			ImGui.SetNextWindowPos(new NumericsVector2(836, 12), ImGuiCond.Once);
			ImGui.SetNextWindowSize(new NumericsVector2(500, 320), ImGuiCond.Once);
			if (!ImGui.Begin("Logs"))
			{
				ImGui.End();
				return;
			}

			foreach (var log in logger.Logs.TakeLast(20))
			{
				ImGui.TextUnformatted(log.ToString());
			}

			ImGui.End();
		}

		void DrawPaletteEntry(string id, string label, Color color, bool isSelected, Action onSelect)
		{
			ImGui.PushID(id);
			ImGui.ColorButton("##color", ToVector4(color), ImGuiColorEditFlags.NoTooltip, new NumericsVector2(18, 18));
			ImGui.SameLine();
			if (ImGui.Selectable(label, isSelected))
			{
				onSelect();
			}

			ImGui.PopID();
		}

		static NumericsVector4 ToVector4(Color color)
			=> new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

		static readonly NumericsVector4 ColorOk = new(0.52f, 0.82f, 0.56f, 1f);
		static readonly NumericsVector4 ColorPlanned = new(0.95f, 0.78f, 0.34f, 1f);
		static readonly NumericsVector4 ColorDeferred = new(0.96f, 0.65f, 0.27f, 1f);
		static readonly NumericsVector4 ColorBlocked = new(0.9f, 0.34f, 0.34f, 1f);

		static string FormatList(IEnumerable<string> values)
		{
			var materialized = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
			return materialized.Length == 0 ? "none" : string.Join(", ", materialized);
		}

		static string FormatCandidate(ActionCandidate candidate, int index)
		{
			var targetName = candidate.TargetEntity.HasValue && !candidate.TargetEntity.Value.Equals(default(Entity))
				? candidate.TargetEntity.Value.GetName()
				: "none";
			return $"{index}. action={candidate.Definition.Name} actionId={candidate.Definition.Id} cost={candidate.Cost} targetCell={candidate.TargetCell} destination={candidate.DestinationCell} target={targetName}";
		}

		static string FormatActionManifestationLabel(ActionCandidate candidate)
		{
			var targetName = candidate.TargetEntity.HasValue && !candidate.TargetEntity.Value.Equals(default(Entity))
				? candidate.TargetEntity.Value.GetName()
				: candidate.Definition.TargetKind;
			return $"{targetName} at {candidate.TargetCell} -> {candidate.DestinationCell}";
		}

		void DrawPlanDiagramExportButtons(Plan plan, string exportId)
		{
			ImGui.PushID(exportId);
			if (ImGui.Button("Copy Treemap Mermaid"))
			{
				CopyPlanDiagramToClipboard(plan, isTreemap: true);
			}

			ImGui.SameLine();
			if (ImGui.Button("Copy Gantt Mermaid"))
			{
				CopyPlanDiagramToClipboard(plan, isTreemap: false);
			}

			ImGui.PopID();
		}

		void CopyPlanDiagramToClipboard(Plan plan, bool isTreemap)
		{
			var diagramText = isTreemap
				? PlanMermaidDiagramBuilder.BuildTreemapDiagram(plan)
				: PlanMermaidDiagramBuilder.BuildGanttDiagram(plan);

			ImGui.SetClipboardText(diagramText);
			lastPlanDiagramExportMessage = $"Copied {(isTreemap ? "treemap" : "gantt")} Mermaid for plan '{plan.Goal.Name}' to clipboard.";
		}

		static void DrawPlanNode(PlanNode node)
		{
			var header = $"{node.Kind}: {node.Label} (cost {node.Cost})";
			if (node.Children.Count == 0)
			{
				ImGui.BulletText(header);
				return;
			}

			if (ImGui.TreeNode(header))
			{
				foreach (var child in node.Children)
				{
					DrawPlanNode(child);
				}

				ImGui.TreePop();
			}
		}

		static string FormatInventoryItemLabel(Entity item)
			=> $"{item.GetName()} [{item.GetItemDefinitionId()}]";

		static string FormatDiagnostic(ActionDiagnostic diagnostic, int index)
		{
			var targetCell = diagnostic.TargetCell?.ToString() ?? "n/a";
			var destinationCell = diagnostic.DestinationCell?.ToString() ?? "n/a";
			return $"{index}. action={diagnostic.Definition.Name} actionId={diagnostic.Definition.Id} target={diagnostic.TargetSummary} targetCell={targetCell} destination={destinationCell} reason={diagnostic.Reason}";
		}

		static NumericsVector4 GetGoalStatusColor(GoalDebugView goal)
			=> goal.IsSatisfied
				? ColorOk
				: goal.CandidatePlan != null
					? ColorPlanned
					: goal.IsEligible
						? ColorDeferred
						: ColorBlocked;

		static List<string> GetCurrentStateBlockers(ActionCandidate candidate, HashSet<string> plannerFacts)
		{
			var blockers = new List<string>();
			foreach (var requiredFact in candidate.RequiredFacts)
			{
				if (requiredFact.StartsWith('!'))
				{
					var blockedFact = requiredFact[1..];
					if (plannerFacts.Contains(blockedFact))
					{
						blockers.Add($"blocked by active fact {blockedFact}");
					}
				}
				else if (!plannerFacts.Contains(requiredFact))
				{
					blockers.Add($"missing fact {requiredFact}");
				}
			}

			return blockers;
		}

		static List<string> GetMissingPrerequisiteFacts(ActionDefinitionSnapshot definition, HashSet<string> plannerFacts)
		{
			var missingPrerequisiteFacts = new List<string>();
			AddMissingFacts(missingPrerequisiteFacts, plannerFacts, definition.RequiredFacts);

			foreach (var blockedFact in definition.BlockedByFacts.Where(plannerFacts.Contains))
			{
				missingPrerequisiteFacts.Add($"blocked:{blockedFact}");
			}

			return missingPrerequisiteFacts;
		}

		static void AddMissingFacts(List<string> missingFacts, HashSet<string> plannerFacts, IEnumerable<string> requiredFacts)
		{
			foreach (var fact in requiredFacts.Where(fact => !string.IsNullOrWhiteSpace(fact)))
			{
				if (fact.StartsWith('!'))
				{
					var blockedFact = fact[1..];
					if (plannerFacts.Contains(blockedFact))
					{
						missingFacts.Add($"blocked:{blockedFact}");
					}

					continue;
				}

				if (!plannerFacts.Contains(fact))
				{
					missingFacts.Add(fact);
				}
			}
		}

		readonly record struct CandidateBlockersView(ActionCandidate Candidate, List<string> Blockers);

		IEnumerable<string> ReflectObject(object obj)
		{
			var concreteType = obj.GetType();
			yield return $"=== ObjectType={concreteType} ===";

			var properties = concreteType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (properties.Length != 0)
			{
				yield return "--- Properties ---";
			}

			foreach (var property in properties)
			{
				yield return $" - {property.Name}={property.GetValue(obj)}";
				if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
				{
					foreach (var item in (IEnumerable)property.GetValue(obj, null))
					{
						yield return $"  * {item}";
					}
				}
			}

			var fields = concreteType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (fields.Length != 0)
			{
				yield return "--- Fields ---";
			}

			foreach (var field in fields)
			{
				yield return $" - {field.Name}={field.GetValue(obj)}";
			}
		}

		IEnumerable<string> ReflectEntityOverview(Entity entity)
		{
			yield return $"=== Entity={entity} ===";

			if (entity.Has<DefinitionIdentityComponent>())
			{
				var identity = entity.Get<DefinitionIdentityComponent>();
				yield return $" - Name={identity.Name}";
				yield return $" - Id={identity.Id}";
			}

			if (entity.Has<RuntimeTransformComponent>())
			{
				var transform = entity.Get<RuntimeTransformComponent>();
				yield return $" - Position={transform.Position}";
			}

			if (entity.Has<BodyComponent>())
			{
				var body = entity.Get<BodyComponent>();
				yield return $" - Body={body.Width}x{body.Height}";
			}

			if (entity.Has<AgentStatsComponent>())
			{
				var stats = entity.Get<AgentStatsComponent>();
				yield return $" - Strength={stats.Strength}";
				yield return $" - BaseSpeed={stats.BaseSpeed}";
				yield return $" - EffectiveSpeed={entity.GetSpeed()}";
			}
		}

		IEnumerable<string> ReflectEntity(Entity entity)
		{
			foreach (var line in ReflectEntityOverview(entity))
			{
				yield return line;
			}

			if (entity.Has<InventoryComponent>())
			{
				var inventory = entity.Get<InventoryComponent>();
				yield return $" - Inventory={inventory.Items.Count}/{inventory.Capacity}";
				foreach (var item in inventory.Items)
				{
					yield return $"  * {item.GetName()}";
				}
			}

			if (entity.Has<ActionQueueComponent>())
			{
				var actionQueue = entity.Get<ActionQueueComponent>();
				yield return $" - Actions={actionQueue.Actions.Count}";
				foreach (var queuedAction in actionQueue.Actions)
				{
					yield return $"  * {queuedAction}";
				}
			}

			if (entity.Has<ItemInstanceComponent>())
			{
				var item = entity.Get<ItemInstanceComponent>();
				yield return $" - ItemDefinition={item.DefinitionId}";
			}

			if (entity.Has<WorldObjectReferenceComponent>())
			{
				var worldObject = entity.Get<WorldObjectReferenceComponent>();
				yield return $" - WorldObjectDefinition={worldObject.DefinitionId}";
			}

			if (entity.Has<CellReferenceComponent>())
			{
				var cellReference = entity.Get<CellReferenceComponent>();
				yield return $" - Cell={cellReference.Cell}";
			}

			if (entity.Has<WorldObjectDefinitionComponent>())
			{
				var worldObjectDefinition = entity.Get<WorldObjectDefinitionComponent>();
				yield return $" - BlocksMovement={worldObjectDefinition.BlocksMovement}";
				yield return $" - Reservable={worldObjectDefinition.IsReservable}";
				yield return $" - AcceptedTags=[{string.Join(", ", worldObjectDefinition.AcceptedItemTags)}]";
			}

			if (entity.Has<ResourceNodeDefinitionComponent>())
			{
				var resourceNode = entity.Get<ResourceNodeDefinitionComponent>();
				yield return $" - YieldItemId={resourceNode.YieldItemId}";
				yield return $" - YieldCount={resourceNode.YieldCount}";
				yield return $" - BlocksMovement={resourceNode.BlocksMovement}";
			}
		}
	}
}