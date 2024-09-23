using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using BetterTurrets;

//using RimWorld.Planet;
//using RimWorld.SquadAI;

//using CommonMisc;

namespace TurretWeaponBase
{
    /// <summary>
    /// This is a turret base that allows you to equip it with a weapon of your choice 
    /// </summary>
    /// <author>Haplo</author>
    /// <permission>Please check the provided license info for granted permissions.</permission>
    public class Building_TurretWeaponBase : Building_Turret
    {

        #region Variables

        public Thing gun;
        protected TurretTop_TurretWeaponBase top;

        protected CompPowerTrader powerComp;
        protected CompMannable mannableComp;
        protected CompInternalCombustion intCombComp;
        public bool loaded;

        protected LocalTargetInfo currentTargetInt = LocalTargetInfo.Invalid;
     
        protected int burstWarmupTicksLeft;
        protected int burstCooldownTicksLeft;

        private       bool collectingGunAllowed;
        private       int  counterSearchForGun;
        private const int  counterSearchForGunMax = 60;
        private const int  floatMenuMaxEntries    = 10;
        private       int  floatMenuShowEntries;

        private bool disallowSlowingGuns;

        private int overrideRefuelPerBurst;

        private bool forceCreateGunAndTop;

        public enum TopMatType
        {
            BuildingMat = 0,
            ShortMediumLongMat = 1,
            GunMat = 2,
            RangeBurstMat = 3
            }
        public TopMatType usedTopMatType;

        public string cooldownResearchName;

        public float aimTimeMultiplicator = 1.5f;
        public float aimTimeResearchMultiplicator = 1.1f;

        public float cooldownMultiplicator = 1.25f;
        public float cooldownResearchMultiplicator = 1.1f;


        public float priceShortMax;
        public float priceMediumMax;
        //public float priceLongMax;

        public string TopMatShortWeaponPath;
        public string TopMatMediumWeaponPath;
        public string TopMatLongWeaponPath;
        public string TopMatExtremeWeaponPath;

        public Material TopMatShortWeapon;
        public     Material TopMatMediumWeapon;
        public Material TopMatLongWeapon;
        public Material TopMatExtremeWeapon;

        private bool holdFire;

        private bool rotateTop = true;
        
        private List<IntVec3> cachedAdjCellsCardinal;

        private ThingDef cachedHopperDef;

        private bool MannedByColonist => mannableComp is { ManningPawn: not null } && mannableComp.ManningPawn.Faction == Faction.OfPlayer;

        private bool CanToggleHoldFire => Faction == Faction.OfPlayer || MannedByColonist;

        public override Verb AttackVerb => gun == null ? null : GunCompEq.verbTracker.PrimaryVerb;

        private bool CanSetForcedTarget => (gun != null) && MannedByColonist;

        public override LocalTargetInfo CurrentTarget => currentTargetInt;

        protected bool WarmingUp => burstWarmupTicksLeft > 0;

        public CompEquippable        GunCompEq  => gun?.TryGetComp<CompEquippable>();
        public CompApparelReloadable GunCompRel => gun?.TryGetComp<CompApparelReloadable>();

        public List<IntVec3> AdjCellsCardinalInBounds =>
            cachedAdjCellsCardinal ??= (from c in GenAdj.CellsAdjacentCardinal(this)
                                        where c.InBounds(Map)
                                        select c).ToList();

        public ThingDef HopperDef =>
            cachedHopperDef ??= DefDatabase<ThingDef>.GetNamedSilentFail("BetterTurrets_Hopper");

        private readonly string txtFloatMenuInstallWeapon   = "Install";
        private readonly string txtFloatMenuUninstallWeapon = "Uninstall";

        #endregion


        #region Initialization

        public Building_TurretWeaponBase() { }

