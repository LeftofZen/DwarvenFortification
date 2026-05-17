using Arch.Core;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.Simulation.Composition
{
	public interface ISimulationEntityFactory
	{
		Entity CreateAgent(string name, Point position, string archetypeId = "dwarf");
		Entity CreateItem(string definitionId);
		Entity CreateWorldObject(string definitionId, Point position, Point cell);
		Entity CreateCompletedWorldObject(string definitionId, Point position, Point cell);
		Entity CreateResourceNode(string definitionId, Point position, Point cell);
	}
}