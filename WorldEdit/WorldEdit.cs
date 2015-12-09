using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using WorldEdit.Commands;
using WorldEdit.Expressions;
using WorldEdit.Extensions;

namespace WorldEdit
{
	public delegate bool Selection(int i, int j, TSPlayer player);

	[ApiVersion(1, 22)]
	public class WorldEdit : TerrariaPlugin
	{
		public static Dictionary<string, int[]> Biomes = new Dictionary<string, int[]>();
		public static Dictionary<string, int> Colors = new Dictionary<string, int>();
		public static IDbConnection Database;
		public static Dictionary<string, Selection> Selections = new Dictionary<string, Selection>();
		public static Dictionary<string, int> Tiles = new Dictionary<string, int>();
		public static Dictionary<string, int> Walls = new Dictionary<string, int>();

		public override string Author
		{
			get { return "MarioE"; }
		}
		private CancellationTokenSource Cancel = new CancellationTokenSource();
		private BlockingCollection<WECommand> CommandQueue = new BlockingCollection<WECommand>();
		public override string Description
		{
			get { return "Adds commands for mass editing of blocks."; }
		}
		public override string Name
		{
			get { return "WorldEdit"; }
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public WorldEdit(Main game)
			: base(game)
		{
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);

				Cancel.Cancel();
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
		}

