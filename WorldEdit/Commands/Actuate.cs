using System;
using System.Diagnostics;
using Terraria;
using TShockAPI;
using WorldEdit.Expressions;

namespace WorldEdit.Commands {
    public class Actuate:WECommand {
        private Expression expression;
        private int actuateType;

        public Actuate(int x,int y,int x2,int y2,TSPlayer plr,int actType,Expression expression)
            : base(x,y,x2,y2,plr) {
            this.actuateType = actType;
            this.expression = expression ?? new TestExpression(new Test(t => true));
        }

        public override void Execute() {
            Tools.PrepareUndo(x,y,x2,y2,plr);
            int edits = 0;
            switch(actuateType) {
                case 0:
                    for(int i = x;i <= x2;i++) {
                        for(int j = y;j <= y2;j++) {
                            var tile = Main.tile[i,j];
                            if(select(i,j,plr) && expression.Evaluate(tile)) {
                                tile.inActive(true);
                                edits++;
                            }
                        }
                    }
                    ResetSection();
                    plr.SendSuccessMessage("Actuated tiles. ({0})",edits);
                    break;
                case 1:
                    for(int i = x;i <= x2;i++) {
                        for(int j = y;j <= y2;j++) {
                            var tile = Main.tile[i,j];
                            if(select(i,j,plr) && expression.Evaluate(tile)) {
                                tile.inActive(false);
                                edits++;
                            }
                        }
                    }
                    ResetSection();
                    plr.SendSuccessMessage("Set tiles' actuate status off. ({0})",edits);
                    break;
                case 2:
                    for(int i = x;i <= x2;i++) {
                        for(int j = y;j <= y2;j++) {
                            var tile = Main.tile[i,j];
                            if(select(i,j,plr) && expression.Evaluate(tile)) {
                                tile.inActive(!tile.inActive());
                                edits++;
                            }
                        }
                    }
                    ResetSection();
                    plr.SendSuccessMessage("Reversed tiles' actuate status. ({0})",edits);
                    break;
            }
        }
    }
}