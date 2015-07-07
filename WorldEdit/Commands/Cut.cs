using System;
using System.IO;
using System.IO.Compression;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace WorldEdit.Commands
{
	public class Cut : WECommand
	{
		private const int BUFFER_SIZE = 1048576;

		public Cut(int x, int y, int x2, int y2, TSPlayer plr)
			: base(x, y, x2, y2, plr)
		{
		}

		public override void Execute()
		{
			foreach (string fileName in Directory.EnumerateFiles("worldedit", String.Format("redo-{0}-*.dat", plr.User.Name)))
				File.Delete(fileName);

			if (WorldEdit.Database.GetSqlType() == SqlType.Mysql)
				WorldEdit.Database.Query("INSERT IGNORE INTO WorldEdit VALUES (@0, -1, -1)", plr.User.Name);
			else
				WorldEdit.Database.Query("INSERT OR IGNORE INTO WorldEdit VALUES (@0, 0, 0)", plr.User.Name);
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = -1 WHERE Account = @0", plr.User.Name);
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = UndoLevel + 1 WHERE Account = @0", plr.User.Name);

			int undoLevel = 0;
			using (var reader = WorldEdit.Database.QueryReader("SELECT UndoLevel FROM WorldEdit WHERE Account = @0", plr.User.Name))
			{
				if (reader.Read())
					undoLevel = reader.Get<int>("UndoLevel");
			}

			string clipboardPath = Tools.GetClipboardPath(plr.User.Name);
			string undoPath = Path.Combine("worldedit", String.Format("undo-{0}-{1}.dat", plr.User.Name, undoLevel));
			// GZipStream is already buffered, but it's much faster to have a 1 MB buffer.
			using (var writer =
				new BinaryWriter(
					new BufferedStream(
						new GZipStream(File.Open(undoPath, FileMode.Create), CompressionMode.Compress), BUFFER_SIZE)))
			{
				writer.Write(x);
				writer.Write(y);
				writer.Write(x2 - x + 1);
				writer.Write(y2 - y + 1);

				for (int i = x; i <= x2; i++)
				{
					for (int j = y; j <= y2; j++)
					{
						writer.Write(Main.tile[i, j]);
						Main.tile[i, j] = new Tile();
					}
				}
			}
			File.Delete(clipboardPath);
			File.Copy(undoPath, clipboardPath);

			ResetSection();
			plr.SendSuccessMessage("Cut selection. ({0})", (x2 - x + 1) * (y2 - y + 1));
		}
	}
}