        // transfer data from extended ThingDef to local variables 
        // (Not really neccessary, but easier for me)
        private void ReadXmlData()
        {
            ThingDef_TurretWeaponBase def2 = (ThingDef_TurretWeaponBase)def;
            usedTopMatType = def2.usedTopGraphic;

            // Load material only, when all three are not empty
            if (! (def2.TopMatShortPath.NullOrEmpty() || 
                   def2.TopMatMediumPath.NullOrEmpty() || 
                   def2.TopMatLongPath.NullOrEmpty())
                ) 
            {
                TopMatShortWeapon = MaterialPool.MatFrom(def2.TopMatShortPath);
                TopMatMediumWeapon = MaterialPool.MatFrom(def2.TopMatMediumPath);
                TopMatLongWeapon = MaterialPool.MatFrom(def2.TopMatLongPath);
                }
            if (!def2.TopMatExtremePath.NullOrEmpty())
                {
                TopMatExtremeWeapon = MaterialPool.MatFrom(def2.TopMatExtremePath);
                }

            priceShortMax = def2.priceShortMax;
            priceMediumMax = def2.priceMediumMax;

            disallowSlowingGuns = def2.disallowSlowingGuns;
            overrideRefuelPerBurst = def2.overrideRefuelPerBurst;

            cooldownResearchName = def2.cooldownResearchName;

            aimTimeMultiplicator = def2.aimTimeMultiplicator;
            aimTimeResearchMultiplicator = def2.aimTimeResearchMultiplicator;

            cooldownMultiplicator = def2.cooldownMultiplicator;
            cooldownResearchMultiplicator = def2.cooldownResearchMultiplicator;

            rotateTop = def2.rotateTop;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            LongEventHandler.ExecuteWhenFinished(SpawnSetup_Part2);
        }

        /// <summary>
        /// This is called seperately when the Mod-Thread is done.
        /// It is needed to be seperately from SpawnSetup, so that the graphics can be found
        /// </summary>
        private void SpawnSetup_Part2()
        {
            powerComp = GetComp<CompPowerTrader>();
            mannableComp = GetComp<CompMannable>();
            intCombComp = GetComp<CompInternalCombustion>();

            ReadXmlData();

            if (forceCreateGunAndTop)
                CreateGunAndTop(gun);
            else
            {
                gun = null;
                top = null;
            }

            // When not of colony, equip a revolver as main weapon.
            if (Faction != Faction.OfPlayer)
            {
                def.building.turretGunDef = ThingDef.Named("Gun_Revolver");
                gun                       = ThingMaker.MakeThing(def.building.turretGunDef);

                List<Verb> allVerbs = GunCompEq.AllVerbs;

                foreach (Verb item in allVerbs)
                    {
                    item.caster               = this;
                    item.castCompleteCallback = BurstComplete;
                    }
                
                top = new TurretTop_TurretWeaponBase(this);
            }
        }

        public override void ExposeData()
        {

            base.ExposeData();

            Scribe_Deep.Look(ref gun, "activeGun");

            Scribe_Values.Look(ref collectingGunAllowed,   "collectingGunAllowed");
            Scribe_Values.Look(ref burstCooldownTicksLeft, "burstCooldownTicksLeft");
            Scribe_Values.Look(ref burstWarmupTicksLeft,   "burstWarmupTicksLeft");
            Scribe_TargetInfo.Look(ref currentTargetInt, "currentTarget");
            Scribe_Values.Look(ref loaded, "loaded");
            Scribe_Values.Look(ref holdFire, "holdFire");

            if (gun != null)
                forceCreateGunAndTop = true;

        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // DeSpawn -> Deconstruct gun
            DeconstructGunAndReset();

            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // If deconstructed, spawn the used gun
            if (mode == DestroyMode.Deconstruct && gun != null)
                DeconstructGunAndReset();
            //else if (gun != null)
            //    gun.Destroy(DestroyMode.Vanish);

            gun = null;

            base.Destroy(mode);
        }

        public void DeconstructGunAndReset()
            {
            DeconstructGunAndReset(out Thing _);
            }
        public void DeconstructGunAndReset(out Thing result)
        {
            result = null;
            if (gun != null && MapHeld != null)
            {
                GenDrop.TryDropSpawn(gun, PositionHeld, MapHeld, ThingPlaceMode.Near, out result);
            }
            forceCreateGunAndTop = false;
            collectingGunAllowed = false;
            gun = null;
            top = null;

            ResetCurrentTarget();
            burstCooldownTicksLeft = 0;

            loaded = false;
            holdFire = false;
        }

        #endregion


        #region Ticker

