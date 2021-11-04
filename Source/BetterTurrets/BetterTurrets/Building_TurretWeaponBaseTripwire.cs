using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TurretWeaponBase;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BetterTurrets
    {
    public class Building_TurretWeaponBaseTripwire : Building_TurretWeaponBase, IAttackTarget
	{

		public CompWatchableControl compWatchableControl;
		private ThingDef fakeDef;

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			compWatchableControl = GetComp<CompWatchableControl>();
			if (compWatchableControl != null)
			{
				fakeDef = new ThingDef();
				fakeDef.building = new BuildingProperties();
				fakeDef.building.watchBuildingInSameRoom = this.def.building.watchBuildingInSameRoom;
			}
		}

		protected override bool IsValidTarget(Thing t)
		{ 
			if (!base.IsValidTarget(t)) return false;
			
			Map currentMap = Find.CurrentMap;
			IEnumerable<IntVec3> tripwireCells;
			if (compWatchableControl != null)
			{
				fakeDef.building.watchBuildingStandDistanceRange = compWatchableControl.distanceRange;
				fakeDef.building.watchBuildingStandRectWidth = compWatchableControl.distanceWidth;

				tripwireCells = WatchBuildingUtility.CalculateWatchCells(fakeDef, this.Position, this.Rotation, currentMap);
			}
			else
				tripwireCells = WatchBuildingUtility.CalculateWatchCells(this.def, this.Position, this.Rotation, currentMap);

			return tripwireCells.Contains(t.Position);
		}

		public override void DrawExtraSelectionOverlays()
		{
			base.DrawExtraSelectionOverlays();
			if (!this.def.drawPlaceWorkersWhileSelected && def.PlaceWorkers != null && compWatchableControl.drawPlaceWorkersWhileSelectedAnyway)
			{
				for (int i = 0; i < def.PlaceWorkers.Count; i++)
				{
					if (def.PlaceWorkers[i] is PlaceWorker_WatchArea)
					{
						fakeDef.building.watchBuildingStandDistanceRange = compWatchableControl.distanceRange;
						fakeDef.building.watchBuildingStandRectWidth = compWatchableControl.distanceWidth;

						def.PlaceWorkers[i].DrawGhost(fakeDef, Position, Rotation, Color.white, this);
					}
					else
						def.PlaceWorkers[i].DrawGhost(this.def, Position, Rotation, Color.white, this);
				}
			}
		}

		#region misc


		/// <summary>
		/// Either the turret is hidden or already fired. Either way, should be invisible to enemies.
		/// </summary>
		bool IAttackTarget.ThreatDisabled(IAttackTargetSearcher disabledFor)
		{
			//Log.Message("Threat disabled!");
			return true;
		}

		

		public bool KnowsOfTrap(Pawn p)
		{
			if (p.Faction != null && !p.Faction.HostileTo(base.Faction))
			{
				return true;
			}
			if (p.Faction == null && p.RaceProps.Animal && !p.InAggroMentalState)
			{
				return true;
			}
			if (p.guest != null && p.guest.Released)
			{
				return true;
			}
			if (!p.IsPrisoner && base.Faction != null && p.HostFaction == base.Faction)
			{
				return true;
			}
			if (p.RaceProps.Humanlike && p.IsFormingCaravan())
			{
				return true;
			}
			if (p.IsPrisoner && p.guest.ShouldWaitInsteadOfEscaping && base.Faction == p.HostFaction)
			{
				return true;
			}
			if (p.Faction == null && p.RaceProps.Humanlike)
			{
				return true;
			}
			return false;
		}

		public override ushort PathFindCostFor(Pawn p)
		{
			if (!KnowsOfTrap(p))
			{
				return 0;
			}
			return 800;
		}

		public override ushort PathWalkCostFor(Pawn p)
		{
			if (!KnowsOfTrap(p))
			{
				return 0;
			}
			return 40;
		}

		public override bool IsDangerousFor(Pawn p)
		{
			return KnowsOfTrap(p);
		}
        #endregion
    }
}
