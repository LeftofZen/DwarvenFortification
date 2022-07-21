namespace DwarvenFortification
{
	public class ItemDefinition
	{
		public string Name { get; init; }
		public string Action { get; init; }
		public IGameEntity Owner { get; init; }
		public int Value { get; init; }
	}

	public class ItemDefinitionJson
	{
		public ItemDefinition[] Items { get; init; }
	}

	public class Item : IGameEntity
	{
		public Item(string name)
		{
			Name = name;
		}

		public string Name { get; }

		public override string ToString()
			=> Name;
	}
}