        public override void Tick()
        {
            base.Tick();

            if (gun == null)
            {
                // Do work for when there is no gun
                if (!collectingGunAllowed)
                    return;

                counterSearchForGun += 1;
                if (counterSearchForGun < counterSearchForGunMax)
                    return;
                else
                    counterSearchForGun = 0;

                TryToInstallWeapon();
                }
            else
            {
                // Do work for when there is a gun
                //base.Tick();
                if (!CanSetForcedTarget && forcedTarget.IsValid)
                    ResetForcedTarget();

                if (!CanToggleHoldFire)
                    holdFire = false;

                if (forcedTarget.ThingDestroyed)
                    ResetForcedTarget();

                bool flag = (powerComp == null || powerComp.PowerOn) && (intCombComp == null || intCombComp.PowerOn) && (mannableComp == null || mannableComp.MannedNow);
                if (flag && Spawned)
                {
                    GunCompEq.verbTracker.VerbsTick();
                    if (!IsStunned && GunCompEq.PrimaryVerb.state != VerbState.Bursting)
                    {
                        if (WarmingUp)
                        {
                            burstWarmupTicksLeft--;
                            if (burstWarmupTicksLeft == 0)
                                BeginBurst();
                        }
                        else
                        {
                            if (burstCooldownTicksLeft > 0)
                                burstCooldownTicksLeft--;

                            if (burstCooldownTicksLeft <= 0)
                                TryStartShootSomething(true);
                        }
                        if (top != null && rotateTop)
                            top.TurretTopTick();
                    }
                }
                else
                {
                    ResetCurrentTarget();
                }

                if (GunCompRel != null && GunCompRel.NeedsReload(false))
                    {
                    Thing ammo = FindAmmoInAnyHopper(GunCompRel.AmmoDef);
                    if (ammo != null)
                        {
                        GunCompRel.ReloadFrom(ammo);
                        if (GunCompRel.Props.soundReload != null)
                            GunCompRel.Props.soundReload.PlayOneShot(new TargetInfo(Position, Map));
                        }
                    }
            }
        }


        public void TryToInstallWeapon(Thing gunToInstall = null)
        {
            List<Thing> gunsForInstallation;

            if (gunToInstall != null)
            {
                gunsForInstallation = new List<Thing>() { gunToInstall };
            }
            else
                {
                // Check, if a gun is at my Position
                IEnumerable<Thing> gunsFoundEnumeration = Map.listerThings.AllThings.Where(t => t.def.IsRangedWeapon && t.Position == Position);
                gunsForInstallation = new List<Thing>(gunsFoundEnumeration);
                }

            if (gunsForInstallation.Count > 0)
            {
                // We have a gun!!!
                Thing gunToWorkWith = gunsForInstallation[0];

                // check for usability of the weapon
                bool usableWeapon = gunToWorkWith.def.Verbs.All(t => !t.onlyManualCast);

                // not a usable weapon, respawn it nearby
                if (!usableWeapon)
                {
                    gunToWorkWith.DeSpawn();
                    GenPlace.TryPlaceThing(gunToWorkWith, Position, Map, ThingPlaceMode.Near);
                    return;
                }


                CreateGunAndTop(gunToWorkWith);

                // despawn the source gun
                if (gunToWorkWith.Spawned)
                    gunToWorkWith.DeSpawn();

                collectingGunAllowed = false;
            }
        }

        // Create the gun and turret top
        private void CreateGunAndTop(Thing thing)
        {
            gun = thing;

            List<Verb> allVerbs = GunCompEq.AllVerbs;

            foreach (Verb item in allVerbs)
                {
                item.caster               = this;
                item.castCompleteCallback = BurstComplete;
                }

            top = new TurretTop_TurretWeaponBase(this);
        }


        #endregion


        #region Button / Inspection / Floatmenu

