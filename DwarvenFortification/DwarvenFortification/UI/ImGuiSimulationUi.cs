using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.Logging;
using ImGuiNET;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector4 = System.Numerics.Vector4;

namespace DwarvenFortification
{
	public sealed class ImGuiSimulationUi
	{
		readonly SimulationDefinitionRegistry definitions;
		readonly ILogger logger;
		object boundObject;
		Entity? boundEntity;

		public ImGuiSimulationUi(SimulationDefinitionRegistry definitions, ILogger logger)
		{
			this.definitions = definitions;
			this.logger = logger;
		}

		public MouseClickMode SelectedMouseClickMode { get; set; } = MouseClickMode.None;
		public CellType SelectedCellType { get; set; } = CellType.Dirt;
		public string SelectedOccupantId { get; set; } = string.Empty;
		public bool WantsMouseCapture { get; private set; }

		public void BindObject(object obj)
		{
			boundObject = obj;
			boundEntity = null;
		}

		public void BindEntity(Entity entity)
		{
			boundEntity = entity;
			boundObject = null;
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
				foreach (var line in ReflectEntity(boundEntity.Value))
				{
					ImGui.TextUnformatted(line);
				}
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

		IEnumerable<string> ReflectEntity(Entity entity)
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

			if (entity.Has<InventoryComponent>())
			{
				var inventory = entity.Get<InventoryComponent>();
				yield return $" - Inventory={inventory.Items.Count}/{inventory.Capacity}";
				foreach (var item in inventory.Items)
				{
					yield return $"  * {item.GetName()}";
				}
			}

			if (entity.Has<TaskQueueComponent>())
			{
				var taskQueue = entity.Get<TaskQueueComponent>();
				yield return $" - Tasks={taskQueue.Tasks.Count}";
				foreach (var task in taskQueue.Tasks)
				{
					yield return $"  * {task}";
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