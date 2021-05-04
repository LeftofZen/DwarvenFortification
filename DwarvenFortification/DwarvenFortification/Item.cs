namespace DwarvenFortification
{
	public class Item
	{
		public Item(string name)
		{
			Name = name;
		}

		public string Name;
		public override string ToString()
			=> Name;
	}
}
