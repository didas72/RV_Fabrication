namespace RV_Fabrication
{
	internal class IMacro(string name, string value)
	{
		public string Name { get; } = name;
		public string Value { get; } = value;
		public int References { get; set; } = 0;
	}
}
