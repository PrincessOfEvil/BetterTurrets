using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterTurrets
    {
    public class CompProperties_PawnAccuracy : CompProperties
        {
        public float accuracy = 5;
        public CompProperties_PawnAccuracy()
            {
            compClass = typeof(CompPawnAccuracy);
            }
        }
    }
