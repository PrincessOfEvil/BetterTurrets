using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterTurrets
    {
    public class CompInternalCombustion : CompRefuelable
        {
        private CompFlickable flickComp;
        public bool PowerOn => HasFuel && (flickComp?.SwitchIsOn ?? true);

        public override void Initialize(CompProperties props)
            {
            base.Initialize(props);
            this.flickComp = base.parent.GetComp<CompFlickable>();
            }
        }
    }
