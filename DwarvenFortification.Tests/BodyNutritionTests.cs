using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class BodyNutritionTests
{
	[Test]
	public void TickBodyNutrition_UsesHydrationAndBackfillsSugarFromCarbohydrates()
	{
		var world = World.Create();
		var agent = world.Create(new BodyNutritionComponent
		{
			CarbohydratesCurrent = 40f,
			CarbohydratesMax = 120f,
			ProteinCurrent = 80f,
			ProteinMax = 120f,
			FatCurrent = 50f,
			FatMax = 80f,
			SugarCurrent = 2f,
			SugarMax = 20f,
			HydrationCurrentLiters = 3f,
			HydrationMaxLiters = 4f,
			SugarUsePerTick = 1f,
			HydrationUsePerTick = 0.1f,
			SugarFromCarbohydratesPerTick = 4f,
			SugarFromFatPerTick = 0.5f,
			ProteinCatabolismPerTick = 0.1f,
		});

		agent.TickBodyNutrition();

		var nutrition = agent.Get<BodyNutritionComponent>();
		Assert.Multiple(() =>
		{
			Assert.That(nutrition.HydrationCurrentLiters, Is.EqualTo(2.9f).Within(0.001f));
			Assert.That(nutrition.SugarCurrent, Is.GreaterThan(1f));
			Assert.That(nutrition.CarbohydratesCurrent, Is.LessThan(40f));
		});
	}

	[Test]
	public void AbsorbNutrition_AddsMacronutrientsAndFluidsByComposition()
	{
		var world = World.Create();
		var agent = world.Create(new BodyNutritionComponent
		{
			CarbohydratesCurrent = 10f,
			CarbohydratesMax = 120f,
			ProteinCurrent = 20f,
			ProteinMax = 120f,
			FatCurrent = 5f,
			FatMax = 80f,
			SugarCurrent = 2f,
			SugarMax = 20f,
			HydrationCurrentLiters = 1.5f,
			HydrationMaxLiters = 4f,
		});

		agent.AbsorbNutrition(new ItemNutritionComponent(24f, 8f, 6f, 4f, 2f, 0.7f));

		var nutrition = agent.Get<BodyNutritionComponent>();
		Assert.Multiple(() =>
		{
			Assert.That(nutrition.CarbohydratesCurrent, Is.EqualTo(34f).Within(0.001f));
			Assert.That(nutrition.ProteinCurrent, Is.EqualTo(28f).Within(0.001f));
			Assert.That(nutrition.FatCurrent, Is.EqualTo(11f).Within(0.001f));
			Assert.That(nutrition.SugarCurrent, Is.EqualTo(6f).Within(0.001f));
			Assert.That(nutrition.HydrationCurrentLiters, Is.EqualTo(2.2f).Within(0.001f));
		});
	}

	[Test]
	public void IsSystemOperational_DisablesMusculoskeletalWhenCarbohydratesAreCritical()
	{
		var world = World.Create();
		var agent = world.Create(new BodyNutritionComponent
		{
			CarbohydratesCurrent = 5f,
			CarbohydratesMax = 120f,
			ProteinCurrent = 60f,
			ProteinMax = 120f,
			FatCurrent = 40f,
			FatMax = 80f,
			SugarCurrent = 8f,
			SugarMax = 20f,
			HydrationCurrentLiters = 3f,
			HydrationMaxLiters = 4f,
		});

		Assert.That(agent.IsSystemOperational("musculoskeletal"), Is.False);
	}
}