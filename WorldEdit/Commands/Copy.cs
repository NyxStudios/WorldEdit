using System;
using TShockAPI;

namespace WorldEdit.Commands
{
	public class Copy : WECommand
	{
		public Copy(int x, int y, int x2, int y2, TSPlayer plr)
			: base(x, y, x2, y2, plr)
		{
		}

		public override void Execute()
		{
			string clipboardPath = Tools.GetClipboardPath(plr.User.Name);
			Tools.SaveWorldSection(x, y, x2, y2, clipboardPath);

			plr.SendSuccessMessage("Copied selection to clipboard.");
		}
	}
}