        // From original
        public override IEnumerable<Gizmo> GetGizmos()
        {
            // Do base gizmos
            foreach (var c in base.GetGizmos())
                yield return c;

            if (gun != null)
            {
                if (CanSetForcedTarget && GunCompEq != null && GunCompEq.PrimaryVerb != null)
                {
                    //Command_VerbTarget_TurretWeaponBase attack = new Command_VerbTarget_TurretWeaponBase();
                    Command_VerbTarget attack = new Command_VerbTarget
                                                    {
                                                    defaultLabel = "CommandSetForceAttackTarget".Translate(),
                                                    defaultDesc  = "CommandSetForceAttackTargetDesc".Translate(),
                                                    icon         = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                                                    verb         = GunCompEq.PrimaryVerb,
                                                    hotKey       = KeyBindingDefOf.Misc4 //N
                                                    };
                    yield return attack;
                }

                if (forcedTarget.IsValid)
                {
                    Command_Action stop = new Command_Action
                                              {
                                              defaultLabel = "CommandStopForceAttack".Translate(),
                                              defaultDesc  = "CommandStopForceAttackDesc".Translate(),
                                              icon         = ContentFinder<Texture2D>.Get("UI/Commands/Halt"),
                                              action       = delegate
                                                                 {
                                                                 ResetForcedTarget();
                                                                 SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                                                                 }
                                              };
                    if (!forcedTarget.IsValid)
                    {
                        stop.Disable("CommandStopAttackFailNotForceAttacking".Translate());
                    }
                    stop.hotKey = KeyBindingDefOf.Misc5;
                    yield return stop;
                }

                if (CanToggleHoldFire)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "CommandHoldFire".Translate(),
                        defaultDesc = "CommandHoldFireDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire"),
                        hotKey = KeyBindingDefOf.Misc6,
                        toggleAction = delegate
                        {
                            holdFire = !holdFire;
                            if (holdFire)
                            {
                                ResetForcedTarget();
                            }
                        },
                        isActive = (() => holdFire)
                    };
                }
            }
        }

        // Mostly from original
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            string inspectString = base.GetInspectString();
            if (!inspectString.NullOrEmpty())
            {
                stringBuilder.Append(inspectString).AppendLine();
            }

            if (gun != null)
            {
                stringBuilder.Append(string.Concat("GunInstalled".Translate(), ": ", gun.Label)).AppendLine();
                if (GunCompEq.PrimaryVerb.verbProps.minRange > 0.1f)
                    stringBuilder.Append(string.Concat("MinimumRange".Translate(), ": ", GunCompEq.PrimaryVerb.verbProps.minRange.ToString("F0"))).AppendLine();
                if (burstCooldownTicksLeft > 0)
                    stringBuilder.Append(string.Concat("CanFireIn".Translate(), ": ", burstCooldownTicksLeft.TicksToSeconds().ToString("F0"))).AppendLine();

                stringBuilder.Append(string.Concat("FireTime".Translate(), ": ", getFireCooldownTicks().TicksToSeconds().ToString("##0.##"))).AppendLine();


                if (Prefs.DevMode)
                    stringBuilder.Append(string.Concat("Accuracy".Translate(), ": ", this.GetStatValue(StatDefOf.ShootingAccuracyTurret))).AppendLine();

                // Show info button for gun
                Widgets.InfoCardButton(360, 0, gun);
            }
            else
                stringBuilder.Append(string.Concat("GunInstalled".Translate(), ": ---")).AppendLine();


            CompChangeableProjectile compChangeableProjectile = gun.TryGetComp<CompChangeableProjectile>();
            if (compChangeableProjectile != null)
                {
                stringBuilder.AppendLine(compChangeableProjectile.Loaded
                                             ? "ShellLoaded".Translate(compChangeableProjectile.LoadedShell.LabelCap)
                                             : "ShellNotLoaded".Translate());
                }
            return stringBuilder.ToString().TrimEndNewlines();
        }


        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            if (gun != null)
                foreach (FloatMenuOption fmo in base.GetFloatMenuOptions(pawn))
                    yield return fmo;

            List<FloatMenuOption> list = new List<FloatMenuOption>();

            //is not a memory
            // Check if this is reservable by the pawn
            if (!pawn.CanReserve(this))
            {
                yield return new FloatMenuOption("CannotUseReserved".Translate(), null);
                yield break;
            }

            // Check if this is reachable by the pawn
            if (!pawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
                yield break;
            }

            // Do only, when no gun installed
            if (gun != null)
                {
                void Action()
                    {
                    var job = new Job(DefDatabase<JobDef>.GetNamed("UninstallWeaponFromTurretBase"), Position) { count = 1 };
                    pawn.jobs.TryTakeOrderedJob(job);
                    pawn.Reserve(this, job);
                    }       

                yield return new FloatMenuOption(txtFloatMenuUninstallWeapon.Translate() + " " + gun.Label, Action);
                yield break;    
                }

            // find available guns for possible use
            IEnumerable<Thing>     foundThings    = Map.listerThings.AllThings.Where(t => t.def.IsRangedWeapon && !t.IsForbidden(pawn.Faction));
            List<Thing>            foundThingList = foundThings.ToList();

            // only guns inside the home area are valid 
            List<IntVec3> HomeAreaCells = Map.areaManager.Home.ActiveCells.ToList();
            
            var availableGuns = new List<Thing>();
            foreach (Thing thing in foundThingList)
                {
                // don't use selfdestroying weapons
                if (thing.def.destroyOnDrop)
                    continue;

                //dont use single shot weapons ffs, why was the check so far forward anyway?
                bool unusableWeapon = Enumerable.Any(thing.def.Verbs, t => t.onlyManualCast);

                //disallow weapons that slow the player down on small turrets
                if (disallowSlowingGuns && thing.def.equippedStatOffsets != null)
                    {
                    if (Enumerable.Any(thing.def.equippedStatOffsets, t => t.stat == StatDefOf.MoveSpeed && t.value < 0))
                        {
                        unusableWeapon = true;
                        }
                    }

                if (unusableWeapon) continue;
                // only use weapons inside the home area
                //if (!HomeAreaCells.Contains(thing.Position) )
                //    continue;

                // New: use weapons inside a storage AND inside home
                if (!thing.IsInAnyStorage() && !HomeAreaCells.Contains(thing.Position))
                    continue;

                // can not reserve or reach?
                if (!pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Deadly))
                    continue;

                availableGuns.Add(thing);
                }

            availableGuns.Sort(CompareThings);
            
            int shownEntries = floatMenuShowEntries;
            int maxThings = floatMenuMaxEntries;
            int lastShownEntry = 0;

            // Make one entry per gun type found
            for (int i = shownEntries; i < availableGuns.Count; i++)
            {
                Thing haulThing = availableGuns[i];

                if (haulThing.IsForbidden(pawn.Faction) || !pawn.CanReserveAndReach(haulThing, PathEndMode.Touch, Danger.Deadly))
                    {
                    int i1 = i;
                    IEnumerable<Thing> allAvailableThings = Map.listerThings.AllThings.Where(t =>
                                                                                                 t.def.defName == availableGuns[i1].def.defName &&
                                                                                                 !t.IsForbidden(pawn.Faction) &&
                                                                                                 pawn.CanReserveAndReach(t, PathEndMode.Touch, Danger.Deadly));

                    //haulThing = Helper.FindNearestThing(allAvailableThings, Position);
                    haulThing = WeaponBaseHelper.FindNearestThing(allAvailableThings, Position);
                    }

                if (haulThing == null)
                    continue;

                void Action()
                    {
                    var job = new Job(DefDatabase<JobDef>.GetNamed("InstallWeaponOnTurretBase"), haulThing, Position) { count = 1, haulOpportunisticDuplicates = false, haulMode = HaulMode.ToCellNonStorage };
                    pawn.jobs.TryTakeOrderedJob(job);
                    pawn.Reserve(this, job);


                    // Allow this building to search for a gun
                    collectingGunAllowed = true;
                    }

                void HoverAction(Rect obj)
                    {
                    if (haulThing != null) FleckMaker.Static(haulThing.Position, Map, FleckDefOf.FeedbackGoto);
                    }

                list.Add(new FloatMenuOption(txtFloatMenuInstallWeapon.Translate() + " " + haulThing.Label, Action, MenuOptionPriority.Default, HoverAction));

                // save shown item position for next call
                lastShownEntry = i;

                // make max. x entries
                if (list.Count >= maxThings)
                    break;
            }

            // add entry for more items: '<' and '>'
            if (availableGuns.Count > maxThings)
            {
                Action switchPageNext = null;
                if (floatMenuShowEntries + maxThings < availableGuns.Count -1)
                {
                    switchPageNext = delegate
                    {
                        floatMenuShowEntries = lastShownEntry;
                    };
                }
                list.Add(new FloatMenuOption(">", switchPageNext, MenuOptionPriority.Low));

                Action switchPageLast = null;
                if (floatMenuShowEntries > 0)
                {
                    switchPageLast = delegate
                    {
                        floatMenuShowEntries = floatMenuShowEntries - floatMenuMaxEntries;
                        if (floatMenuShowEntries < 0)
                            floatMenuShowEntries = 0;
                    };
                }
                list.Add(new FloatMenuOption("<", switchPageLast, MenuOptionPriority.Low));

            }

            // No usable weapons found
            if (list.Count == 0)
                list.Add(new FloatMenuOption("NoWeaponFoundForTurretBase".Translate(), null, MenuOptionPriority.Low));

            foreach (FloatMenuOption fmo in list)
                yield return fmo;
        }

        private static int CompareThings(Thing x, Thing y)
            {
            return x.Label.CompareTo(y.Label);
            }

        #endregion


        #region Drawing

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            top?.DrawTurret();

            base.DrawAt(drawLoc, flip);
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            if (gun == null)
                return;

            float range = GunCompEq.PrimaryVerb.verbProps.range;
            if (range < 90f && rotateTop)
            {
                GenDraw.DrawRadiusRing(Position, range);
            }
            float minRange = GunCompEq.PrimaryVerb.verbProps.minRange;
            if (minRange < 90f && minRange > 0.1f && rotateTop)
            {
                GenDraw.DrawRadiusRing(Position, minRange);
            }
            if (WarmingUp)
            {
                int degreesWide = (int)(burstWarmupTicksLeft * 0.5f);
                GenDraw.DrawAimPie(this, CurrentTarget, degreesWide, def.size.x * 0.5f);
            }
            if (forcedTarget.IsValid && (!forcedTarget.HasThing || forcedTarget.Thing.Spawned))
                {
                Vector3 b = forcedTarget.HasThing ? forcedTarget.Thing.TrueCenter() : forcedTarget.Cell.ToVector3Shifted();
                Vector3 a = this.TrueCenter();
                b.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                a.y = b.y;
                GenDraw.DrawLineBetween(a, b, Building_TurretGun.ForcedTargetLineMat);
                }
        }

        #endregion


        #region Target functions

        protected virtual bool IsValidTarget(Thing t)
        {
            if (gun == null)
                return false;

            if (t is Pawn pawn)
            {
                if (GunCompEq.PrimaryVerb.ProjectileFliesOverhead())
                {
                    RoofDef roofDef = Map.roofGrid.RoofAt(t.Position);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        return false;
                    }
                }
                if (mannableComp == null)
                {
                    return !GenAI.MachinesLike(Faction, pawn);
                }
                if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
                {
                    return false;
                }
            }
            return true;
        }

        public override void OrderAttack(LocalTargetInfo targ)
        {
            if (gun == null)
                return;

            if (!targ.IsValid)
            {
                if (forcedTarget.IsValid)
                    ResetForcedTarget();
                return;
            }
            if ((targ.Cell - Position).LengthHorizontal < GunCompEq.PrimaryVerb.verbProps.minRange)
            {
                Messages.Message("MessageTargetBelowMinimumRange".Translate(), this, MessageTypeDefOf.RejectInput);
                return;
            }
            if ((targ.Cell - Position).LengthHorizontal > GunCompEq.PrimaryVerb.verbProps.range)
            {
                Messages.Message("MessageTargetBeyondMaximumRange".Translate(), this, MessageTypeDefOf.RejectInput);
                return;
            }
            if (forcedTarget != targ)
            {
                forcedTarget = targ;
                if (burstCooldownTicksLeft <= 0)
                {
                    TryStartShootSomething(false);
                }
            }
        }

        #endregion


        #region Targeting / Shooting functions

        protected LocalTargetInfo TryFindNewTarget()
        {
            if (gun == null)
                return LocalTargetInfo.Invalid;

            IAttackTargetSearcher attackTargetSearcher = TargSearcher();
            Faction faction = attackTargetSearcher.Thing.Faction;
            float range = AttackVerb.verbProps.range;
            if (Rand.Value < 0.5f && AttackVerb.ProjectileFliesOverhead() && faction.HostileTo(Faction.OfPlayer) && Map.listerBuildings.allBuildingsColonist.Where(delegate (Building x)
                                                                                                                                                                       {
                                                                                                                                                                       float num = AttackVerb.verbProps.EffectiveMinRange(x, this);
                                                                                                                                                                       float num2 = x.Position.DistanceToSquared(Position);
                                                                                                                                                                       return num2 > num * num && num2 < range * range;
                                                                                                                                                                       }).TryRandomElement(out Building t))
            {
                return t;
            }
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (!AttackVerb.ProjectileFliesOverhead())
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToAll;
                targetScanFlags |= TargetScanFlags.LOSBlockableByGas;
            }
            if (AttackVerb.IsIncendiary_Ranged())
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }
            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, targetScanFlags, IsValidTarget);
        }
        private IAttackTargetSearcher TargSearcher()
        {
            if (mannableComp != null && mannableComp.MannedNow)
            {
                return mannableComp.ManningPawn;
            }
            return this;
        }
        private void ResetCurrentTarget()
        {
            currentTargetInt = LocalTargetInfo.Invalid;
            burstWarmupTicksLeft = 0;
        }
        private void ResetForcedTarget()
        {
            forcedTarget = LocalTargetInfo.Invalid;
            burstWarmupTicksLeft = 0;
            if (burstCooldownTicksLeft <= 0)
            {
                TryStartShootSomething(false);
            }
        }

        protected void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            if (gun == null)
                return;

            if (!Spawned || (holdFire && CanToggleHoldFire) || (GunCompEq.PrimaryVerb.ProjectileFliesOverhead() && Map.roofGrid.Roofed(Position)) || 
                !AttackVerb.Available() || (this.TryGetComp<CompRefuelable>() != null && !this.TryGetComp<CompRefuelable>().HasFuel))
            {
                ResetCurrentTarget();
                return;
            }
            bool isValid = currentTargetInt.IsValid;
            currentTargetInt = forcedTarget.IsValid ? forcedTarget : TryFindNewTarget();
            if (!isValid && currentTargetInt.IsValid)
            {
                SoundDefOf.TurretAcquireTarget.PlayOneShot(new TargetInfo(Position, Map));
            }
            if (!currentTargetInt.IsValid)
            {
                ResetCurrentTarget();
                return;
            }
            float randomInRange = def.building.turretBurstWarmupTime.RandomInRange;
            if (randomInRange > 0f)
            {
                burstWarmupTicksLeft = randomInRange.SecondsToTicks();
                return;
            }
            if (canBeginBurstImmediately)
            {
                BeginBurst();
                return;
            }
            burstWarmupTicksLeft = 1;
        }

        protected virtual void BeginBurst()
        {
            GunCompEq.PrimaryVerb.TryStartCastOn(CurrentTarget);
            OnAttackedTarget(CurrentTarget);
        }

        protected void BurstComplete()
        {
            burstCooldownTicksLeft = getFireCooldownTicks();

            loaded = false;

            if (overrideRefuelPerBurst > 0)
                this.TryGetComp<CompRefuelable>()?.ConsumeFuel(overrideRefuelPerBurst);
            else
                if (GunCompEq.PrimaryVerb.verbProps.consumeFuelPerShot > 0f)
                    this.TryGetComp<CompRefuelable>()?.ConsumeFuel(GunCompEq.PrimaryVerb.verbProps.consumeFuelPerShot);
                else
                    if (GunCompEq.PrimaryVerb.verbProps.burstShotCount >= 1)
                        this.TryGetComp<CompRefuelable>()?.ConsumeFuel(1 * GunCompEq.PrimaryVerb.verbProps.burstShotCount);
                    else
                        this.TryGetComp<CompRefuelable>()?.ConsumeFuel(1);
        }

        #endregion

        #region PrincessCode

        protected int getFireCooldownTicks()
            {
            int aimTime;
            int cooldown;
            //base cooldown
            if (def.building.turretBurstCooldownTime >= 0f)
                {
                aimTime = def.building.turretBurstWarmupTime.Average.SecondsToTicks();
                cooldown = def.building.turretBurstCooldownTime.SecondsToTicks();
                }
            else
                {
                //Fun fact: it doesn't actually give a single fuck about the pawn.
                aimTime = GunCompEq.PrimaryVerb.verbProps.warmupTime.SecondsToTicks();
                cooldown = GunCompEq.PrimaryVerb.verbProps.AdjustedCooldown(GunCompEq.PrimaryVerb, null).SecondsToTicks();
                }

            //cooldown multipliers
            if (cooldownResearchName.NullOrEmpty() ||
                DefDatabase<ResearchProjectDef>.GetNamedSilentFail(cooldownResearchName) == null ||
                !ResearchProjectDef.Named(cooldownResearchName).IsFinished)
                {
                // Research for reduction not found or not finished
                if (aimTimeMultiplicator >= 0.1f && cooldownMultiplicator >= 0.1f)
                    { 
                    aimTime = (int)(aimTime * aimTimeMultiplicator);
                    cooldown = (int)(cooldown * cooldownMultiplicator);
                   }
                }
            else
            {
                // Research for reduction finished
                if (aimTimeResearchMultiplicator >= 0.1f && cooldownResearchMultiplicator >= 0.1f)
                    {
                    aimTime = (int)(aimTime * aimTimeResearchMultiplicator);
                    cooldown = (int)(cooldown * cooldownResearchMultiplicator);
                    }
                }

            return aimTime + cooldown;
        }

        public virtual Thing FindAmmoInAnyHopper(ThingDef ammo)
            {
            for (int i = 0; i < AdjCellsCardinalInBounds.Count; i++)
                {
                Thing thing = null;
                Thing thing2 = null;
                List<Thing> thingList = AdjCellsCardinalInBounds[i].GetThingList(Map);
                foreach (Thing thing3 in thingList)
                    {
                    if (thing3.def == ammo)
                        {
                        thing = thing3;
                        }
                    if (thing3.def == HopperDef)
                        {
                        thing2 = thing3;
                        }
                    }
                if (thing != null && thing2 != null)
                    {
                    return thing;
                    }
                }
            return null;
            }

        #endregion

        #region harmony
