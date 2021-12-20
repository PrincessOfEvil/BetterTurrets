using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace BetterTurrets
	{
	public class WorkGiver_RefuelIC : WorkGiver_Refuel
		{
		private JobDef jobStandard;
		public override JobDef JobStandard => jobStandard ??= DefDatabase<JobDef>.GetNamedSilentFail("TurretWeaponBase_RefuelIC");

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
			{
			CompRefuelable compRefuelable = t.TryGetComp<CompInternalCombustion>();
			if (compRefuelable == null || compRefuelable.IsFull || (!forced && !compRefuelable.allowAutoRefuel))
				{
				return false;
				}
			if (compRefuelable.FuelPercentOfMax > 0f && !compRefuelable.Props.allowRefuelIfNotEmpty)
				{
				return false;
				}
			if (!forced && !compRefuelable.ShouldAutoRefuelNow)
				{
				return false;
				}
			if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
				{
				return false;
				}
			if (t.Faction != pawn.Faction)
				{
				return false;
				}
			if (FindBestFuel(pawn, t) == null)
				{
				ThingFilter fuelFilter = compRefuelable.Props.fuelFilter;
				JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter.Summary));
				return false;
				}
			if (compRefuelable.Props.atomicFueling)
				{
				ThingFilter fuelFilter2 = compRefuelable.Props.fuelFilter;
				JobFailReason.Is("AtomicRefuelingDisabled");
				return false;
				}
			return true;
			}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
			{
			Thing thing = FindBestFuel(pawn, t);
			return JobMaker.MakeJob(JobStandard, t, thing);
			}

		// This is the point from which i'm just copypasting parts of RefuelWorkGiverUtility
		private static Thing FindBestFuel(Pawn pawn, Thing refuelable)
			{
			ThingFilter filter = refuelable.TryGetComp<CompInternalCombustion>().Props.fuelFilter;
			Predicate<Thing> validator = delegate (Thing x)
				{
					if (x.IsForbidden(pawn) || !pawn.CanReserve(x))
						{
						return false;
						}
					return filter.Allows(x) ? true : false;
					};
			return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, filter.BestThingRequest, PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator);
			}
		}

	public class JobDriver_RefuelIC : JobDriver_Refuel
        {
        protected new CompRefuelable RefuelableComp => Refuelable.TryGetComp<CompInternalCombustion>();

		protected override IEnumerable<Toil> MakeNewToils()
			{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			base.AddEndCondition(() => (!RefuelableComp.IsFull) ? JobCondition.Ongoing : JobCondition.Succeeded);
			base.AddFailCondition(() => !job.playerForced && !RefuelableComp.ShouldAutoRefuelNowIgnoringFuelPct);
			base.AddFailCondition(() => !RefuelableComp.allowAutoRefuel && !job.playerForced);
			yield return Toils_General.DoAtomic(delegate
				{
				job.count = RefuelableComp.GetFuelCountToFullyRefuel();
				});
			Toil reserveFuel = Toils_Reserve.Reserve(TargetIndex.B);
			yield return reserveFuel;
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(TargetIndex.B);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFuel, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Wait(240).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A)
				.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
				.WithProgressBarToilDelay(TargetIndex.A);

			Toil toil = new Toil();
			toil.initAction = delegate
				{
				if (job.placedThings.NullOrEmpty())
					{
					RefuelableComp.Refuel(new List<Thing> { job.GetTarget(TargetIndex.B).Thing });
					}
				else
					{
					RefuelableComp.Refuel(job.placedThings.Select((ThingCountClass p) => p.thing).ToList());
					}
				};
			toil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil;
			}
		}
	}
