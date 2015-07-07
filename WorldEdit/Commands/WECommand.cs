using System;
using System.Linq;
using System.Threading;
using Terraria;
using TShockAPI;
using WorldEdit.Expressions;
using WorldEdit.Extensions;

namespace WorldEdit.Commands
{
	public abstract class WECommand
	{
		public TSPlayer plr;
		public Selection select;
		public int x;
		public int x2;
		public int y;
		public int y2;

		protected WECommand(int x, int y, int x2, int y2, TSPlayer plr)
		{
			this.plr = plr;
			this.select = plr.GetPlayerInfo().Select ?? WorldEdit.Selections["normal"];
			this.x = x;
			this.x2 = x2;
			this.y = y;
			this.y2 = y2;
		}

		public abstract void Execute();
		public void Position()
		{
			int temp;
			x = Math.Max(x, 0);
			y = Math.Max(y, 0);
			x2 = Math.Min(x2, Main.maxTilesX - 1);
			y2 = Math.Min(y2, Main.maxTilesY - 1);
			
			if (x > x2)
			{
				temp = x2;
				x2 = x;
				x = temp;
			}
			if (y > y2)
			{
				temp = y2;
				y2 = y;
				y = temp;
			}
		}
		public void ResetSection()
		{
			int lowX = Netplay.GetSectionX(x);
			int highX = Netplay.GetSectionX(x2);
			int lowY = Netplay.GetSectionY(y);
			int highY = Netplay.GetSectionY(y2);
			foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive))
			{
				for (int i = lowX; i <= highX; i++)
				{
					for (int j = lowY; j <= highY; j++)
						sock.TileSections[i, j] = false;
				}
			}
		}
		public void SetTile(int i, int j, int tileType)
		{
			var tile = Main.tile[i, j];
			switch (tileType)
			{
				case -1:
					tile.active(false);
					tile.frameX = -1;
					tile.frameY = -1;
					tile.liquidType(0);
					tile.liquid = 0;
					tile.type = 0;
					return;
				case -2:
					tile.active(false);
					tile.liquidType(1);
					tile.liquid = 255;
					tile.type = 0;
					return;
				case -3:
					tile.active(false);
					tile.liquidType(2);
					tile.liquid = 255;
					tile.type = 0;
					return;
				case -4:
					tile.active(false);
					tile.liquidType(0);
					tile.liquid = 255;
					tile.type = 0;
					return;
				default:
					if (Main.tileFrameImportant[tileType])
						WorldGen.PlaceTile(i, j, tileType);
					else
					{
						tile.active(true);
						tile.frameX = -1;
						tile.frameY = -1;
						tile.liquidType(0);
						tile.liquid = 0;
						tile.slope(0);
						tile.type = (ushort)tileType;
					}
					return;
			}
		}
		public bool TileSolid(int x, int y)
		{
			return x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY || (Main.tile[x, y].active() && Main.tileSolid[Main.tile[x, y].type]);
		}
	}
}