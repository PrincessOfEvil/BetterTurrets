using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace TurretWeaponBase
    {
    public class JobDriver_UninstallTurretWeapon : JobDriver
        {
        private const TargetIndex TurretCellInd = TargetIndex.A;
        public JobDriver_UninstallTurretWeapon() { }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
            {
            return base.pawn.Reserve(base.job.GetTarget(TurretCellInd), base.job, 1, -1, null, errorOnFailed);
            }
        protected override IEnumerable<Toil> MakeNewToils()
            {
            //Set fail conditions
            this.FailOnBurningImmobile(TurretCellInd);

            //Reserve target storage cell, if it is a storage
            bool targetIsStorage = StoreUtility.GetSlotGroup(pawn.jobs.curJob.GetTarget(TurretCellInd).Cell, Map) != null;
            if (targetIsStorage)
                yield return Toils_Reserve.Reserve(TurretCellInd, 1);

            yield return Toils_Goto.GotoThing(TurretCellInd, PathEndMode.ClosestTouch);
            yield return JobDriver_HaulToCellAndDoWork.Toils_WaitWithSoundAndEffect(180, "Interact_ConstructMetal", "ConstructMetal", TurretCellInd);


            yield return Toils_TryToRemoveFromWeaponBase(pawn, TurretCellInd);
            }
        private Toil Toils_TryToRemoveFromWeaponBase(Pawn actor, TargetIndex turret)
            {

            IntVec3 targetCell = pawn.jobs.curJob.GetTarget(turret).Cell;

            IEnumerable<Building_TurretWeaponBase> foundBuildings = Map.listerBuildings.AllBuildingsColonistOfClass<Building_TurretWeaponBase>();

            if (foundBuildings == null)
                return null;

            Building_TurretWeaponBase weaponBase = foundBuildings.Where(b => b.Position == targetCell).FirstOrDefault();

            if (weaponBase == null)
                return null;

            Toil toil = new Toil();
            toil.initAction = () =>
            {
                weaponBase.DeconstructGunAndReset();
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.defaultDuration = 0;

            return toil;
            }

        }
    }
