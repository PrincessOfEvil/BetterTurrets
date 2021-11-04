using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterTurrets
{
    public class CompProperties_WatchableControl : CompProperties
    {
        public int maxRange = 45;
        public int maxWidth = 5;
        public bool drawPlaceWorkersWhileSelectedAnyway = false;
        public CompProperties_WatchableControl()
        {
            compClass = typeof(CompWatchableControl);
        }
    }
}
