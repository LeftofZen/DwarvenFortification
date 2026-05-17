using Arch.Core.Extensions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class EconomyTests
{
	[Test]
	public void CreateWorldObject_WithBuildCosts_CreatesConstructionSite()
	{
		var registry = SimulationDefinitionRegistry.LoadFromContentDirectory(GetContentConfigDirectory());
		var factory = new SimulationEntityFactory(registry);

		var worldObject = factory.CreateWorldObject("ore-bin", Point.Zero, Point.Zero);

		Assert.Multiple(() =>
		{
			Assert.That(worldObject.IsConstructionSite(), Is.True);
			Assert.That(worldObject.Get<ConstructionSiteComponent>().TargetDefinitionId, Is.EqualTo("ore-bin"));
			Assert.That(worldObject.GetMissingBuildCosts().Select(cost => (cost.ItemId, cost.Quantity)), Is.EquivalentTo(new[]
			{
				("oak-planks", 6),
				("stone", 4),
			}));
		});
	}

	[Test]
	public void ConstructionSite_OnlyAcceptsStillRequiredMaterials()
	{
		var registry = SimulationDefinitionRegistry.LoadFromContentDirectory(GetContentConfigDirectory());
		var factory = new SimulationEntityFactory(registry);
		var site = factory.CreateWorldObject("ore-bin", Point.Zero, Point.Zero);

		Assert.That(site.CanStore(factory.CreateItem("oak-planks")), Is.True);
		Assert.That(site.CanStore(factory.CreateItem("maple-log")), Is.False);

		for (var i = 0; i < 6; ++i)
		{
			site.AddStoredItem(factory.CreateItem("oak-planks"));
		}

		Assert.That(site.CanStore(factory.CreateItem("oak-planks")), Is.False);
		Assert.That(site.CanStore(factory.CreateItem("stone")), Is.True);
	}

	[Test]
	public void WorkstationRecipe_UsesStoredItemEntitiesAsInputs()
	{
		var registry = SimulationDefinitionRegistry.LoadFromContentDirectory(GetContentConfigDirectory());
		var factory = new SimulationEntityFactory(registry);
		var smelter = factory.CreateCompletedWorldObject("smelter", Point.Zero, Point.Zero);

		smelter.AddStoredItem(factory.CreateItem("iron-ore"));
		smelter.AddStoredItem(factory.CreateItem("iron-ore"));
		smelter.AddStoredItem(factory.CreateItem("coal"));

		Assert.That(smelter.TryGetRecipeForOutput("iron-ingot", out var recipe), Is.True);
		Assert.That(recipe.Id, Is.EqualTo("smelt-iron-ingot"));
		Assert.That(smelter.HasStoredMaterials(recipe.Inputs), Is.True);

		var consumed = smelter.ConsumeStoredMaterials(recipe.Inputs);

		Assert.Multiple(() =>
		{
			Assert.That(consumed, Has.Length.EqualTo(3));
			Assert.That(consumed.Select(item => item.GetItemDefinitionId()), Is.EquivalentTo(new[] { "iron-ore", "iron-ore", "coal" }));
			Assert.That(smelter.CountStoredItems("iron-ore"), Is.Zero);
			Assert.That(smelter.CountStoredItems("coal"), Is.Zero);
		});
	}

	static string GetContentConfigDirectory()
		=> Path.GetFullPath(Path.Combine(
			TestContext.CurrentContext.TestDirectory,
			"..",
			"..",
			"..",
			"..",
			"DwarvenFortification",
			"Content",
			"config"));
}