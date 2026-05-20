using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.Actions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP;
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
		int selectedDropInventoryItemIndex;
		NumericsVector2 goapGraphPan = new(24f, 24f);
		float goapGraphZoom = 1f;
		string selectedGoapActionId = string.Empty;
		NumericsVector2 skillsGraphPan = new(24f, 24f);
		float skillsGraphZoom = 1f;
		string selectedSkillId = string.Empty;
		int productionOrderBatchCount = 1;
		int selectedProductionRecipeIndex;

		public ImGuiSimulationUi(SimulationDefinitionRegistry definitions, ILogger logger)
		{
			this.definitions = definitions;
			this.logger = logger;
		}

		public MouseClickMode SelectedMouseClickMode { get; set; } = MouseClickMode.None;
		public CellType SelectedCellType { get; set; } = CellType.Dirt;
		public string SelectedOccupantId { get; set; } = string.Empty;
		public string SelectedItemId { get; set; } = string.Empty;
		public bool WantsMouseCapture { get; private set; }
		public Func<Entity, AgentActionRequest, string> ActionRequestHandler { get; set; }
		public Func<Entity, GoapAgent> PlanningAgentProvider { get; set; }

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
					SelectedCellType == cellType && string.IsNullOrWhiteSpace(SelectedOccupantId) && string.IsNullOrWhiteSpace(SelectedItemId),
					() =>
					{
						SelectedCellType = cellType;
						SelectedOccupantId = string.Empty;
						SelectedItemId = string.Empty;
						SelectedMouseClickMode = MouseClickMode.Paint;
					});
			}

			ImGui.Separator();
			ImGui.Text("Occupant Palette");

			var paletteGroupOrder = new[] { "Ore Veins", "Minerals", "Trees", "Storage", "Workstations", "Furniture", "Structures", "Misc" };
			var paletteByGroup = definitions.GetPaintableOccupants()
				.GroupBy(o => o.Group)
				.ToDictionary(g => g.Key, g => g.ToList());

			foreach (var groupName in paletteGroupOrder)
			{
				if (!paletteByGroup.TryGetValue(groupName, out var groupEntries))
				{
					continue;
				}

				if (ImGui.CollapsingHeader(groupName, ImGuiTreeNodeFlags.DefaultOpen))
				{
					foreach (var occupant in groupEntries)
					{
						DrawPaletteEntry(
							$"occupant-{occupant.Id}",
							occupant.Name,
							occupant.Color,
							string.Equals(SelectedOccupantId, occupant.Id, StringComparison.OrdinalIgnoreCase),
							() =>
							{
								SelectedOccupantId = occupant.Id;
								SelectedItemId = string.Empty;
								SelectedMouseClickMode = MouseClickMode.Paint;
							});
					}
				}
			}

			foreach (var kvp in paletteByGroup.Where(kvp => !paletteGroupOrder.Contains(kvp.Key)))
			{
				if (ImGui.CollapsingHeader(kvp.Key, ImGuiTreeNodeFlags.DefaultOpen))
				{
					foreach (var occupant in kvp.Value)
					{
						DrawPaletteEntry(
							$"occupant-{occupant.Id}",
							occupant.Name,
							occupant.Color,
							string.Equals(SelectedOccupantId, occupant.Id, StringComparison.OrdinalIgnoreCase),
							() =>
							{
								SelectedOccupantId = occupant.Id;
								SelectedItemId = string.Empty;
								SelectedMouseClickMode = MouseClickMode.Paint;
							});
					}
				}
			}

			ImGui.Separator();
			ImGui.Text("Item Palette");

			var itemGroupOrder = new[] { "Tools", "Ores", "Fuel", "Ingots", "Logs", "Planks", "Parts & Components", "Food & Drink", "Weapons", "Knowledge", "Resources", "Misc" };
			var itemsByGroup = definitions.GetPaletteItems()
				.GroupBy(i => i.Group)
				.ToDictionary(g => g.Key, g => g.ToList());

			foreach (var groupName in itemGroupOrder)
			{
				if (!itemsByGroup.TryGetValue(groupName, out var itemEntries))
				{
					continue;
				}

				if (ImGui.CollapsingHeader($"{groupName}##item", ImGuiTreeNodeFlags.DefaultOpen))
				{
					foreach (var item in itemEntries)
					{
						DrawPaletteEntry(
							$"item-{item.Id}",
							item.Name,
							item.Color,
							string.Equals(SelectedItemId, item.Id, StringComparison.OrdinalIgnoreCase),
							() =>
							{
								SelectedItemId = item.Id;
								SelectedOccupantId = string.Empty;
								SelectedMouseClickMode = MouseClickMode.Paint;
							});
					}
				}
			}

			foreach (var kvp in itemsByGroup.Where(kvp => !itemGroupOrder.Contains(kvp.Key)))
			{
				if (ImGui.CollapsingHeader($"{kvp.Key}##item", ImGuiTreeNodeFlags.DefaultOpen))
				{
					foreach (var item in kvp.Value)
					{
						DrawPaletteEntry(
							$"item-{item.Id}",
							item.Name,
							item.Color,
							string.Equals(SelectedItemId, item.Id, StringComparison.OrdinalIgnoreCase),
							() =>
							{
								SelectedItemId = item.Id;
								SelectedOccupantId = string.Empty;
								SelectedMouseClickMode = MouseClickMode.Paint;
							});
					}
				}
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
			else if (boundObject is GridCell gridCell)
			{
				DrawCellInspector(gridCell);
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

		void DrawCellInspector(GridCell cell)
		{
			ImGui.TextUnformatted($"Cell Type: {cell.CellType}");
			ImGui.Separator();

			if (ImGui.CollapsingHeader("Occupants", ImGuiTreeNodeFlags.DefaultOpen))
			{
				if (cell.Occupants.Count == 0)
				{
					ImGui.TextUnformatted("  (none)");
				}
				else
				{
					foreach (var occupant in cell.Occupants)
					{
						var label = occupant.Has<DefinitionIdentityComponent>()
							? $"{occupant.Get<DefinitionIdentityComponent>().Name} [{occupant.Get<DefinitionIdentityComponent>().Id}]"
							: occupant.ToString();
						ImGui.TextUnformatted($"  {label}");
					}
				}
			}

			var floorItemCount = cell.ItemsInCell.Count;
			if (ImGui.CollapsingHeader($"Floor Items ({floorItemCount})##cell", ImGuiTreeNodeFlags.DefaultOpen))
			{
				if (floorItemCount == 0)
				{
					ImGui.TextUnformatted("  (none)");
				}
				else
				{
					foreach (var item in cell.ItemsInCell)
					{
						var itemId = item.GetItemDefinitionId();
						definitions.TryGetItemDisplayName(itemId, out var displayName);
						ImGui.PushID($"floor-item-{itemId}-{item.Id}");
						if (ImGui.TreeNode($"{displayName} [{itemId}]"))
						{
							DrawItemEffects(item);
							ImGui.TreePop();
						}
						ImGui.PopID();
					}
				}
			}
		}

		void DrawEntityInspector(Entity entity)
		{
			var goapAgent = entity.IsAgent() && PlanningAgentProvider != null
				? PlanningAgentProvider(entity)
				: null;

			if (!entity.IsAgent())
			{
				DrawSiteAndMachinePanel(entity);
				DrawEntityReflectionSection(entity);
				return;
			}

			DrawEntityOverview(entity);
			DrawBodyNutritionSection(entity);
			DrawInventorySection(entity);
			DrawAgentActionControls(entity);
			DrawGoalSection(entity, goapAgent);
			DrawPlanningSection(goapAgent);
			DrawActivePlanSection(entity);
			DrawActionSection(entity, goapAgent);
			DrawAgentSkillsSection(entity);
			DrawSkillCatalogSection(entity, goapAgent);
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

		void DrawGoalSection(Entity entity, GoapAgent goapAgent)
		{
			if (!ImGui.CollapsingHeader("Current Goals", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (goapAgent == null || goapAgent.Goals.Count == 0)
			{
				ImGui.TextUnformatted("No goals available.");
				return;
			}

			foreach (var goal in BuildGoalInspections(goapAgent))
			{
				var status = goal.IsReached
					? "Satisfied"
					: goal.Plan != null
						? "Candidate"
						: goal.IsValid
							? "Eligible"
							: "Blocked";
				var statusColor = GetGoalStatusColor(goal);
				var hasCandidatePlan = goal.Plan != null;
				ImGui.PushID($"goal-{goal.Goal.Id}");
				ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
				if (ImGui.TreeNodeEx($"[{goal.Priority:0.##}] {goal.Goal.Name} [{status}]##goal", hasCandidatePlan ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
				{
					ImGui.PopStyleColor();
					ImGui.TextUnformatted($"Id: {goal.Goal.Id}");
					ImGui.TextUnformatted($"Priority: {goal.Priority:0.##}");

					ImGui.TextWrapped($"Desired facts: {FormatList(goal.Goal.DesiredFacts)}");

					if (goal.MissingRequiredFacts.Count > 0)
					{
						ImGui.TextWrapped($"Missing required facts: {FormatList(goal.MissingRequiredFacts)}");
					}

					if (goal.Plan != null)
					{
						ImGui.TextUnformatted($"Candidate plan cost: {CalculatePlanCost(goal.Plan):0.##}");

						ImGui.TextUnformatted("Flattened execution order:");
						for (var i = 0; i < goal.Plan.Actions.Count; ++i)
						{
							var step = goal.Plan.Actions[i];
							ImGui.BulletText($"{i + 1}. {step.Name} (cost {step.Cost(goal.Plan.Agent):0.##})");
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
				ImGui.PushID($"inv-item-{i}");
				if (ImGui.TreeNode($"{item.GetName()} [{item.GetItemDefinitionId()}] {itemKind} {itemDefinition.WeightKg:0.##}kg"))
				{
					DrawItemEffects(item);
					ImGui.TreePop();
				}
				ImGui.PopID();
			}
		}

		void DrawAgentSkillsSection(Entity entity)
		{
			if (!ImGui.CollapsingHeader("Skills", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (!entity.Has<AgentSkillsComponent>())
			{
				ImGui.TextUnformatted("No skills component.");
				return;
			}

			var agentSkills = entity.Get<AgentSkillsComponent>().Skills ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			var skillDefs = definitions.GetSkillDefinitions();
			if (skillDefs.Count == 0)
			{
				ImGui.TextUnformatted("No skill definitions loaded.");
				return;
			}

			var layout = BuildSkillGraphLayout(skillDefs, agentSkills);
			DrawNodeGraphCanvas("AgentSkillsGraph", layout, ref skillsGraphPan, ref skillsGraphZoom, ref selectedSkillId, 380f);
			ImGui.TextDisabled("Drag to pan, scroll to zoom, click a node for details.");

			if (!string.IsNullOrWhiteSpace(selectedSkillId) && !selectedSkillId.StartsWith("group:", StringComparison.OrdinalIgnoreCase))
			{
				var selectedDef = skillDefs.FirstOrDefault(s => string.Equals(s.Id, selectedSkillId, StringComparison.OrdinalIgnoreCase));
				if (selectedDef != null && !string.IsNullOrEmpty(selectedDef.Id))
				{
					var level = agentSkills.TryGetValue(selectedDef.Id, out var l) ? l : 1;
					var tierColor = GetSkillTierColor(level);
					var durationPct = (1.0f - (level - 1f) * 0.5f / 99f) * 100f;
					var yieldMult = 1.0f + (level - 1f) / 99f;
					ImGui.Separator();
					ImGui.TextUnformatted($"{selectedDef.Name}  [{selectedDef.Id}]");
					ImGui.TextUnformatted($"{selectedDef.Category} \u203a {selectedDef.Group}");
					ImGui.TextWrapped(selectedDef.Description);
					ImGui.TextColored(tierColor, $"Level {level}  \u2014  {GetSkillTierName(level)}");
					ImGui.TextUnformatted($"Action duration: {durationPct:0.#}% of base  |  Yield: {yieldMult:0.##}x");
				}
			}
		}

		void DrawBodyNutritionSection(Entity entity)
		{
			if (!ImGui.CollapsingHeader("Body / Nutrition", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (!entity.Has<BodyNutritionComponent>())
			{
				ImGui.TextUnformatted("No body nutrition component.");
				return;
			}

			var nutrition = entity.Get<BodyNutritionComponent>();
			DrawNutrientLine("Carbohydrates", nutrition.CarbohydratesCurrent, nutrition.CarbohydratesMax, entity.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Carbohydrates));
			DrawNutrientLine("Protein", nutrition.ProteinCurrent, nutrition.ProteinMax, entity.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Protein));
			DrawNutrientLine("Fat", nutrition.FatCurrent, nutrition.FatMax, entity.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Fat));
			DrawNutrientLine("Sugar", nutrition.SugarCurrent, nutrition.SugarMax, entity.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Sugar));
			DrawNutrientLine("Hydration (L)", nutrition.HydrationCurrentLiters, nutrition.HydrationMaxLiters, entity.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Hydration));

			ImGui.Separator();
			var energyRatio = Math.Clamp(entity.GetMetabolicEnergyRatio(), 0f, 1f);
			ImGui.PushStyleColor(ImGuiCol.PlotHistogram, energyRatio < 0.3f ? ColorBlocked : ColorOk);
			ImGui.ProgressBar(energyRatio, new NumericsVector2(-1, 0), $"Metabolic energy: {energyRatio * 100f:0.#}%");
			ImGui.PopStyleColor();

			var impairedSystems = entity.GetImpairedSystems().ToArray();
			if (impairedSystems.Length == 0)
			{
				ImGui.TextColored(ColorOk, "All body systems operational.");
			}
			else
			{
				ImGui.TextColored(ColorBlocked, $"Impaired systems: {impairedSystems.Length}");
				foreach (var system in impairedSystems)
				{
					ImGui.BulletText(system);
				}
			}
		}

		void DrawItemEffects(Entity item)
		{
			if (!item.Has<ItemDefinitionComponent>())
			{
				return;
			}

			var def = item.Get<ItemDefinitionComponent>();
			var tags = item.Has<TagCollectionComponent>() ? item.Get<TagCollectionComponent>().Values : [];

			ImGui.TextDisabled($"Weight: {def.WeightKg:0.##} kg  |  {(def.Stackable ? "Stackable" : "Non-stackable")}");

			if (tags.Length > 0)
			{
				ImGui.TextDisabled($"Tags: {string.Join(", ", tags)}");
			}

			var n = def.Nutrition;
			var hasNutrition = n.CarbohydratesGrams > 0f || n.ProteinGrams > 0f || n.FatGrams > 0f
				|| n.SugarGrams > 0f || n.FiberGrams > 0f || n.FluidLiters > 0f;
			if (hasNutrition)
			{
				ImGui.TextUnformatted("Nutrition on consume:");
				if (n.CarbohydratesGrams > 0f)
				{
					ImGui.BulletText($"Carbohydrates: +{n.CarbohydratesGrams:0.##}g");
				}

				if (n.ProteinGrams > 0f)
				{
					ImGui.BulletText($"Protein: +{n.ProteinGrams:0.##}g");
				}

				if (n.FatGrams > 0f)
				{
					ImGui.BulletText($"Fat: +{n.FatGrams:0.##}g");
				}

				if (n.SugarGrams > 0f)
				{
					ImGui.BulletText($"Sugar: +{n.SugarGrams:0.##}g");
				}

				if (n.FiberGrams > 0f)
				{
					ImGui.BulletText($"Fibre: +{n.FiberGrams:0.##}g");
				}

				if (n.FluidLiters > 0f)
				{
					ImGui.BulletText($"Hydration: +{n.FluidLiters:0.##}L");
				}
			}

			var enabledActions = definitions.GetActionDefinitions()
				.Where(action => action.RequiredFacts.Any(fact =>
					Facts.TryGetHasItemTag(fact, out var tag)
					&& tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
				.Select(a => a.Name)
				.ToArray();
			if (enabledActions.Length > 0)
			{
				ImGui.TextUnformatted("Enables actions:");
				foreach (var actionName in enabledActions)
				{
					ImGui.BulletText(actionName);
				}
			}

			if (def.ThrowRange > 0f)
			{
				ImGui.BulletText($"Throw range: {def.ThrowRange:0.#} m");
			}

			if (def.LearnedFacts.Length > 0)
			{
				ImGui.TextUnformatted("Teaches:");
				foreach (var fact in def.LearnedFacts)
				{
					ImGui.BulletText(fact);
				}
			}
		}

		void DrawNutrientLine(string label, float current, float max, bool isLow)
		{
			var ratio = max <= 0f ? 0f : Math.Clamp(current / max, 0f, 1f);
			ImGui.PushStyleColor(ImGuiCol.PlotHistogram, isLow ? ColorBlocked : ColorOk);
			ImGui.ProgressBar(ratio, new NumericsVector2(-1, 0), $"{label}: {current:0.##}/{max:0.##} ({ratio * 100f:0.#}%)");
			ImGui.PopStyleColor();
		}

		void DrawSkillCatalogSection(Entity entity, GoapAgent goapAgent)
		{
			if (!ImGui.CollapsingHeader("Skills / Action Catalog", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			var plannerFacts = GetCurrentFacts(goapAgent);
			var candidatesByAction = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

			ImGui.TextUnformatted("Direct actions");
			ImGui.BulletText("Move To Cell");
			ImGui.BulletText("Wait");
			ImGui.BulletText("Pick Up First Item");
			ImGui.BulletText("Put Down Inventory");
			ImGui.BulletText("Drop Inventory Item");

			ImGui.Separator();
			ImGui.TextUnformatted("GOAP actions");
			DrawGoapActionGraph(definitions.GetActionDefinitions(), plannerFacts, candidatesByAction);
		}

		void DrawGoapActionGraph(
			IReadOnlyList<SimulationGoapAction> definitions,
			HashSet<string> plannerFacts,
			Dictionary<string, int> candidatesByAction)
		{
			if (definitions.Count == 0)
			{
				ImGui.TextUnformatted("No GOAP actions defined.");
				return;
			}

			var graph = BuildActionGraphLayout(definitions, plannerFacts, candidatesByAction);
			DrawNodeGraphCanvas("GoapActionGraph", graph, ref goapGraphPan, ref goapGraphZoom, ref selectedGoapActionId, 420f);
			ImGui.TextColored(ColorOk, "Available");
			ImGui.SameLine();
			ImGui.TextColored(ColorDeferred, "Deferred");
			ImGui.SameLine();
			ImGui.TextColored(ColorBlocked, "Blocked");
			ImGui.SameLine();
			ImGui.TextDisabled("Drag empty space to pan, wheel to zoom, click a node for full details.");

			if (!string.IsNullOrWhiteSpace(selectedGoapActionId))
			{
				var selectedDefinition = definitions.FirstOrDefault(definition => string.Equals(definition.Id, selectedGoapActionId, StringComparison.OrdinalIgnoreCase));
				if (!string.IsNullOrWhiteSpace(selectedDefinition.Id))
				{
					DrawSelectedGoapActionDetails(selectedDefinition, plannerFacts, candidatesByAction);
				}
			}
		}

		void DrawNodeGraphCanvas(
			string childId,
			ActionGraphLayout layout,
			ref NumericsVector2 pan,
			ref float zoom,
			ref string selectedId,
			float childHeight = 420f)
		{
			ImGui.BeginChild(childId, new NumericsVector2(0, childHeight), true);
			var origin = ImGui.GetCursorScreenPos();
			var availableSize = ImGui.GetContentRegionAvail();
			var canvasSize = new NumericsVector2(Math.Max(availableSize.X, 240f), Math.Max(availableSize.Y, childHeight - 8f));
			ImGui.InvisibleButton($"{childId}Canvas", canvasSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
			var drawList = ImGui.GetWindowDrawList();
			var canvasMin = origin;
			var canvasMax = origin + canvasSize;
			var hoveredCanvas = ImGui.IsItemHovered();
			var io = ImGui.GetIO();

			if (hoveredCanvas && Math.Abs(io.MouseWheel) > float.Epsilon)
			{
				var mouseCanvas = io.MousePos - origin;
				var worldBeforeZoom = (mouseCanvas - pan) / zoom;
				zoom = Math.Clamp(zoom + (io.MouseWheel * 0.1f), 0.5f, 2.25f);
				pan = mouseCanvas - (worldBeforeZoom * zoom);
			}

			ActionGraphNode? hoveredNode = null;
			if (hoveredCanvas)
			{
				var mouseWorld = (io.MousePos - origin - pan) / zoom;
				var hitNodes = layout.Nodes.Where(node => Contains(node.Bounds, mouseWorld)).ToList();
				hoveredNode = hitNodes.Count == 0 ? null : hitNodes[^1];

				if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && hoveredNode == null)
				{
					pan += io.MouseDelta;
				}

				if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hoveredNode.HasValue)
				{
					selectedId = hoveredNode.Value.Id;
				}
			}

			drawList.AddRectFilled(canvasMin, canvasMax, ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.08f, 0.09f, 0.12f, 1f)), 6f);
			drawList.AddRect(canvasMin, canvasMax, ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.2f, 0.22f, 0.27f, 1f)), 6f, ImDrawFlags.None, 1.5f);

			foreach (var edge in layout.Edges)
			{
				var from = origin + pan + (edge.From * zoom);
				var to = origin + pan + (edge.To * zoom);
				drawList.AddLine(from, to, edge.Color, 2f);
				if (!string.IsNullOrWhiteSpace(edge.Label))
				{
					var labelPosition = ((from + to) / 2f) + new NumericsVector2(0f, -10f);
					var labelSize = ImGui.CalcTextSize(edge.Label);
					var labelMin = labelPosition - new NumericsVector2((labelSize.X / 2f) + 4f, 2f);
					var labelMax = labelPosition + new NumericsVector2((labelSize.X / 2f) + 4f, labelSize.Y + 2f);
					drawList.AddRectFilled(labelMin, labelMax, ToU32(new NumericsVector4(0.09f, 0.1f, 0.13f, 0.95f)), 4f);
					drawList.AddText(labelPosition - new NumericsVector2(labelSize.X / 2f, 0f), ToU32(new NumericsVector4(0.85f, 0.88f, 0.92f, 1f)), edge.Label);
				}
			}

			foreach (var node in layout.Nodes)
			{
				var min = origin + pan + (node.Bounds.Min * zoom);
				var max = origin + pan + (node.Bounds.Max * zoom);
				var isSelected = string.Equals(selectedId, node.Id, StringComparison.OrdinalIgnoreCase);
				var borderColor = isSelected ? ToU32(new NumericsVector4(0.98f, 0.98f, 0.99f, 1f)) : node.BorderColor;
				var borderThickness = isSelected ? 3f : 2f;
				drawList.AddRectFilled(min, max, node.FillColor, 6f);
				drawList.AddRect(min, max, borderColor, 6f, ImDrawFlags.None, borderThickness);
				drawList.AddText(min + new NumericsVector2(10, 8), node.TextColor, node.Title);

				if (node.ProgressRatio > 0f && node.ProgressBarColor != 0u)
				{
					var barMin = min + new NumericsVector2(10, 26);
					var barMax = new NumericsVector2(max.X - 10, min.Y + 34);
					drawList.AddRectFilled(barMin, barMax, ToU32(new NumericsVector4(0.10f, 0.11f, 0.14f, 1f)), 3f);
					var fillWidth = (barMax.X - barMin.X) * node.ProgressRatio;
					if (fillWidth > 0f)
					{
						drawList.AddRectFilled(barMin, new NumericsVector2(barMin.X + fillWidth, barMax.Y), node.ProgressBarColor, 3f);
					}

					drawList.AddText(min + new NumericsVector2(10, 40), node.SubtitleColor, node.Subtitle);
					if (!string.IsNullOrWhiteSpace(node.Detail))
					{
						drawList.AddText(min + new NumericsVector2(10, 58), node.DetailColor, node.Detail);
					}
				}
				else
				{
					drawList.AddText(min + new NumericsVector2(10, 30), node.SubtitleColor, node.Subtitle);
					if (!string.IsNullOrWhiteSpace(node.Detail))
					{
						drawList.AddText(min + new NumericsVector2(10, 48), node.DetailColor, node.Detail);
					}
				}
			}

			ImGui.EndChild();
		}

		ActionGraphLayout BuildActionGraphLayout(
			IReadOnlyList<SimulationGoapAction> definitions,
			HashSet<string> plannerFacts,
			Dictionary<string, int> candidatesByAction)
		{
			const float nodeWidth = 220f;
			const float nodeHeight = 78f;
			const float horizontalGap = 52f;
			const float verticalGap = 24f;
			const float margin = 20f;

			var producersByFact = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var dependencyFactsByPair = new Dictionary<(string SourceId, string TargetId), HashSet<string>>();
			foreach (var definition in definitions)
			{
				foreach (var fact in definition.EffectFacts.Where(fact => !string.IsNullOrWhiteSpace(fact)))
				{
					if (!producersByFact.TryGetValue(fact, out var producers))
					{
						producers = new List<string>();
						producersByFact[fact] = producers;
					}

					producers.Add(definition.Id);
				}
			}

			var dependenciesByAction = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			foreach (var definition in definitions)
			{
				var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var fact in definition.RequiredFacts.Where(fact => !string.IsNullOrWhiteSpace(fact) && !fact.StartsWith('!')))
				{
					if (!producersByFact.TryGetValue(fact, out var producers))
					{
						continue;
					}

					foreach (var producer in producers)
					{
						if (!string.Equals(producer, definition.Id, StringComparison.OrdinalIgnoreCase))
						{
							dependencies.Add(producer);
							var key = (producer, definition.Id);
							if (!dependencyFactsByPair.TryGetValue(key, out var facts))
							{
								facts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
								dependencyFactsByPair[key] = facts;
							}

							facts.Add(fact);
						}
					}
				}

				dependenciesByAction[definition.Id] = [.. dependencies.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)];
			}

			var depthCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			var activePath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			int GetDepth(string actionId)
			{
				if (depthCache.TryGetValue(actionId, out var cached))
				{
					return cached;
				}

				if (!activePath.Add(actionId))
				{
					return 0;
				}

				var depth = 0;
				if (dependenciesByAction.TryGetValue(actionId, out var dependencies) && dependencies.Count > 0)
				{
					depth = dependencies.Max(GetDepth) + 1;
				}

				activePath.Remove(actionId);
				depthCache[actionId] = depth;
				return depth;
			}

			var columnGroups = definitions
				.GroupBy(definition => GetDepth(definition.Id))
				.OrderBy(group => group.Key)
				.ToList();

			var nodes = new List<ActionGraphNode>();
			var nodesById = new Dictionary<string, ActionGraphNode>(StringComparer.OrdinalIgnoreCase);
			var maxRows = 0;
			for (var columnIndex = 0; columnIndex < columnGroups.Count; ++columnIndex)
			{
				var column = columnGroups[columnIndex].OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase).ToList();
				maxRows = Math.Max(maxRows, column.Count);
				for (var rowIndex = 0; rowIndex < column.Count; ++rowIndex)
				{
					var definition = column[rowIndex];
					var missingRequirements = GetMissingRequirements(definition, plannerFacts);
					var manifestationCount = candidatesByAction.TryGetValue(definition.Id, out var count) ? count : 0;
					var state = missingRequirements.Count > 0
						? "Blocked"
						: manifestationCount > 0
							? "Available"
							: "Deferred";
					var stateColor = missingRequirements.Count > 0
						? ColorBlocked
						: manifestationCount > 0
							? ColorOk
							: ColorDeferred;
					var min = new NumericsVector2(
						margin + (columnIndex * (nodeWidth + horizontalGap)),
						margin + (rowIndex * (nodeHeight + verticalGap)));
					var max = min + new NumericsVector2(nodeWidth, nodeHeight);
					var node = new ActionGraphNode(
						definition.Id,
						new GraphBounds(min, max),
						TrimGraphText(definition.Name, 24),
						$"{state} | targets {manifestationCount}",
						missingRequirements.Count > 0
							? $"Requires {TrimGraphText(FormatList(missingRequirements), 24)}"
							: $"Adds {TrimGraphText(FormatList(definition.EffectFacts), 24)}",
						ToU32(WithAlpha(stateColor, 0.22f)),
						ToU32(WithAlpha(stateColor, 0.95f)),
						ToU32(new NumericsVector4(0.96f, 0.97f, 0.98f, 1f)),
						ToU32(WithAlpha(stateColor, 1f)),
						ToU32(new NumericsVector4(0.78f, 0.82f, 0.87f, 1f)));
					nodes.Add(node);
					nodesById[definition.Id] = node;
				}
			}

			var edges = new List<ActionGraphEdge>();
			foreach (var (actionId, dependencies) in dependenciesByAction)
			{
				if (!nodesById.TryGetValue(actionId, out var targetNode))
				{
					continue;
				}

				foreach (var dependencyId in dependencies)
				{
					if (!nodesById.TryGetValue(dependencyId, out var sourceNode))
					{
						continue;
					}
					var dependencyFacts = dependencyFactsByPair.TryGetValue((dependencyId, actionId), out var facts)
						? TrimGraphText(FormatList(facts), 28)
						: string.Empty;

					edges.Add(new ActionGraphEdge(
						sourceNode.Bounds.Min + new NumericsVector2(nodeWidth, nodeHeight / 2f),
						targetNode.Bounds.Min + new NumericsVector2(0, nodeHeight / 2f),
						ToU32(WithAlpha(ColorPlanned, 0.85f)),
						dependencyFacts));
				}
			}

			var canvasWidth = (columnGroups.Count * nodeWidth) + (Math.Max(0, columnGroups.Count - 1) * horizontalGap) + (margin * 2);
			var canvasHeight = (maxRows * nodeHeight) + (Math.Max(0, maxRows - 1) * verticalGap) + (margin * 2);
			return new ActionGraphLayout(nodes, edges, new NumericsVector2(canvasWidth, canvasHeight));
		}

		ActionGraphLayout BuildSkillGraphLayout(IReadOnlyList<SkillDefinition> skills, Dictionary<string, int> agentSkills)
		{
			const float nodeWidth = 180f;
			const float skillNodeHeight = 92f;
			const float groupHeaderHeight = 32f;
			const float horizontalGap = 20f;
			const float verticalGap = 14f;
			const float categoryGap = 40f;
			const float margin = 20f;

			agentSkills ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

			var groupOrderByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var skillsByGroup = new Dictionary<string, List<SkillDefinition>>(StringComparer.OrdinalIgnoreCase);

			foreach (var skill in skills)
			{
				if (!groupOrderByCategory.TryGetValue(skill.Category, out var groupOrder))
				{
					groupOrder = new List<string>();
					groupOrderByCategory[skill.Category] = groupOrder;
				}

				if (!skillsByGroup.ContainsKey(skill.Group))
				{
					groupOrder.Add(skill.Group);
					skillsByGroup[skill.Group] = new List<SkillDefinition>();
				}

				skillsByGroup[skill.Group].Add(skill);
			}

			string[] knownCategoryOrder = ["Physical", "Mental", "Social", "Supernatural"];
			var orderedCategories = knownCategoryOrder
				.Where(groupOrderByCategory.ContainsKey)
				.Concat(groupOrderByCategory.Keys.Where(c => !knownCategoryOrder.Any(k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase))))
				.ToList();

			var nodes = new List<ActionGraphNode>();
			var cursorX = margin;

			for (var ci = 0; ci < orderedCategories.Count; ci++)
			{
				if (ci > 0)
				{
					cursorX += categoryGap;
				}

				var category = orderedCategories[ci];
				var groupOrder = groupOrderByCategory[category];

				foreach (var groupName in groupOrder)
				{
					var groupSkills = skillsByGroup[groupName];

					nodes.Add(new ActionGraphNode(
						$"group:{groupName}",
						new GraphBounds(
							new NumericsVector2(cursorX, margin),
							new NumericsVector2(cursorX + nodeWidth, margin + groupHeaderHeight)),
						groupName,
						category,
						string.Empty,
						ToU32(new NumericsVector4(0.12f, 0.15f, 0.22f, 1f)),
						ToU32(new NumericsVector4(0.35f, 0.42f, 0.60f, 0.9f)),
						ToU32(new NumericsVector4(0.72f, 0.80f, 0.95f, 1f)),
						ToU32(new NumericsVector4(0.45f, 0.52f, 0.70f, 1f)),
						default));

					for (var si = 0; si < groupSkills.Count; si++)
					{
						var skill = groupSkills[si];
						var level = agentSkills.TryGetValue(skill.Id, out var l) ? l : 1;
						var tierColor = GetSkillTierColor(level);
						var progress = (level - 1f) / 99f;
						var nodeY = margin + groupHeaderHeight + verticalGap + (si * (skillNodeHeight + verticalGap));

						nodes.Add(new ActionGraphNode(
							skill.Id,
							new GraphBounds(
								new NumericsVector2(cursorX, nodeY),
								new NumericsVector2(cursorX + nodeWidth, nodeY + skillNodeHeight)),
							skill.Name,
							$"Lv {level}  \u2014  {GetSkillTierName(level)}",
							$"{skill.Category} \u203a {skill.Group}",
							ToU32(new NumericsVector4(0.08f + (tierColor.X * 0.06f), 0.09f + (tierColor.Y * 0.06f), 0.12f + (tierColor.Z * 0.06f), 1f)),
							ToU32(WithAlpha(tierColor, 0.6f)),
							ToU32(new NumericsVector4(0.96f, 0.97f, 0.98f, 1f)),
							ToU32(tierColor),
							ToU32(new NumericsVector4(0.55f, 0.60f, 0.68f, 1f)),
							progress,
							ToU32(WithAlpha(tierColor, 0.9f))));
					}

					cursorX += nodeWidth + horizontalGap;
				}
			}

			var maxGroupSkills = skillsByGroup.Values.Max(g => g.Count);
			var totalCanvasWidth = cursorX + margin;
			var totalCanvasHeight = margin + groupHeaderHeight + verticalGap + (maxGroupSkills * (skillNodeHeight + verticalGap)) + margin;
			return new ActionGraphLayout(nodes, Array.Empty<ActionGraphEdge>(), new NumericsVector2(totalCanvasWidth, totalCanvasHeight));
		}

		void DrawSelectedGoapActionDetails(
			SimulationGoapAction definition,
			HashSet<string> plannerFacts,
			Dictionary<string, int> candidatesByAction)
		{
			ImGui.Separator();
			ImGui.TextUnformatted($"Selected action: {definition.Name}");
			ImGui.TextUnformatted($"Id: {definition.Id}");
			ImGui.TextWrapped($"Target kind: {definition.TargetKind}; destination mode: {definition.DestinationMode}");
			ImGui.TextWrapped($"Requirements: {FormatList(definition.RequiredFacts)}");
			ImGui.TextWrapped($"Effects {FormatList(definition.EffectFacts)}");
			ImGui.TextUnformatted($"Base cost: {definition.BaseCost}; duration ticks: {definition.DurationTicks}");
			var manifestations = candidatesByAction.TryGetValue(definition.Id, out var count) ? count : 0;
			ImGui.TextUnformatted($"Current manifestations: {manifestations}");
			var missingPrerequisiteFacts = GetMissingRequirements(definition, plannerFacts);
			if (missingPrerequisiteFacts.Count == 0)
			{
				ImGui.TextColored(manifestations > 0 ? ColorOk : ColorDeferred, manifestations > 0 ? "Status: available" : "Status: deferred (no current targets)");
			}
			else
			{
				ImGui.TextColored(ColorBlocked, $"Status: blocked by {FormatList(missingPrerequisiteFacts)}");
			}
		}

		void DrawActivePlanSection(Entity entity)
		{
			if (!entity.Has<ActionQueueComponent>())
			{
				return;
			}

			var queue = entity.Get<ActionQueueComponent>().Actions.ToArray();
			if (queue.Length == 0)
			{
				return;
			}

			if (!ImGui.CollapsingHeader($"Active Execution — {queue.Length} action(s) queued", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			// ── Gantt bar ─────────────────────────────────────────────────────────────
			const float barHeight = 26f;
			const float gap = 2f;
			var totalDisplayCost = queue.Sum(a => Math.Max(1, a.Cost));
			var availableWidth = ImGui.GetContentRegionAvail().X;
			var drawList = ImGui.GetWindowDrawList();
			var origin = ImGui.GetCursorScreenPos();

			// Reserve space so ImGui layout accounts for the bar
			ImGui.Dummy(new NumericsVector2(availableWidth, barHeight));

			// Background track
			drawList.AddRectFilled(origin, origin + new NumericsVector2(availableWidth, barHeight),
				ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.08f, 0.09f, 0.12f, 1f)), 4f);

			var segX = origin.X;
			for (var i = 0; i < queue.Length; ++i)
			{
				var action = queue[i];
				var displayCost = Math.Max(1, action.Cost);
				var segWidth = (displayCost / (float)totalDisplayCost) * availableWidth;

				var segMin = new NumericsVector2(segX + (i > 0 ? gap : 0), origin.Y + 1f);
				var segMax = new NumericsVector2(segX + segWidth - (i < queue.Length - 1 ? gap : 0), origin.Y + barHeight - 1f);

				// Segment background
				var bgColor = action.Status == AgentActionStatus.Running
					? ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.12f, 0.28f, 0.48f, 1f))
					: ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.16f, 0.17f, 0.20f, 1f));
				drawList.AddRectFilled(segMin, segMax, bgColor, 3f);

				// Progress fill for the running action
				if (action.Status == AgentActionStatus.Running && action.Cost > 0)
				{
					var ratio = Math.Clamp(action.Progress / (float)action.Cost, 0f, 1f);
					var fillMax = new NumericsVector2(segMin.X + (segMax.X - segMin.X) * ratio, segMax.Y);
					drawList.AddRectFilled(segMin, fillMax,
						ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.22f, 0.58f, 0.88f, 1f)), 3f);
				}

				// Segment border
				var borderColor = action.Status == AgentActionStatus.Running
					? ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.40f, 0.80f, 1.00f, 1f))
					: ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.26f, 0.28f, 0.32f, 1f));
				drawList.AddRect(segMin, segMax, borderColor, 3f);

				// Label clipped to segment
				var label = action.ActionId ?? action.Name;
				drawList.PushClipRect(segMin, segMax, true);
				var textPos = new NumericsVector2(segMin.X + 4f, segMin.Y + (barHeight - 13f) * 0.5f);
				drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(new NumericsVector4(0.88f, 0.90f, 0.94f, 1f)), label);
				drawList.PopClipRect();

				segX += segWidth;
			}

			// ── Step list ─────────────────────────────────────────────────────────────
			ImGui.Spacing();
			for (var i = 0; i < queue.Length; ++i)
			{
				var action = queue[i];
				var isRunning = action.Status == AgentActionStatus.Running;
				var isFailed = action.Status == AgentActionStatus.Failed;
				var color = isRunning ? ColorOk : isFailed ? ColorBlocked : new NumericsVector4(0.55f, 0.57f, 0.60f, 1f);
				var marker = isRunning ? "▶" : isFailed ? "✗" : " ";
				var progressStr = action.Cost > 0
					? $"  [{action.Progress}/{action.Cost} ticks ({100f * action.Progress / (float)action.Cost:0.#}%)]"
					: string.Empty;
				ImGui.TextColored(color, $"{marker} [{i + 1}] {action.ActionId}{progressStr}");
				if (!string.IsNullOrWhiteSpace(action.FailureReason))
				{
					ImGui.TextColored(ColorBlocked, $"     ↳ {action.FailureReason}");
				}
			}
		}

		void DrawActionSection(Entity entity, GoapAgent goapAgent)
		{
			if (!ImGui.CollapsingHeader("Actions", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			var plannerFacts = GetCurrentFacts(goapAgent);

			DrawDirectActions(entity);

			foreach (var definition in definitions.GetActionDefinitions())
			{
				var missingPrerequisiteFacts = GetMissingRequirements(definition, plannerFacts);
				var isAvailableAction = missingPrerequisiteFacts.Count == 0;
				var actionColor = isAvailableAction ? ColorOk : ColorBlocked;
				var header = $"{definition.Name} [{(isAvailableAction ? "Available" : "Unavailable")}]";

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

					DrawManualActionControls(entity, definition);

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
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.MoveToCell, tags: ["manual", "direct-action"]);
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
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.Wait, durationTicks: taskDurationTicks, tags: ["manual", "direct-action"]);
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
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PickUpFirstItemAtCell, tags: ["manual", "direct-action"]);
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
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PutDownInventoryAtCell, tags: ["manual", "direct-action"]);
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
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.DropInventoryItem, selectedItem: inventory[selectedDropInventoryItemIndex], tags: ["manual", "direct-action", "inventory"]);
			}

			ImGui.TreePop();
		}

		void DrawManualActionControls(Entity entity, SimulationGoapAction definition)
		{
			ImGui.Separator();
			ImGui.TextUnformatted("Issue action manually");
			if (ActionRequestHandler == null) { ImGui.TextUnformatted("Action request handler is not configured."); return; }
			if (ImGui.Button($"Queue Action##{definition.Id}"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.ExecuteAction, action: definition, tags: ["manual", "goap-action"]);
			}
		}

		void DrawPlanningSection(GoapAgent goapAgent)
		{
			if (!ImGui.CollapsingHeader("Goal Planning", ImGuiTreeNodeFlags.DefaultOpen))
			{
				return;
			}

			if (goapAgent == null)
			{
				ImGui.TextUnformatted("GOAP agent unavailable.");
				return;
			}

			var plannerFacts = GetCurrentFacts(goapAgent);
			ImGui.TextUnformatted($"Planner facts: {plannerFacts.Count}");
			if (plannerFacts.Count > 0 && ImGui.TreeNode("Planner facts"))
			{
				foreach (var fact in plannerFacts.OrderBy(fact => fact, StringComparer.OrdinalIgnoreCase))
				{
					ImGui.BulletText(fact);
				}

				ImGui.TreePop();
			}

			var candidatePlans = BuildGoalInspections(goapAgent)
				.Select(goal => goal.Plan)
				.Where(plan => plan != null)
				.ToList();

			if (candidatePlans.Count == 0)
			{
				ImGui.TextColored(ColorBlocked, "No candidate plans built.");
			}
			else
			{
				ImGui.TextColored(ColorPlanned, $"Candidate plans: {candidatePlans.Count}");
				if (ImGui.TreeNode("Candidate plans"))
				{
					for (var i = 0; i < candidatePlans.Count; ++i)
					{
						var candidatePlan = candidatePlans[i];
						if (ImGui.TreeNode($"{i + 1}. {candidatePlan.Goal.Name} (cost {CalculatePlanCost(candidatePlan):0.##})"))
						{
							DrawPlanActions(candidatePlan);
							ImGui.TreePop();
						}
					}
					ImGui.TreePop();
				}
			}

			var allActions = goapAgent.Actions.OfType<SimulationGoapAction>().ToList();
			var availableActions = allActions.Where(a => GetMissingRequirements(a, plannerFacts).Count == 0).ToList();
			var unavailableActions = allActions.Where(a => GetMissingRequirements(a, plannerFacts).Count > 0).ToList();

			ImGui.TextUnformatted($"Available now: {availableActions.Count}");
			ImGui.TextUnformatted($"Unavailable (missing requirements): {unavailableActions.Count}");

			if (ImGui.TreeNode($"Available actions ({availableActions.Count})"))
			{
				for (var i = 0; i < availableActions.Count; ++i)
				{
					ImGui.TextColored(ColorOk, $"{i + 1}. {availableActions[i].Name}");
				}

				ImGui.TreePop();
			}

			if (ImGui.TreeNode($"Unavailable actions ({unavailableActions.Count})"))
			{
				for (var i = 0; i < unavailableActions.Count; ++i)
				{
					var action = unavailableActions[i];
					ImGui.TextColored(ColorDeferred, $"{i + 1}. {action.Name}");
					ImGui.TextWrapped($"  Missing: {FormatList(GetMissingRequirements(action, plannerFacts))}");
				}
				ImGui.TreePop();
			}
		}

		void DrawSiteAndMachinePanel(Entity entity)
		{
			if (entity.IsConstructionSite())
			{
				if (!ImGui.CollapsingHeader("Construction Site"))
				{
					return;
				}

				var missingCosts = entity.GetMissingBuildCosts();
				var allCosts = entity.GetBuildCosts();

				int totalRequired = 0;
				int totalStored = 0;

				foreach (var cost in allCosts)
				{
					var stored = entity.CountStoredItems(cost.ItemId);
					totalRequired += cost.Quantity;
					totalStored += Math.Min(stored, cost.Quantity);
					var ratio = cost.Quantity > 0 ? (float)stored / cost.Quantity : 1f;
					ratio = Math.Min(ratio, 1f);
					ImGui.ProgressBar(ratio, new NumericsVector2(-1, 0), $"{cost.ItemId}: {stored}/{cost.Quantity}");
				}

				if (totalRequired > 0)
				{
					var overallRatio = (float)totalStored / totalRequired;
					ImGui.Separator();
					ImGui.ProgressBar(overallRatio, new NumericsVector2(-1, 0), $"Overall: {(int)(overallRatio * 100)}%%");
				}

				ImGui.Separator();
				return;
			}

			var recipes = entity.GetRecipes();
			if (recipes.Length == 0)
			{
				return;
			}

			if (!ImGui.CollapsingHeader("Workstation"))
			{
				return;
			}

			// Stored inputs summary
			ImGui.TextUnformatted("Stored Items:");
			if (entity.Has<InventoryComponent>())
			{
				var stored = entity.GetInventory();
				if (stored.Count == 0)
				{
					ImGui.TextDisabled("  (empty)");
				}
				else
				{
					foreach (var item in stored)
					{
						ImGui.TextUnformatted($"  {item.GetItemDefinitionId()}");
					}
				}
			}

			ImGui.Separator();

			// Recipe readiness
			ImGui.TextUnformatted("Recipes:");
			foreach (var recipe in recipes)
			{
				var ready = entity.HasStoredMaterials(recipe.Inputs);
				var label = ready ? $"[READY] {recipe.Name}" : $"[WAIT]  {recipe.Name}";
				ImGui.TextUnformatted(label);
				foreach (var input in recipe.Inputs)
				{
					var storedCount = entity.CountStoredItems(input.ItemId);
					var ratio = input.Quantity > 0 ? (float)storedCount / input.Quantity : 1f;
					ratio = Math.Min(ratio, 1f);
					ImGui.ProgressBar(ratio, new NumericsVector2(-1, 0), $"  {input.ItemId}: {storedCount}/{input.Quantity}");
				}
			}

			ImGui.Separator();

			// Production order controls
			ImGui.TextUnformatted("Production Order:");
			var activeOrder = entity.GetProductionOrder();
			var hasOrder = entity.HasActiveProductionOrder();

			if (hasOrder)
			{
				ImGui.TextUnformatted($"Active: {activeOrder.ActiveRecipeId}");
				ImGui.TextUnformatted($"Progress: {activeOrder.BatchesCompleted}/{activeOrder.BatchesRequested} batches");

				if (ImGui.Button("Clear Order"))
				{
					entity.ClearProductionOrder();
				}
			}
			else
			{
				var recipeNames = recipes.Select(r => r.Name).ToArray();
				if (selectedProductionRecipeIndex >= recipeNames.Length)
				{
					selectedProductionRecipeIndex = 0;
				}

				ImGui.Combo("Recipe##prod", ref selectedProductionRecipeIndex, recipeNames, recipeNames.Length);
				ImGui.InputInt("Batches##prod", ref productionOrderBatchCount);
				productionOrderBatchCount = Math.Max(1, productionOrderBatchCount);

				if (ImGui.Button("Set Order") && recipeNames.Length > 0)
				{
					entity.SetProductionOrder(recipes[selectedProductionRecipeIndex].Id, productionOrderBatchCount);
				}
			}

			ImGui.Separator();
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
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.MoveToCell, tags: ["manual", "direct-action"]);
			}

			if (ImGui.Button("Queue Wait"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.Wait, durationTicks: taskDurationTicks, tags: ["manual", "direct-action"]);
			}

			if (ImGui.Button("Queue Pick Up First Item"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PickUpFirstItemAtCell, tags: ["manual", "direct-action"]);
			}

			if (ImGui.Button("Queue Put Down Inventory"))
			{
				lastIssuedActionMessage = RequestAction(entity, AgentActionIds.PutDownInventoryAtCell, tags: ["manual", "direct-action"]);
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
			GoapAction action = default,
			string[] tags = null)
		{
			if (ActionRequestHandler == null)
			{
				return "Action request handler is not configured.";
			}

			var metadata = new AgentActionMetadata(AgentActionSource.Manual, tags ?? ["manual"]);
			var actionRequest = new AgentActionRequest(
				actionId,
				targetCell ?? new Point(taskTargetCellX, taskTargetCellY),
				durationTicks ?? taskDurationTicks,
				replaceQueuedActions,
				selectedItem,
				action,
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

		static uint ToU32(NumericsVector4 color)
			=> ImGui.ColorConvertFloat4ToU32(color);

		static NumericsVector4 WithAlpha(NumericsVector4 color, float alpha)
			=> new(color.X, color.Y, color.Z, alpha);

		static string GetSkillTierName(int level)
			=> level switch
			{
				<= 20 => "Novice",
				<= 40 => "Apprentice",
				<= 60 => "Journeyman",
				<= 80 => "Expert",
				_ => "Master"
			};

		static NumericsVector4 GetSkillTierColor(int level)
			=> level switch
			{
				<= 20 => new NumericsVector4(0.55f, 0.58f, 0.65f, 1f),   // grey-blue  (Novice)
				<= 40 => new NumericsVector4(0.27f, 0.76f, 0.67f, 1f),   // teal       (Apprentice)
				<= 60 => new NumericsVector4(0.88f, 0.72f, 0.22f, 1f),   // gold       (Journeyman)
				<= 80 => new NumericsVector4(0.92f, 0.48f, 0.15f, 1f),   // orange     (Expert)
				_ => new NumericsVector4(0.97f, 0.82f, 0.26f, 1f)        // bright amber (Master)
			};

		static string TrimGraphText(string value, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
			{
				return value;
			}

			return value[..Math.Max(0, maxLength - 3)] + "...";
		}

		static bool Contains(GraphBounds bounds, NumericsVector2 point)
			=> point.X >= bounds.Min.X && point.X <= bounds.Max.X && point.Y >= bounds.Min.Y && point.Y <= bounds.Max.Y;

		static readonly NumericsVector4 ColorOk = new(0.52f, 0.82f, 0.56f, 1f);
		static readonly NumericsVector4 ColorPlanned = new(0.95f, 0.78f, 0.34f, 1f);
		static readonly NumericsVector4 ColorDeferred = new(0.96f, 0.65f, 0.27f, 1f);
		static readonly NumericsVector4 ColorBlocked = new(0.9f, 0.34f, 0.34f, 1f);

		static string FormatList(IEnumerable<string> values)
		{
			var materialized = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? [];
			return materialized.Length == 0 ? "none" : string.Join(", ", materialized);
		}

		static void DrawPlanActions(GoapPlan plan)
		{
			if (plan.Actions.Count == 0)
			{
				ImGui.TextUnformatted("Plan has no actions.");
				return;
			}

			for (var i = 0; i < plan.Actions.Count; ++i)
			{
				var action = plan.Actions[i];
				ImGui.BulletText($"{i + 1}. {action.Name} (cost {action.Cost(plan.Agent):0.##})");
			}
		}

		static string FormatInventoryItemLabel(Entity item)
			=> $"{item.GetName()} [{item.GetItemDefinitionId()}]";

		static NumericsVector4 GetGoalStatusColor(GoalInspection goal)
			=> goal.IsReached
				? ColorOk
				: goal.Plan != null
					? ColorPlanned
					: goal.IsValid
						? ColorDeferred
						: ColorBlocked;

		static IReadOnlyList<GoalInspection> BuildGoalInspections(GoapAgent goapAgent)
			=> [.. goapAgent.Goals
				.OfType<SimulationGoapGoal>()
				.Select(goal =>
				{
					var priority = goal.Priority(goapAgent);
					var isValid = goapAgent.IsGoalValid(goal, goapAgent.States);
					var isReached = goal.IsReached(goapAgent.States);
					var plan = isValid && !isReached ? goapAgent.FindPlan(goal) : null;
					var missingFacts = goal.RequiredFacts.Where(fact => !GoapFactState.IsSatisfied(goapAgent.States, fact)).ToArray();
					return new GoalInspection(goal, priority, isValid, isReached, missingFacts, plan);
				})
				.OrderByDescending(goal => goal.Priority)];

		static HashSet<string> GetCurrentFacts(GoapAgent goapAgent)
			=> goapAgent == null
				? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(goapAgent.States
					.Where(pair => pair.Value is bool value && value)
					.Select(pair => pair.Key.ToString())
					.Where(fact => !string.IsNullOrWhiteSpace(fact)), StringComparer.OrdinalIgnoreCase);

		static double CalculatePlanCost(GoapPlan plan)
			=> plan.Actions.Sum(action => action.Cost(plan.Agent));

		static List<string> GetMissingRequirements(SimulationGoapAction definition, HashSet<string> plannerFacts)
		{
			var missingRequirements = new List<string>();
			AddMissingRequirements(missingRequirements, plannerFacts, definition.RequiredFacts);

			foreach (var blockedFact in definition.RequiredFacts.Where(fact => fact.StartsWith('!') && plannerFacts.Contains(fact[1..])))
			{
				missingRequirements.Add($"blocked:{blockedFact[1..]}");
			}

			return missingRequirements;
		}

		static void AddMissingRequirements(List<string> missingRequirements, HashSet<string> plannerFacts, IEnumerable<string> requiredFacts)
		{
			foreach (var fact in requiredFacts.Where(fact => !string.IsNullOrWhiteSpace(fact)))
			{
				if (fact.StartsWith('!'))
				{
					var blockedFact = fact[1..];
					if (plannerFacts.Contains(blockedFact))
					{
						missingRequirements.Add($"blocked:{blockedFact}");
					}

					continue;
				}

				if (!plannerFacts.Contains(fact))
				{
					missingRequirements.Add(fact);
				}
			}
		}

		readonly record struct ActionGraphLayout(IReadOnlyList<ActionGraphNode> Nodes, IReadOnlyList<ActionGraphEdge> Edges, NumericsVector2 CanvasSize);
		readonly record struct GraphBounds(NumericsVector2 Min, NumericsVector2 Max);
		readonly record struct ActionGraphNode(string Id, GraphBounds Bounds, string Title, string Subtitle, string Detail, uint FillColor, uint BorderColor, uint TextColor, uint SubtitleColor, uint DetailColor, float ProgressRatio = 0f, uint ProgressBarColor = 0u);
		readonly record struct ActionGraphEdge(NumericsVector2 From, NumericsVector2 To, uint Color, string Label);
		readonly record struct GoalInspection(SimulationGoapGoal Goal, double Priority, bool IsValid, bool IsReached, IReadOnlyList<string> MissingRequiredFacts, GoapPlan Plan);

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
				if (worldObjectDefinition.BuildCosts.Length > 0)
				{
					yield return $" - BuildCosts=[{string.Join(", ", worldObjectDefinition.BuildCosts.Select(cost => $"{cost.Quantity}x {cost.ItemId}"))}]";
				}

				if (worldObjectDefinition.Recipes.Length > 0)
				{
					yield return $" - Recipes={worldObjectDefinition.Recipes.Length}";
					foreach (var recipe in worldObjectDefinition.Recipes)
					{
						yield return $"   * {recipe.Name}: {string.Join(", ", recipe.Inputs.Select(input => $"{input.Quantity}x {input.ItemId}"))} -> {recipe.OutputQuantity}x {recipe.OutputItemId}";
					}
				}
			}

			if (entity.Has<ConstructionSiteComponent>())
			{
				var site = entity.Get<ConstructionSiteComponent>();
				yield return $" - ConstructionTarget={site.TargetDefinitionId}";
				yield return $" - MissingBuildCosts=[{string.Join(", ", entity.GetMissingBuildCosts().Select(cost => $"{cost.Quantity}x {cost.ItemId}"))}]";
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