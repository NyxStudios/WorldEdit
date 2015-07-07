using System;
using System.IO;
using System.IO.Compression;
using Terraria;
using TShockAPI;
using WorldEdit.Expressions;

namespace WorldEdit.Commands
{
	public class Paste : WECommand
	{
		private int alignment;
		private Expression expression;

		public Paste(int x, int y, TSPlayer plr, int alignment, Expression expression)
			: base(x, y, Int32.MaxValue, Int32.MaxValue, plr)
		{
			this.alignment = alignment;
			this.expression = expression;
		}

		public override void Execute()
		{
			string clipboardPath = Tools.GetClipboardPath(plr.User.Name);
			using (var reader = new BinaryReader(new GZipStream(new FileStream(clipboardPath, FileMode.Open), CompressionMode.Decompress)))
			{
				reader.ReadInt32();
				reader.ReadInt32();

				int width = reader.ReadInt32() - 1;
				int height = reader.ReadInt32() - 1;

				if ((alignment & 1) == 0)
					x2 = x + width;
				else
				{
					x2 = x;
					x -= width;
				}
				if ((alignment & 2) == 0)
					y2 = y + height;
				else
				{
					y2 = y;
					y -= height;
				}
				
				Tools.PrepareUndo(x, y, x2, y2, plr);
				for (int i = x; i <= x2; i++)
				{
					for (int j = y; j <= y2; j++)
					{
						Tile tile = reader.ReadTile();
						if (i >= 0 && j >= 0 && i < Main.maxTilesX && j < Main.maxTilesY && (expression == null || expression.Evaluate(Main.tile[i, j])))
							Main.tile[i, j] = tile;
					}
				}
			}
			ResetSection();
			plr.SendSuccessMessage("Pasted clipboard to selection.");
		}
	}
}