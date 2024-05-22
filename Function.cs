using System.Collections.Generic;

using static RV_Fabrication.Function;

namespace RV_Fabrication
{
	internal class Function(string name, int argCount, InlineHint inlineHint)
	{
		public enum InlineHint
		{
			AutoInline = 0,
			AgressiveInline = 1,
			NoInline = 2,
		}

		public string Name { get; } = name;
		public int ArgCount { get; } = argCount;
		public InlineHint InlineOption { get; } = inlineHint;
		public List<string> Lines { get; } = [];
		public int References { get; set; } = 0;
		private List<string>? usedSaveRegs = null;



		public List<string> GetUsedSaveRegs()
		{
			if (usedSaveRegs != null) return usedSaveRegs;

			usedSaveRegs = [];

			foreach (string line in Lines)
			{
				string code = line.Split(FabricationProcessor.COMMENT_PREFIX)[0];
				for (int i = 0; i < 12; i++)
				{
					string reg = $"s{i}";
					if (!usedSaveRegs.Contains(reg)) continue;
					if (code.Contains(reg)) usedSaveRegs.Add(reg);
				}
			}

			return usedSaveRegs;
		}
	}
}