#pragma warning disable IDE0051 // Remove unused private members

        [StaticConstructorOnStartup]
        static class HarmonyLoader
            {
            static HarmonyLoader()
                {
                var harmony = new Harmony("princess.betterturrets");
                Log.Message("Better Turrets patching...");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                }
            }

        [HarmonyPatch(typeof(TooltipUtility), "ShotCalculationTipString")]
        static class BetterTurrets_ShotCalculationTipString_Patch
            {
            //[HarmonyDebug]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
                {
                var instructionsList = instructions.ToList();

                List<Label> searchFor = instructionsList.FindLast(x => x.opcode == OpCodes.Ldloc_0).labels;

                // Wee woo, off by one warning
                for (var i = 0; i < instructionsList.Count - 1; i++)
                    {
                    CodeInstruction    instruction = instructionsList[i];
                    if (
                        instructionsList[i + 1].Branches(out Label? label) &&
                        label != null                                      &&
                        searchFor.Contains((Label)label)                   &&
                        instruction.opcode == OpCodes.Ldloc_2
                        )

                        {
                        Label returnValue = il.DefineLabel();
                        LocalBuilder building_TurretGun = il.DeclareLocal(typeof(Building_TurretWeaponBase));

                        // Building_TurretWeaponBase building_TurretGun = singleSelectedThing as Building_TurretWeaponBase;
                        yield return new CodeInstruction(OpCodes.Ldloc_1) { labels = instruction.ExtractLabels() };
                        yield return new CodeInstruction(OpCodes.Isinst, typeof(Building_TurretWeaponBase));
                        yield return new CodeInstruction(OpCodes.Stloc_S, building_TurretGun);
                        
                        // if (building_TurretGun != null && building_TurretGun != target)
                        yield return new CodeInstruction(OpCodes.Ldloc_S, building_TurretGun);
                        yield return new CodeInstruction(OpCodes.Brfalse_S, returnValue);

                        yield return new CodeInstruction(OpCodes.Ldloc_S, building_TurretGun);
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Beq_S, returnValue);

                        // verb = building_TurretGun.AttackVerb;
                        yield return new CodeInstruction(OpCodes.Ldloc_S, building_TurretGun);
                        yield return new CodeInstruction(OpCodes.Callvirt, typeof(Building_TurretWeaponBase).GetProperty("AttackVerb")!.GetGetMethod());
                        yield return new CodeInstruction(OpCodes.Stloc_2);

                        instruction.labels = new List<Label> { returnValue };
                        }
                    yield return instruction;
                    }
                yield return instructionsList.Last();
                }
            }
/*
        [HarmonyPatch(typeof(CompReloadable), "ReloadFrom")]
        static class BetterTurrets_CompReloadable_ReloadFrom_Patch
            {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
                {
                Label? label;
                var instructionsList = instructions.ToList();
                for (var i = 0; i < instructionsList.Count; i++)
                    {
                    if (instructionsList[i].opcode == OpCodes.Ldarg_0
                        &&
                        instructionsList[i + 1].Calls(AccessTools.Method(typeof(CompReloadable), "get_Props"))
                        &&
                        instructionsList[i + 2].LoadsField(AccessTools.DeclaredField(typeof(CompProperties_Reloadable), "soundReload"))
                        &&
                        instructionsList[i + 3].Branches(out label))

                        {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, typeof(CompReloadable).GetProperty("Wearer").GetGetMethod());
                        yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                        }

                    yield return instructionsList[i];
                    }
                }
            }
*/

#pragma warning restore IDE0051
            #endregion

        }
    }
