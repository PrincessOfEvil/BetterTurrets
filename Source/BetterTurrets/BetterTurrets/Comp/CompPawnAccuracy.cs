using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace BetterTurrets
    {
    public class CompPawnAccuracy : ThingComp
        {
        private const int minStat = 1;
        private const int maxStat = 42; //Max accuracy you can get in vanilla: 20 + 12 (sight) + 5 (trait) + 3 (gunlink) + 1 (cornea) + 1 (rounding)

        public float accuracy = -666;
        public float accFloat {get => StatDefOf.ShootingAccuracyPawn.postProcessCurve.Evaluate(accuracy);}

        private CompProperties_PawnAccuracy Props => (CompProperties_PawnAccuracy)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
            {
            base.PostSpawnSetup(respawningAfterLoad);
            if (accuracy < minStat || accuracy > maxStat)
                {
                accuracy = Props.accuracy;
                }
            }

        public override void PostExposeData()
            {
            base.PostExposeData();
            Scribe_Values.Look(ref accuracy, "accuracy", Props.accuracy);
            }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
            {
            foreach (Gizmo item in base.CompGetGizmosExtra())
                {
                yield return item;
                }

            if (Prefs.DevMode)
                {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "ACC-0.5";
                command_Action.action = (Action)Delegate.Combine(command_Action.action, (Action)delegate
                    {
                    addAccuracy(-0.5f);
                    });
                command_Action.hotKey = KeyBindingDefOf.Misc1;
                yield return command_Action;

                Command_Action command_Action2 = new Command_Action();
                command_Action2.defaultLabel = "ACC+0.5";
                command_Action2.action = (Action)Delegate.Combine(command_Action2.action, (Action)delegate
                    {
                    addAccuracy(0.5f);
                    });
                command_Action2.hotKey = KeyBindingDefOf.Misc2;
                yield return command_Action2;

                Command_Action command_Action3 = new Command_Action();
                command_Action3.defaultLabel = "ACC=R";
                command_Action3.action = (Action)Delegate.Combine(command_Action3.action, (Action)delegate
                    {
                    accuracy = Props.accuracy;
                    parent.BroadcastCompSignal("AccuracyChanged");
                    });
                command_Action3.hotKey = KeyBindingDefOf.Misc3;
                yield return command_Action3;
                }
            }

        public override string CompInspectStringExtra()
            {
            StringBuilder stringBuilder = new StringBuilder();
            if (Prefs.DevMode)
                {
                stringBuilder.Append("SkillAccuracy".Translate() + ": ");
                stringBuilder.AppendLine((accuracy).ToString());
                stringBuilder.Append("StatAccuracy".Translate() + ": ");
                stringBuilder.AppendLine(accFloat.ToString());
                }
            return stringBuilder.ToString().TrimEndNewlines();
            }

        private void addAccuracy(float Y)
            {
            if (accuracy + Y <= maxStat && accuracy + Y >= minStat)
                {
                accuracy += Y;
                parent.BroadcastCompSignal("AccuracyChanged");
                }
            }
        }
    }