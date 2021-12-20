using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterTurrets
    {
    public class StatPart_AccuracyShift : StatPart
        {
        private StatDef stat;
        protected StatDef Stat => stat ??= DefDatabase<StatDef>.GetNamedSilentFail("BetterTurrets_ShootingAccuracyTurret");

        public override void TransformValue(StatRequest req, ref float val)
            {
            float unfinalizedValue = StatDefOf.ShootingAccuracyPawn.postProcessCurve.EvaluateInverted(val) + Stat.Worker.GetValue(req);
            val = StatDefOf.ShootingAccuracyPawn.postProcessCurve.Evaluate(unfinalizedValue);
            }

        public override string ExplanationPart(StatRequest req)
            {
            return "BetterTurrets_StatPart_AccuracyShift".Translate() + ": " + Stat.Worker.GetValueUnfinalized(req)/*.ToStringWithSign()*/ + "\n" +
                Stat.GetStatPart<StatPart_Quality_Offset>().ExplanationPart(req);
            }
        }
    }
