using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace BetterTurrets
{
    public class CompWatchableControl : ThingComp
	{
		private int maxRange;
		private int maxWidth;

		public IntRange distanceRange;
		public int distanceWidth;
        public bool drawPlaceWorkersWhileSelectedAnyway;

        private CompProperties_WatchableControl Props => (CompProperties_WatchableControl)props;

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			maxRange = Props.maxRange;
			maxWidth = Props.maxWidth;

			drawPlaceWorkersWhileSelectedAnyway = Props.drawPlaceWorkersWhileSelectedAnyway;
		}
		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (respawningAfterLoad) return;
			if (parent.def.building != null)
			{
				distanceRange = parent.def.building.watchBuildingStandDistanceRange;
				distanceWidth = parent.def.building.watchBuildingStandRectWidth;
			}
		}
		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref distanceRange, "distanceRange", new IntRange(1, 1));
			Scribe_Values.Look(ref distanceWidth, "distanceWidth", 1);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
			{
			foreach (Gizmo item in base.CompGetGizmosExtra())
				{
				yield return item;
				}

			Command_Action command_Action = new Command_Action();
			command_Action.defaultLabel = "X-1";
			command_Action.action = (Action)Delegate.Combine(command_Action.action, (Action)delegate
			{
				addWatchXOffset(-1);
			});
			command_Action.hotKey = KeyBindingDefOf.Misc1;
			yield return command_Action;

			Command_Action command_Action2 = new Command_Action();
			command_Action2.defaultLabel = "X+1";
			command_Action2.action = (Action)Delegate.Combine(command_Action2.action, (Action)delegate
			{
				addWatchXOffset(1);
			});
			command_Action2.hotKey = KeyBindingDefOf.Misc2;
			yield return command_Action2;

			Command_Action command_Action3 = new Command_Action();
			command_Action3.defaultLabel = "Y-1";
			command_Action3.action = (Action)Delegate.Combine(command_Action3.action, (Action)delegate
			{
				addWatchYOffset(-1);
			});
			command_Action3.hotKey = KeyBindingDefOf.Misc3;
			yield return command_Action3;

			Command_Action command_Action4 = new Command_Action();
			command_Action4.defaultLabel = "Y+1";
			command_Action4.action = (Action)Delegate.Combine(command_Action4.action, (Action)delegate
			{
				addWatchYOffset(1);
			});
			command_Action4.hotKey = KeyBindingDefOf.Misc4;
			yield return command_Action4;

			Command_Action command_Action5 = new Command_Action();
			command_Action5.defaultLabel = "X--";
			command_Action5.action = (Action)Delegate.Combine(command_Action5.action, (Action)delegate
			{
				addWatchXLength(-1);
			});
			command_Action5.hotKey = KeyBindingDefOf.Misc5;
			yield return command_Action5;

			Command_Action command_Action6 = new Command_Action();
			command_Action6.defaultLabel = "X++";
			command_Action6.action = (Action)Delegate.Combine(command_Action6.action, (Action)delegate
			{
				addWatchXLength(1);
			});
			command_Action6.hotKey = KeyBindingDefOf.Misc6;
			yield return command_Action6;
		}
        public override string CompInspectStringExtra()
		{
			if (Prefs.DevMode) { 
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append("Range: ");
				stringBuilder.AppendLine("[" + distanceRange.min + ":" + distanceRange.max + "]");
				stringBuilder.Append("Width: ");
				stringBuilder.Append(distanceWidth.ToString());

				return stringBuilder.ToString();
			}
			return null;
		}

			private void addWatchXOffset(int X)
		{
			if (distanceRange.max + X <= maxRange && distanceRange.min + X > 0)
				{
				distanceRange.min += X;
				distanceRange.max += X;
				}
		}
		private void addWatchXLength(int X)
		{
			if (distanceRange.max + X <= maxRange && distanceRange.max + X >= distanceRange.min && distanceRange.max + X - distanceRange.min < maxWidth)
			{
				distanceRange.max += X;
			}
		}

		private void addWatchYOffset(int Y)
		{
			if (distanceWidth + Y <= maxWidth && distanceWidth + Y > 0)
				{
				distanceWidth += Y;
				}
		}
    }
}
