using System;
using Terraria;
using TShockAPI;
using WorldEdit.Expressions;

namespace WorldEdit.Commands
{
	public class SetActuator : WECommand
	{
		private Expression expression;
		private bool state;
		private int actuator;

		public SetActuator(int x, int y, int x2, int y2, TSPlayer plr, bool state, Expression expression)
			: base(x, y, x2, y2, plr)
		{
			this.expression = expression ?? new TestExpression(new Test(t => true));
			this.state = state;
		}

		public override void Execute()
		{
			Tools.PrepareUndo(x, y, x2, y2, plr);
			int edits = 0;

				for (int i = x; i <= x2; i++)
				{
					for (int j = y; j <= y2; j++)
					{
						var tile = Main.tile[i, j];
						if (tile.actuator() != state && select(i, j, plr) && expression.Evaluate(tile))
						{
							tile.actuator(state);
							edits++;
						}
					}
				}
				ResetSection();
				plr.SendSuccessMessage("Set actuator. ({0})", edits);
				return;
		}
	}
}