		void OnGetData(GetDataEventArgs e)
		{
			if (!e.Handled && e.MsgID == PacketTypes.Tile)
			{
				PlayerInfo info = TShock.Players[e.Msg.whoAmI].GetPlayerInfo();
				if (info.Point != 0)
				{
					using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
					{
						reader.ReadByte();
						int x = reader.ReadInt16();
						int y = reader.ReadInt16();
						if (x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY)
						{
							if (info.Point == 1)
							{
								info.X = x;
								info.Y = y;
								TShock.Players[e.Msg.whoAmI].SendInfoMessage("Set point 1.");
							}
							else if (info.Point == 2)
							{
								info.X2 = x;
								info.Y2 = y;
								TShock.Players[e.Msg.whoAmI].SendInfoMessage("Set point 2.");
							}
							else if (info.Point == 3)
							{
								List<string> regions = TShock.Regions.InAreaRegionName(x, y);
								if (regions.Count == 0)
								{
									TShock.Players[e.Msg.whoAmI].SendErrorMessage("No region exists there.");
									return;
								}
								Region curReg = TShock.Regions.GetRegionByName(regions[0]);
								info.X = curReg.Area.Left;
								info.Y = curReg.Area.Top;
								info.X2 = curReg.Area.Right;
								info.Y2 = curReg.Area.Bottom;
								TShock.Players[e.Msg.whoAmI].SendInfoMessage("Set region.");
							}
							info.Point = 0;
							e.Handled = true;
							TShock.Players[e.Msg.whoAmI].SendTileSquare(x, y, 3);
						}
					}
				}
			}
		}
		void OnInitialize(EventArgs e)
		{
			Directory.CreateDirectory("worldedit");

			#region Commands
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.all", All, "/all")
			{
				HelpText = "Sets the worldedit selection to the entire world."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.region.biome", Biome, "/biome")
			{
				HelpText = "Converts biomes in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.clipboard.copy", Copy, "/copy")
			{
				HelpText = "Copies the worldedit selection to the clipboard."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.clipboard.cut", Cut, "/cut")
			{
				HelpText = "Copies the worldedit selection to the clipboard, then deletes it."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.utils.drain", Drain, "/drain")
			{
				HelpText = "Drains liquids in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.utils.fixgrass", FixGrass, "/fixgrass")
			{
				HelpText = "Fixes suffocated grass in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.utils.fixhalves", FixHalves, "/fixhalves")
			{
				HelpText = "Fixes half blocks in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.utils.fixslopes", FixSlopes, "/fixslopes")
			{
				HelpText = "Fixes covered slopes in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.clipboard.flip", Flip, "/flip")
			{
				HelpText = "Flips the worldedit clipboard."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.utils.flood", Flood, "/flood")
			{
				HelpText = "Floods liquids in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.utils.mow", Mow, "/mow")
			{
				HelpText = "Mows grass, thorns, and vines in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.near", Near, "/near")
			{
				AllowServer = false,
				HelpText = "Sets the worldedit selection to a radius around you."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.region.paint", Paint, "/paint")
			{
				HelpText = "Paints tiles in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.region.paintwall", PaintWall, "/paintwall")
			{
				HelpText = "Paints walls in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.clipboard.paste", Paste, "/paste")
			{
				HelpText = "Pastes the clipboard to the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.point", Point1, "/point1")
			{
				HelpText = "Sets the positions of the worldedit selection's first point."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.point", Point2, "/point2")
			{
				HelpText = "Sets the positions of the worldedit selection's second point."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.history.redo", Redo, "/redo")
			{
				HelpText = "Redoes a number of worldedit actions."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.region", RegionCmd, "/region")
			{
				HelpText = "Selects a region as a worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.resize", Resize, "/resize")
			{
				HelpText = "Resizes the worldedit selection in a direction."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.clipboard.rotate", Rotate, "/rotate")
			{
				HelpText = "Rotates the worldedit clipboard."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.schematic", Schematic, "/schematic", "/schem")
			{
				HelpText = "Manages worldedit schematics."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.selecttype", Select, "/select")
			{
				HelpText = "Sets the worldedit selection function."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.region.set", Set, "/set")
			{
				HelpText = "Sets tiles in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.region.setwall", SetWall, "/setwall")
			{
				HelpText = "Sets walls in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.region.setwire", SetWire, "/setwire")
			{
				HelpText = "Sets wires in the worldedit selection."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.selection.shift", Shift, "/shift")
			{
				HelpText = "Shifts the worldedit selection in a direction."
			});
			TShockAPI.Commands.ChatCommands.Add(new Command("worldedit.history.undo", Undo, "/undo")
			{
				HelpText = "Undoes a number of worldedit actions."
			});
			#endregion
			#region Database
			switch (TShock.Config.StorageType.ToLowerInvariant())
			{
				case "mysql":
					string[] host = TShock.Config.MySqlHost.Split(':');
					Database = new MySqlConnection()
					{
						ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
							host[0],
							host.Length == 1 ? "3306" : host[1],
							TShock.Config.MySqlDbName,
							TShock.Config.MySqlUsername,
							TShock.Config.MySqlPassword)
					};
					break;
				case "sqlite":
					string sql = Path.Combine(TShock.SavePath, "worldedit.sqlite");
					Database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
					break;
			}

			var sqlcreator = new SqlTableCreator(Database,
				Database.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
			sqlcreator.EnsureTableStructure(new SqlTable("WorldEdit",
				new SqlColumn("Account", MySqlDbType.VarChar) { Primary = true, Length = 50 },
				new SqlColumn("RedoLevel", MySqlDbType.Int32),
				new SqlColumn("UndoLevel", MySqlDbType.Int32)));
			#endregion

			#region Biomes
			// Format: dirt, stone, ice, sand, grass, plants, tall plants, vines, thorn

			Biomes.Add("crimson", new[] { 0, 203, 200, 234, 199, -1, -1, 205, 32 });
			Biomes.Add("corruption", new[] { 0, 25, 163, 112, 23, 24, -1, -1, 32 });
			Biomes.Add("hallow", new[] { 0, 117, 164, 116, 109, 110, 113, 52, -1 });
			Biomes.Add("jungle", new[] { 59, 1, 161, 53, 60, 61, 74, 62, 69 });
			Biomes.Add("mushroom", new[] { 59, 1, 161, 53, 70, 71, -1, -1, -1 });
			Biomes.Add("normal", new[] { 0, 1, 161, 53, 2, 3, 73, 52, -1 });
			Biomes.Add("snow", new[] { 147, 161, 161, 53, 147, -1, -1, -1, -1 });
			#endregion
			#region Colors
			Colors.Add("blank", 0);

			Main.player[Main.myPlayer] = new Player();
			var item = new Item();
			for (int i = -48; i < Main.maxItemTypes; i++)
			{
				item.netDefaults(i);
				if (item.paint > 0)
					Colors.Add(item.name.Substring(0, item.name.Length - 6).ToLowerInvariant(), item.paint);
			}
			#endregion
			#region Selections
			Selections.Add("altcheckers", (i, j, plr) => ((i + j) & 1) == 0);
			Selections.Add("checkers", (i, j, plr) => ((i + j) & 1) == 1);
			Selections.Add("ellipse", (i, j, plr) =>
			{
				PlayerInfo info = plr.GetPlayerInfo();

				int X = Math.Min(info.X, info.X2);
				int Y = Math.Min(info.Y, info.Y2);
				int X2 = Math.Max(info.X, info.X2);
				int Y2 = Math.Max(info.Y, info.Y2);

				Vector2 center = new Vector2((float)(X2 - X) / 2, (float)(Y2 - Y) / 2);
				float major = Math.Max(center.X, center.Y);
				float minor = Math.Min(center.X, center.Y);
				if (center.Y > center.X)
				{
					float temp = major;
					major = minor;
					minor = temp;
				}
				return (i - center.X - X) * (i - center.X - X) / (major * major) + (j - center.Y - Y) * (j - center.Y - Y) / (minor * minor) <= 1;
			});
			Selections.Add("normal", (i, j, plr) => true);
			Selections.Add("outline", (i, j, plr) =>
			{
				PlayerInfo info = plr.GetPlayerInfo();
				return i == info.X || i == info.X2 || j == info.Y || j == info.Y2;
			});
			#endregion
			#region Tiles
			Tiles.Add("air", -1);
			Tiles.Add("lava", -2);
			Tiles.Add("honey", -3);
			Tiles.Add("water", -4);

			foreach (var fi in typeof(TileID).GetFields())
			{
				string name = fi.Name;
				var sb = new StringBuilder();
				for (int i = 0; i < name.Length; i++)
				{
					if (Char.IsUpper(name[i]))
						sb.Append(" ").Append(Char.ToLower(name[i]));
					else
						sb.Append(name[i]);
				}
				Tiles.Add(sb.ToString(1, sb.Length - 1), (ushort)fi.GetValue(null));
			}
			#endregion
			#region Walls
			Walls.Add("air", 0);

			foreach (var fi in typeof(WallID).GetFields())
			{
				string name = fi.Name;
				var sb = new StringBuilder();
				for (int i = 0; i < name.Length; i++)
				{
					if (Char.IsUpper(name[i]))
						sb.Append(" ").Append(Char.ToLower(name[i]));
					else
						sb.Append(name[i]);
				}
				Walls.Add(sb.ToString(1, sb.Length - 1), (byte)fi.GetValue(null));
			}
			#endregion

			ThreadPool.QueueUserWorkItem(QueueCallback);
		}

		void QueueCallback(object context)
		{
			while (!Netplay.disconnect)
			{
				WECommand command;
				try
				{
					if (!CommandQueue.TryTake(out command, -1, Cancel.Token))
						return;
					if (Main.rand == null)
						Main.rand = new Random();
					if (WorldGen.genRand == null)
						WorldGen.genRand = new Random();
					command.Position();
					command.Execute();
				}
				catch (OperationCanceledException)
				{
					return;
				}
			}
		}

		void All(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			info.X = info.Y = 0;
			info.X2 = Main.maxTilesX - 1;
			info.Y2 = Main.maxTilesY - 1;
			e.Player.SendSuccessMessage("Selected all tiles.");
		}
		void Biome(CommandArgs e)
		{
			if (e.Parameters.Count != 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //biome <biome 1> <biome 2>");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection.");
				return;
			}

			string biome1 = e.Parameters[0].ToLowerInvariant();
			string biome2 = e.Parameters[1].ToLowerInvariant();
			if (!Biomes.ContainsKey(biome1) || !Biomes.ContainsKey(biome2))
				e.Player.SendErrorMessage("Invalid biome.");
			else
				CommandQueue.Add(new Biome(info.X, info.Y, info.X2, info.Y2, e.Player, biome1, biome2));
		}
		void Copy(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection!");
			else
				CommandQueue.Add(new Copy(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void Cut(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection.");
			else
				CommandQueue.Add(new Cut(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void Drain(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection.");
			else
				CommandQueue.Add(new Drain(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void FixGrass(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection!");
			else
				CommandQueue.Add(new FixGrass(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void FixHalves(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection!");
			else
				CommandQueue.Add(new FixHalves(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void FixSlopes(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection!");
			else
				CommandQueue.Add(new FixSlopes(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void Flood(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //flood <liquid>");
				return;
			}

			int liquid = 0;
			if (String.Equals(e.Parameters[0], "lava", StringComparison.CurrentCultureIgnoreCase))
				liquid = 1;
			else if (String.Equals(e.Parameters[0], "honey", StringComparison.CurrentCultureIgnoreCase))
				liquid = 2;
			else if (!String.Equals(e.Parameters[0], "water", StringComparison.CurrentCultureIgnoreCase))
			{
				e.Player.SendErrorMessage("Invalid liquid type '{0}'!", e.Parameters[0]);
				return;
			}

			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection!");
			CommandQueue.Add(new Flood(info.X, info.Y, info.X2, info.Y2, e.Player, liquid));
		}
		void Flip(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //flip <direction>");
			else if (!Tools.HasClipboard(e.Player.User.Name))
				e.Player.SendErrorMessage("Invalid clipboard!");
			else
			{
				bool flipX = false;
				bool flipY = false;
				foreach (char c in e.Parameters[0].ToLowerInvariant())
				{
					if (c == 'x')
						flipX ^= true;
					else if (c == 'y')
						flipY ^= true;
					else
					{
						e.Player.SendErrorMessage("Invalid direction '{0}'!", c);
						return;
					}
				}
				CommandQueue.Add(new Flip(e.Player, flipX, flipY));
			}
		}
		void Mow(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
				e.Player.SendErrorMessage("Invalid selection!");
			else
				CommandQueue.Add(new Mow(info.X, info.Y, info.X2, info.Y2, e.Player));
		}
		void Near(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //near <radius>");
				return;
			}

			int radius;
			if (!int.TryParse(e.Parameters[0], out radius) || radius <= 0)
			{
				e.Player.SendErrorMessage("Invalid radius '{0}'!", e.Parameters[0]);
				return;
			}

			PlayerInfo info = e.Player.GetPlayerInfo();
			info.X = e.Player.TileX - radius;
			info.X2 = e.Player.TileX + radius + 1;
			info.Y = e.Player.TileY - radius;
			info.Y2 = e.Player.TileY + radius + 2;
			e.Player.SendSuccessMessage("Selected tiles around you!");
		}
		void Paint(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //paint <color> [where] [conditions...]");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			var colors = Tools.GetColorID(e.Parameters[0].ToLowerInvariant());
			if (colors.Count == 0)
				e.Player.SendErrorMessage("Invalid color '{0}'!", e.Parameters[0]);
			else if (colors.Count > 1)
				e.Player.SendErrorMessage("More than one color matched!");
			else
			{
				Expression expression = null;
				if (e.Parameters.Count > 1)
				{
					if (!Parser.TryParseTree(e.Parameters.Skip(1), out expression))
					{
						e.Player.SendErrorMessage("Invalid expression!");
						return;
					}
				}
				CommandQueue.Add(new Paint(info.X, info.Y, info.X2, info.Y2, e.Player, colors[0], expression));
			}
		}
		void PaintWall(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //paintwall <color> [where] [conditions...]");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			var colors = Tools.GetColorID(e.Parameters[0].ToLowerInvariant());
			if (colors.Count == 0)
				e.Player.SendErrorMessage("Invalid color '{0}'!", e.Parameters[0]);
			else if (colors.Count > 1)
				e.Player.SendErrorMessage("More than one color matched!");
			else
			{
				Expression expression = null;
				if (e.Parameters.Count > 1)
				{
					if (!Parser.TryParseTree(e.Parameters.Skip(1), out expression))
					{
						e.Player.SendErrorMessage("Invalid expression!");
						return;
					}
				}
				CommandQueue.Add(new PaintWall(info.X, info.Y, info.X2, info.Y2, e.Player, colors[0], expression));
			}
		}
		void Paste(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			e.Player.SendInfoMessage("X: {0}, Y: {1}", info.X, info.Y);
			if (info.X == -1 || info.Y == -1)
				e.Player.SendErrorMessage("Invalid first point!");
			else if (!Tools.HasClipboard(e.Player.User.Name))
				e.Player.SendErrorMessage("Invalid clipboard!");
			else
			{
				int alignment = 0;
				if (e.Parameters.Count == 1)
				{
					foreach (char c in e.Parameters[0].ToLowerInvariant())
					{
						if (c == 'l')
							alignment &= 2;
						else if (c == 'r')
							alignment |= 1;
						else if (c == 't')
							alignment &= 1;
						else if (c == 'b')
							alignment |= 2;
						else
						{
							e.Player.SendErrorMessage("Invalid paste alignment '{0}'!", c);
							return;
						}
					}
				}

				Expression expression = null;
				if (e.Parameters.Count > 1)
				{
					if (!Parser.TryParseTree(e.Parameters.Skip(1), out expression))
					{
						e.Player.SendErrorMessage("Invalid expression!");
						return;
					}
				}
				CommandQueue.Add(new Paste(info.X, info.Y, e.Player, alignment, expression));
			}
		}
		void Point1(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (e.Parameters.Count == 0)
			{
				if (!e.Player.RealPlayer)
					e.Player.SendErrorMessage("You must use this command in-game.");
				else
				{
					info.Point = 1;
					e.Player.SendInfoMessage("Modify a block to set point 1.");
				}
				return;
			}
			if (e.Parameters.Count != 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //point1 <x> <y>");
				return;
			}

			int x, y;
			if (!int.TryParse(e.Parameters[0], out x) || x < 0 || x >= Main.maxTilesX
				|| !int.TryParse(e.Parameters[1], out y) || y < 0 || y >= Main.maxTilesY)
			{
				e.Player.SendErrorMessage("Invalid coordinates.");
				return;
			}

			info.X = x;
			info.Y = y;
			e.Player.SendInfoMessage("Set point 1.");
		}
		void Point2(CommandArgs e)
		{
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (e.Parameters.Count == 0)
			{
				if (!e.Player.RealPlayer)
					e.Player.SendErrorMessage("You must use this command in-game.");
				else
				{
					info.Point = 2;
					e.Player.SendInfoMessage("Modify a block to set point 2.");
				}
				return;
			}
			if (e.Parameters.Count != 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //point2 [x] [y]");
				return;
			}

			int x, y;
			if (!int.TryParse(e.Parameters[0], out x) || x < 0 || x >= Main.maxTilesX
				|| !int.TryParse(e.Parameters[1], out y) || y < 0 || y >= Main.maxTilesY)
			{
				e.Player.SendErrorMessage("Invalid coordinates '({0}, {1})'!", e.Parameters[0], e.Parameters[1]);
				return;
			}

			info.X2 = x;
			info.Y2 = y;
			e.Player.SendInfoMessage("Set point 2.");
		}
		void Redo(CommandArgs e)
		{
			if (e.Parameters.Count > 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //redo [steps] [account]");
				return;
			}

			int steps = 1;
			if (e.Parameters.Count > 0 && (!int.TryParse(e.Parameters[0], out steps) || steps <= 0))
				e.Player.SendErrorMessage("Invalid redo steps '{0}'!", e.Parameters[0]);
			else
				CommandQueue.Add(new Redo(e.Player, e.Parameters.Count > 1 ? e.Parameters[1] : e.Player.User.Name, steps));
		}
		void RegionCmd(CommandArgs e)
		{
			if (e.Parameters.Count > 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //region [region name]");
				return;
			}

			PlayerInfo info = e.Player.GetPlayerInfo();
			if (e.Parameters.Count == 0)
			{
				info.Point = 3;
				e.Player.SendInfoMessage("Hit a block to select that region.");
			}
			else
			{
				Region region = TShock.Regions.GetRegionByName(e.Parameters[0]);
				if (region == null)
					e.Player.SendErrorMessage("Invalid region '{0}'!", e.Parameters[0]);
				else
				{
					info.X = region.Area.Left;
					info.Y = region.Area.Top;
					info.X2 = region.Area.Right;
					info.Y2 = region.Area.Bottom;
					e.Player.SendSuccessMessage("Set selection to region '{0}'.", region.Name);
				}
			}
		}
		void Resize(CommandArgs e)
		{
			if (e.Parameters.Count != 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //resize <direction(s)> <amount>");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			int amount;
			if (!int.TryParse(e.Parameters[1], out amount))
			{
				e.Player.SendErrorMessage("Invalid resize amount '{0}'!", e.Parameters[0]);
				return;
			}

			foreach (char c in e.Parameters[0].ToLowerInvariant())
			{
				if (c == 'd')
				{
					if (info.Y < info.Y2)
						info.Y2 += amount;
					else
						info.Y += amount;
				}
				else if (c == 'l')
				{
					if (info.X < info.X2)
						info.X -= amount;
					else
						info.X2 -= amount;
				}
				else if (c == 'r')
				{
					if (info.X < info.X2)
						info.X2 += amount;
					else
						info.X += amount;
				}
				else if (c == 'u')
				{
					if (info.Y < info.Y2)
						info.Y -= amount;
					else
						info.Y2 -= amount;
				}
				else
				{
					e.Player.SendErrorMessage("Invalid direction '{0}'!", c);
					return;
				}
			}
			e.Player.SendSuccessMessage("Resized selection.");
		}
		void Rotate(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //rotate <angle>");
				return;
			}
			if (!Tools.HasClipboard(e.Player.User.Name))
			{
				e.Player.SendErrorMessage("Invalid clipboard!");
				return;
			}

			int degrees;
			if (!int.TryParse(e.Parameters[0], out degrees) || degrees % 90 != 0)
				e.Player.SendErrorMessage("Invalid angle '{0}'!", e.Parameters[0]);
			else
				CommandQueue.Add(new Rotate(e.Player, degrees));
		}
		void Schematic(CommandArgs e)
		{
			string subCmd = e.Parameters.Count == 0 ? "help" : e.Parameters[0].ToLowerInvariant();
			switch (subCmd)
			{
				case "del":
				case "delete":
					{
						if (e.Parameters.Count != 2)
						{
							e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //schematic delete <name>");
							return;
						}
						string schematicPath = Path.Combine("worldedit", String.Format("schematic-{0}.dat", e.Parameters[1]));
						if (!File.Exists(schematicPath))
						{
							e.Player.SendErrorMessage("Invalid schematic '{0}'!");
							return;
						}
						File.Delete(schematicPath);
						e.Player.SendErrorMessage("Deleted schematic '{0}'.", e.Parameters[1]);
					}
					return;
				case "help":
					e.Player.SendSuccessMessage("Schematics Subcommands:");
					e.Player.SendInfoMessage("//schematic delete <name>");
					e.Player.SendInfoMessage("//schematic list [page]");
					e.Player.SendInfoMessage("//schematic load <name>");
					e.Player.SendInfoMessage("//schematic save <name>");
					return;
				case "list":
					{
						if (e.Parameters.Count > 2)
						{
							e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //schematic list [page]");
							return;
						}

						int pageNumber;
						if (!PaginationTools.TryParsePageNumber(e.Parameters, 1, e.Player, out pageNumber))
							return;

						var schematics = from s in Directory.EnumerateFiles("worldedit", "schematic-*.dat")
										 select s.Substring(20, s.Length - 24);
						PaginationTools.SendPage(e.Player, pageNumber, PaginationTools.BuildLinesFromTerms(schematics),
							new PaginationTools.Settings
							{
								HeaderFormat = "Schematics ({0}/{1}):",
								FooterFormat = "Type //schematic list {0} for more."
							});
					}
					return;
				case "load":
					{
						if (e.Parameters.Count != 2)
						{
							e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //schematic load <name>");
							return;
						}
						string schematicPath = Path.Combine("worldedit", String.Format("schematic-{0}.dat", e.Parameters[1]));
						if (!File.Exists(schematicPath))
						{
							e.Player.SendErrorMessage("Invalid schematic '{0}'!");
							return;
						}

						string clipboardPath = Path.Combine("worldedit", String.Format("clipboard-{0}.dat", e.Player.User.Name));
						File.Copy(schematicPath, clipboardPath, true);
						e.Player.SendSuccessMessage("Loaded schematic '{0}' to clipboard.", e.Parameters[1]);
					}
					return;
				case "save":
					{
						if (e.Parameters.Count != 2)
						{
							e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //schematic save <name>");
							return;
						}
						string clipboardPath = Path.Combine("worldedit", String.Format("clipboard-{0}.dat", e.Player.User.Name));
						if (!File.Exists(clipboardPath))
						{
							e.Player.SendErrorMessage("Invalid clipboard!");
							return;
						}

						string schematicPath = Path.Combine("worldedit", String.Format("schematic-{0}.dat", e.Parameters[1]));
						File.Copy(clipboardPath, schematicPath, true);
						e.Player.SendSuccessMessage("Saved clipboard to schematic '{0}'.", e.Parameters[1]);
					}
					return;
				default:
					e.Player.SendErrorMessage("Invalid subcommand.");
					return;
			}
		}
		void Select(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //select <selection type>");
				return;
			}

			string selection = e.Parameters[0].ToLowerInvariant();
			if (!Selections.ContainsKey(selection))
			{
				e.Player.SendErrorMessage("Invalid selection type '{0}'!", selection);
				return;
			}
			e.Player.GetPlayerInfo().Select = Selections[selection];
			e.Player.SendSuccessMessage("Set selection type to '{0}'.", selection);
		}
		void Set(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //set <tile> [=> boolean expr...]");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			var tiles = Tools.GetTileID(e.Parameters[0].ToLowerInvariant());
			if (tiles.Count == 0)
				e.Player.SendErrorMessage("Invalid tile '{0}'!", e.Parameters[0]);
			else if (tiles.Count > 1)
				e.Player.SendErrorMessage("More than one tile matched!");
			else
			{
				Expression expression = null;
				if (e.Parameters.Count > 1)
				{
					if (!Parser.TryParseTree(e.Parameters.Skip(1), out expression))
					{
						e.Player.SendErrorMessage("Invalid expression!");
						return;
					}
				}
				CommandQueue.Add(new Set(info.X, info.Y, info.X2, info.Y2, e.Player, tiles[0], expression));
			}
		}
		void SetWall(CommandArgs e)
		{
			if (e.Parameters.Count == 0)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //setwall <wall> [=> boolean expr...]");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			var walls = Tools.GetWallID(e.Parameters[0].ToLowerInvariant());
			if (walls.Count == 0)
				e.Player.SendErrorMessage("Invalid wall '{0}'!", e.Parameters[0]);
			else if (walls.Count > 1)
				e.Player.SendErrorMessage("More than one wall matched!");
			else
			{
				Expression expression = null;
				if (e.Parameters.Count > 1)
				{
					if (!Parser.TryParseTree(e.Parameters.Skip(1), out expression))
					{
						e.Player.SendErrorMessage("Invalid expression!");
						return;
					}
				}
				CommandQueue.Add(new SetWall(info.X, info.Y, info.X2, info.Y2, e.Player, walls[0], expression));
			}
		}
		void SetWire(CommandArgs e)
		{
			if (e.Parameters.Count < 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //setwire <wire> <wire state> [=> boolean expr...]");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			int wire;
			if (!int.TryParse(e.Parameters[0], out wire) || wire < 1 || wire > 3)
			{
				e.Player.SendErrorMessage("Invalid wire '{0}'!", e.Parameters[0]);
				return;
			}

			bool state = false;
			if (String.Equals(e.Parameters[1], "on", StringComparison.CurrentCultureIgnoreCase))
				state = true;
			else if (!String.Equals(e.Parameters[1], "off", StringComparison.CurrentCultureIgnoreCase))
			{
				e.Player.SendErrorMessage("Invalid wire state '{0}'!", e.Parameters[1]);
				return;
			}

			Expression expression = null;
			if (e.Parameters.Count > 2)
			{
				if (!Parser.TryParseTree(e.Parameters.Skip(2), out expression))
				{
					e.Player.SendErrorMessage("Invalid expression!");
					return;
				}
			}
			CommandQueue.Add(new SetWire(info.X, info.Y, info.X2, info.Y2, e.Player, wire, state, expression));
		}
		void Shift(CommandArgs e)
		{
			if (e.Parameters.Count != 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //shift <direction> <amount>");
				return;
			}
			PlayerInfo info = e.Player.GetPlayerInfo();
			if (info.X == -1 || info.Y == -1 || info.X2 == -1 || info.Y2 == -1)
			{
				e.Player.SendErrorMessage("Invalid selection!");
				return;
			}

			int amount;
			if (!int.TryParse(e.Parameters[1], out amount) || amount < 0)
			{
				e.Player.SendErrorMessage("Invalid shift amount '{0}'!", e.Parameters[0]);
				return;
			}

			foreach (char c in e.Parameters[0].ToLowerInvariant())
			{
				if (c == 'd')
				{
					info.Y += amount;
					info.Y2 += amount;
				}
				else if (c == 'l')
				{
					info.X -= amount;
					info.X2 -= amount;
				}
				else if (c == 'r')
				{
					info.X += amount;
					info.X2 += amount;
				}
				else if (c == 'u')
				{
					info.Y -= amount;
					info.Y2 -= amount;
				}
				else
				{
					e.Player.SendErrorMessage("Invalid direction '{0}'!", c);
					return;
				}
			}
			e.Player.SendSuccessMessage("Shifted selection.");
		}
		void Undo(CommandArgs e)
		{
			if (e.Parameters.Count > 2)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: //undo [steps] [account]");
				return;
			}

			int steps = 1;
			if (e.Parameters.Count > 0 && (!int.TryParse(e.Parameters[0], out steps) || steps <= 0))
				e.Player.SendErrorMessage("Invalid undo steps '{0}'!", e.Parameters[0]);
			else
				CommandQueue.Add(new Undo(e.Player, e.Parameters.Count > 1 ? e.Parameters[1] : e.Player.User.Name, steps));
		}
	}
}
