using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using System.Runtime.Remoting.Messaging;
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
        protected CompPawnAccuracy accComp;
        public bool loaded;

        protected LocalTargetInfo currentTargetInt = LocalTargetInfo.Invalid;
     
        protected int burstWarmupTicksLeft;
        protected int burstCooldownTicksLeft;

        private bool collectingGunAllowed = false;
        private int counterSearchForGun;
        private const int counterSearchForGunMax = 60;
        private const int floatMenuMaxEntries = 10;
        private int floatMenuShowEntries = 0;

        private bool disallowSlowingGuns;

        private int overrideRefuelPerBurst;

        private bool forceCreateGunAndTop = false;

        public enum TopMatType
        {
            BuildingMat = 0,
            ShortMediumLongMat = 1,
            GunMat = 2,
            RangeBurstMat = 3
            }
        public TopMatType usedTopMatType;

        public string cooldownResearchName = null;

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

        public Material TopMatShortWeapon = null;
        public Material TopMatMediumWeapon = null;
        public Material TopMatLongWeapon = null;
        public Material TopMatExtremeWeapon = null;

        private bool holdFire;

        private bool rotateTop = true;
        
        private List<IntVec3> cachedAdjCellsCardinal;

        private ThingDef cachedHopperDef;

        private bool MannedByColonist
        {
            get
            {
                return this.mannableComp != null && this.mannableComp.ManningPawn != null && this.mannableComp.ManningPawn.Faction == Faction.OfPlayer;
            }
        }
        private bool CanToggleHoldFire
        {
            get
            {
                return base.Faction == Faction.OfPlayer || this.MannedByColonist;
            }
        }

        public override Verb AttackVerb
        {
            get
            {
                if (gun == null)
                    return null;

                return this.GunCompEq.verbTracker.PrimaryVerb;
            }
        }

        private bool CanSetForcedTarget
        {
            get
            {
                return (gun != null) && this.MannedByColonist;
            }
        }

        public override LocalTargetInfo CurrentTarget
        {
            get
            {
                return currentTargetInt;
            }
        }

        protected bool WarmingUp
        {
            get
            {
                return burstWarmupTicksLeft > 0;
            }
        }

        public CompEquippable GunCompEq
        {
            get
            {
                if (gun == null)
                    return null;

                return gun.TryGetComp<CompEquippable>();
            }
            }
        public CompReloadable GunCompRel
            {
            get
                {
                if (gun == null)
                    return null;

                return gun.TryGetComp<CompReloadable>();
                }
            }

        public List<IntVec3> AdjCellsCardinalInBounds
            {
            get
                {
                if (this.cachedAdjCellsCardinal == null)
                    {
                    this.cachedAdjCellsCardinal = (from c in GenAdj.CellsAdjacentCardinal(this)
                                                   where c.InBounds(base.Map)
                                                   select c).ToList();
                    }
                return this.cachedAdjCellsCardinal;
                }
            }

        public ThingDef HopperDef
            {
            get
                {
                if (this.cachedHopperDef == null)
                    {
                    this.cachedHopperDef = DefDatabase<ThingDef>.GetNamedSilentFail("BetterTurrets_Hopper");
                    }
                return this.cachedHopperDef;
                }
            }

        private string txtFloatMenuInstallWeapon = "Install";
        private string txtFloatMenuUninstallWeapon = "Uninstall";

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
            powerComp = base.GetComp<CompPowerTrader>();
            mannableComp = base.GetComp<CompMannable>();
            accComp = base.GetComp<CompPawnAccuracy>();

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
                this.gun = (Thing)ThingMaker.MakeThing(def.building.turretGunDef, null);

                List<Verb> allVerbs = this.GunCompEq.AllVerbs;

                for (int i = 0; i < allVerbs.Count; i++)
                {
                    Verb item = allVerbs[i];
                    item.caster = this;
                    item.castCompleteCallback = new Action(this.BurstComplete);
                }
                
                top = new TurretTop_TurretWeaponBase(this);
            }

            ReceiveCompSignal("AccuracyChanged");
        }

        public override void ExposeData()
        {

            base.ExposeData();

            Scribe_Deep.Look<Thing>(ref gun, "activeGun");

            Scribe_Values.Look<bool>(ref collectingGunAllowed, "collectingGunAllowed");
            Scribe_Values.Look<int>(ref burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<int>(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_TargetInfo.Look(ref this.currentTargetInt, "currentTarget");
            Scribe_Values.Look<bool>(ref this.loaded, "loaded", false, false);
            Scribe_Values.Look<bool>(ref this.holdFire, "holdFire", false, false);

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
            Thing resultingThing;

            DeconstructGunAndReset(out resultingThing);
            }
        public void DeconstructGunAndReset(out Thing result)
        {
            result = null;
            if (gun != null && PositionHeld != null && MapHeld != null)
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

                return;
            }
            else
            {
                // Do work for when there is a gun
                //base.Tick();
                if (!this.CanSetForcedTarget && this.forcedTarget.IsValid)
                    this.ResetForcedTarget();

                if (!this.CanToggleHoldFire)
                    this.holdFire = false;

                if (this.forcedTarget.ThingDestroyed)
                    this.ResetForcedTarget();

                bool flag = (this.powerComp == null || this.powerComp.PowerOn) && (this.mannableComp == null || this.mannableComp.MannedNow);
                if (flag && base.Spawned)
                {
                    this.GunCompEq.verbTracker.VerbsTick();
                    if (!this.stunner.Stunned && this.GunCompEq.PrimaryVerb.state != VerbState.Bursting)
                    {
                        if (this.WarmingUp)
                        {
                            this.burstWarmupTicksLeft--;
                            if (this.burstWarmupTicksLeft == 0)
                                this.BeginBurst();
                        }
                        else
                        {
                            if (this.burstCooldownTicksLeft > 0)
                                this.burstCooldownTicksLeft--;

                            if (this.burstCooldownTicksLeft <= 0)
                                this.TryStartShootSomething(true);
                        }
                        if (top != null && rotateTop)
                            this.top.TurretTopTick();
                    }
                }
                else
                {
                    this.ResetCurrentTarget();
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
            List<Thing> gunsForInstallation = null;

            if (gunToInstall != null)
            {
                gunsForInstallation = new List<Thing>() { gunToInstall };
            }
            else
            {
                // Check, if a gun is at my Position
                IEnumerable<Thing> gunsFoundEnumeration = Map.listerThings.AllThings.Where(t => t.def.IsRangedWeapon && t.Position == Position);
                if (gunsFoundEnumeration != null)
                    gunsForInstallation = new List<Thing>(gunsFoundEnumeration);
            }

            if (gunsForInstallation != null && gunsForInstallation.Count > 0)
            {
                // We have a gun!!!
                Thing gunToWorkWith = gunsForInstallation[0];

                // check for usability of the weapon
                bool usableWeapon = true;
                for (int v = 0; v < gunToWorkWith.def.Verbs.Count; v++)
                {
                    if (gunToWorkWith.def.Verbs[v].onlyManualCast)
                    {
                        usableWeapon = false;
                        break;
                    }
                }

                // not a usable weapon, respawn it nearby
                if (!usableWeapon)
                {
                    gunToWorkWith.DeSpawn();
                    GenPlace.TryPlaceThing(gunToWorkWith, Position, this.Map, ThingPlaceMode.Near);
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

            List<Verb> allVerbs = this.GunCompEq.AllVerbs;

            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb item = allVerbs[i];
                item.caster = this;
                item.castCompleteCallback = new Action(this.BurstComplete);
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
                if (this.CanSetForcedTarget && GunCompEq != null && GunCompEq.PrimaryVerb != null)
                {
                    //Command_VerbTarget_TurretWeaponBase attack = new Command_VerbTarget_TurretWeaponBase();
                    Command_VerbTarget attack = new Command_VerbTarget();
                    attack.defaultLabel = "CommandSetForceAttackTarget".Translate();
                    attack.defaultDesc = "CommandSetForceAttackTargetDesc".Translate();
                    attack.icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true);
                    attack.verb = GunCompEq.PrimaryVerb;
                    attack.hotKey = KeyBindingDefOf.Misc4; //N
                    yield return attack;
                }

                if (this.forcedTarget.IsValid)
                {
                    Command_Action stop = new Command_Action();
                    stop.defaultLabel = "CommandStopForceAttack".Translate();
                    stop.defaultDesc = "CommandStopForceAttackDesc".Translate();
                    stop.icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt", true);
                    stop.action = delegate
                    {
                        ResetForcedTarget();
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                    };
                    if (!this.forcedTarget.IsValid)
                    {
                        stop.Disable("CommandStopAttackFailNotForceAttacking".Translate());
                    }
                    stop.hotKey = KeyBindingDefOf.Misc5;
                    yield return stop;
                }

                if (this.CanToggleHoldFire)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "CommandHoldFire".Translate(),
                        defaultDesc = "CommandHoldFireDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire", true),
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


            CompChangeableProjectile compChangeableProjectile = this.gun.TryGetComp<CompChangeableProjectile>();
            if (compChangeableProjectile != null)
            {
                if (compChangeableProjectile.Loaded)
                {
                    stringBuilder.AppendLine("ShellLoaded".Translate( compChangeableProjectile.LoadedShell.LabelCap ));
                }
                else
                {
                    stringBuilder.AppendLine("ShellNotLoaded".Translate());
                }
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
            if (!pawn.CanReserve(this, 1))
            {
                yield return new FloatMenuOption("CannotUseReserved".Translate(), null, MenuOptionPriority.Default, null, null);
                yield break;
            }

            // Check if this is reachable by the pawn
            if (!pawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotUseNoPath".Translate(), null, MenuOptionPriority.Default, null, null);
                yield break;
            }

            // Do only, when no gun installed
            if (gun != null)
                {
                Action action = delegate
                    {
                        Job job = new Job(DefDatabase<JobDef>.GetNamed("UninstallWeaponFromTurretBase"), Position)
                            {
                            count = 1
                            };
                        pawn.jobs.TryTakeOrderedJob(job);
                        pawn.Reserve(this, job, 1);
                        };

                yield return new FloatMenuOption(txtFloatMenuUninstallWeapon.Translate() + " " + this.gun.Label, action, MenuOptionPriority.Default);
                yield break;
                }

            // find available guns for possible use
            IEnumerable<Thing> foundThings = Map.listerThings.AllThings.Where(t => t.def.IsRangedWeapon && !t.IsForbidden(pawn.Faction));
            List<Thing> foundThingList;
            if (foundThings != null)
                foundThingList = foundThings.ToList();
            else
                foundThingList = null;

            // only guns inside the home area are valid 
            List<IntVec3> HomeAreaCells = null;
            if (Map.areaManager.Home.ActiveCells.FirstOrDefault<IntVec3>() != null)
                HomeAreaCells = Map.areaManager.Home.ActiveCells.ToList();
            
            List<Thing> availableGuns = new List<Thing>();
            if (foundThingList != null && HomeAreaCells != null)
            {
                for (int w = 0; w < foundThingList.Count; w++)
                {
                    Thing thing = foundThingList[w];

                    // don't use selfdestroying weapons
                    if (thing.def.destroyOnDrop)
                        continue;

                    //dont use single shot weapons ffs, why was the check so far forward anyway?
                    bool unusableWeapon = false;
                    for (int v = 0; v < thing.def.Verbs.Count; v++)
                    {
                        if (thing.def.Verbs[v].onlyManualCast)
                        {
                            unusableWeapon = true;
                            break;
                        }
                    }

                    //disallow weapons that slow the player down on small turrets
                    if (disallowSlowingGuns && thing.def.equippedStatOffsets != null)
                        {
                        for (int v = 0; v < thing.def.equippedStatOffsets.Count; v++)
                            {
                            if (thing.def.equippedStatOffsets[v].stat == StatDefOf.MoveSpeed && thing.def.equippedStatOffsets[v].value < 0)
                                {
                                unusableWeapon = true;
                                break;
                                }
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
                    if (!pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.Deadly, 1))
                        continue;

                    availableGuns.Add(thing);
                }
            }
            
            availableGuns.Sort(CompareThings);
            
            int shownEntries = floatMenuShowEntries;
            int maxThings = floatMenuMaxEntries;
            int lastShownEntry = 0;

            // Make one entry per gun type found
            int availableGunsCount = availableGuns.Count;
            for (int i = shownEntries; i < availableGuns.Count; i++)
            {
                Thing haulThing = availableGuns[i];

                if (haulThing.IsForbidden(pawn.Faction) || !pawn.CanReserveAndReach(haulThing, PathEndMode.Touch, Danger.Deadly, 1))
                {
                    IEnumerable<Thing> allAvailableThings = Map.listerThings.AllThings.Where(t =>
                                                                                                   t.def.defName == availableGuns[i].def.defName &&
                                                                                                   !t.IsForbidden(pawn.Faction) &&
                                                                                                   pawn.CanReserveAndReach(t, PathEndMode.Touch, Danger.Deadly, 1));

                    //haulThing = Helper.FindNearestThing(allAvailableThings, Position);
                    haulThing = WeaponBaseHelper.FindNearestThing(allAvailableThings, Position);
                }

                if (haulThing == null)
                    continue;

                Action action = delegate
                {
                    Job job = new Job(DefDatabase<JobDef>.GetNamed("InstallWeaponOnTurretBase"), haulThing, Position)
                    {
                        count = 1,
                        haulOpportunisticDuplicates = false,
                        haulMode = HaulMode.ToCellNonStorage
                    };
                    pawn.jobs.TryTakeOrderedJob(job);
                    pawn.Reserve(this, job, 1);


                    // Allow this building to search for a gun
                    this.collectingGunAllowed = true;
                };

                Action<Rect> hoverAction = delegate
                {
                    if (haulThing != null)
                        FleckMaker.Static(haulThing.Position, Map, FleckDefOf.FeedbackGoto);
                };

                list.Add(new FloatMenuOption(txtFloatMenuInstallWeapon.Translate() + " " + haulThing.Label, action, MenuOptionPriority.Default, hoverAction));

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

        public override void Draw()
        {
            if (top != null)
                top.DrawTurret();

            base.Draw();
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
                int degreesWide = (int)((float)burstWarmupTicksLeft * 0.5f);
                GenDraw.DrawAimPie(this, CurrentTarget, degreesWide, (float)def.size.x * 0.5f);
            }
            if (forcedTarget.IsValid && (!forcedTarget.HasThing || forcedTarget.Thing.Spawned))
            {
                Vector3 b;
                if (forcedTarget.HasThing)
                {
                    b = forcedTarget.Thing.TrueCenter();
                }
                else
                {
                    b = forcedTarget.Cell.ToVector3Shifted();
                }
                Vector3 a = this.TrueCenter();
                b.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
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

            Pawn pawn = t as Pawn;
            if (pawn != null)
            {
                if (this.GunCompEq.PrimaryVerb.ProjectileFliesOverhead())
                {
                    RoofDef roofDef = base.Map.roofGrid.RoofAt(t.Position);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        return false;
                    }
                }
                if (this.mannableComp == null)
                {
                    return !GenAI.MachinesLike(base.Faction, pawn);
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
                if (this.forcedTarget.IsValid)
                    this.ResetForcedTarget();
                return;
            }
            if ((targ.Cell - base.Position).LengthHorizontal < this.GunCompEq.PrimaryVerb.verbProps.minRange)
            {
                Messages.Message("MessageTargetBelowMinimumRange".Translate(), this, MessageTypeDefOf.RejectInput);
                return;
            }
            if ((targ.Cell - base.Position).LengthHorizontal > this.GunCompEq.PrimaryVerb.verbProps.range)
            {
                Messages.Message("MessageTargetBeyondMaximumRange".Translate(), this, MessageTypeDefOf.RejectInput);
                return;
            }
            if (this.forcedTarget != targ)
            {
                this.forcedTarget = targ;
                if (this.burstCooldownTicksLeft <= 0)
                {
                    this.TryStartShootSomething(false);
                }
            }
        }

        #endregion


        #region Targeting / Shooting functions

        protected LocalTargetInfo TryFindNewTarget()
        {
            if (gun == null)
                return LocalTargetInfo.Invalid;

            IAttackTargetSearcher attackTargetSearcher = this.TargSearcher();
            Faction faction = attackTargetSearcher.Thing.Faction;
            float range = this.AttackVerb.verbProps.range;
            Building t;
            if (Rand.Value < 0.5f && this.AttackVerb.ProjectileFliesOverhead() && faction.HostileTo(Faction.OfPlayer) && base.Map.listerBuildings.allBuildingsColonist.Where(delegate (Building x)
            {
                float num = this.AttackVerb.verbProps.EffectiveMinRange(x, this);
                float num2 = (float)x.Position.DistanceToSquared(this.Position);
                return num2 > num * num && num2 < range * range;
            }).TryRandomElement(out t))
            {
                return t;
            }
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (!this.AttackVerb.ProjectileFliesOverhead())
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToAll;
                targetScanFlags |= TargetScanFlags.LOSBlockableByGas;
            }
            if (this.AttackVerb.IsIncendiary())
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }
            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, targetScanFlags, IsValidTarget);
        }
        private IAttackTargetSearcher TargSearcher()
        {
            if (this.mannableComp != null && this.mannableComp.MannedNow)
            {
                return this.mannableComp.ManningPawn;
            }
            return this;
        }
        private void ResetCurrentTarget()
        {
            this.currentTargetInt = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
        }
        private void ResetForcedTarget()
        {
            this.forcedTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
            if (this.burstCooldownTicksLeft <= 0)
            {
                this.TryStartShootSomething(false);
            }
        }

        protected void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            if (gun == null)
                return;

            if (!base.Spawned || (this.holdFire && this.CanToggleHoldFire) || (this.GunCompEq.PrimaryVerb.ProjectileFliesOverhead() && base.Map.roofGrid.Roofed(base.Position)) || 
                !this.AttackVerb.Available() || (this.TryGetComp<CompRefuelable>() != null && !this.TryGetComp<CompRefuelable>().HasFuel))
            {
                ResetCurrentTarget();
                return;
            }
            bool isValid = this.currentTargetInt.IsValid;
            if (this.forcedTarget.IsValid)
            {
                this.currentTargetInt = this.forcedTarget;
            }
            else
            {
                this.currentTargetInt = this.TryFindNewTarget();
            }
            if (!isValid && this.currentTargetInt.IsValid)
            {
                SoundDefOf.TurretAcquireTarget.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
            }
            if (!this.currentTargetInt.IsValid)
            {
                this.ResetCurrentTarget();
                return;
            }
            if (this.def.building.turretBurstWarmupTime > 0f)
            {
                this.burstWarmupTicksLeft = this.def.building.turretBurstWarmupTime.SecondsToTicks();
                return;
            }
            if (canBeginBurstImmediately)
            {
                this.BeginBurst();
                return;
            }
            this.burstWarmupTicksLeft = 1;
        }

        protected virtual void BeginBurst()
        {
            GunCompEq.PrimaryVerb.TryStartCastOn(CurrentTarget, false, true);
            base.OnAttackedTarget(this.CurrentTarget);
        }

        protected void BurstComplete()
        {
            this.burstCooldownTicksLeft = getFireCooldownTicks();

            this.loaded = false;

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
            if (this.def.building.turretBurstCooldownTime >= 0f)
                {
                aimTime = this.def.building.turretBurstWarmupTime.SecondsToTicks();
                cooldown = this.def.building.turretBurstCooldownTime.SecondsToTicks();
                }
            else
                {
                //Fun fact: it doesn't actually give a single fuck about the pawn.
                aimTime = this.GunCompEq.PrimaryVerb.verbProps.warmupTime.SecondsToTicks();
                cooldown = this.GunCompEq.PrimaryVerb.verbProps.AdjustedCooldown(this.GunCompEq.PrimaryVerb, null).SecondsToTicks();
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


        protected override void ReceiveCompSignal(string signal)
            {
            base.ReceiveCompSignal(signal);
            if (signal == "AccuracyChanged" && accComp != null)
                {
                StatUtility.SetStatValueInList(ref this.def.statBases, StatDefOf.ShootingAccuracyTurret, accComp.accFloat);
                }
            }

        public virtual Thing FindAmmoInAnyHopper(ThingDef ammo)
            {
            for (int i = 0; i < this.AdjCellsCardinalInBounds.Count; i++)
                {
                Thing thing = null;
                Thing thing2 = null;
                List<Thing> thingList = this.AdjCellsCardinalInBounds[i].GetThingList(base.Map);
                for (int j = 0; j < thingList.Count; j++)
                    {
                    Thing thing3 = thingList[j];
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
                Label? label;

                var instructionsList = instructions.ToList();

                List<Label> searchFor = instructionsList.FindLast(x => x.opcode == OpCodes.Ldloc_0).labels;

                // Wee woo, off by one warning
                for (var i = 0; i < instructionsList.Count - 1; i++)
                    {
                    var instruction = instructionsList[i];
                    if (
                        instructionsList[i + 1].Branches(out label) &&
                        label != null &&
                        searchFor.Contains((Label)label) &&
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
                        yield return new CodeInstruction(OpCodes.Callvirt, typeof(Building_TurretWeaponBase).GetProperty("AttackVerb").GetGetMethod());
                        yield return new CodeInstruction(OpCodes.Stloc_2);

                        instruction.labels = new List<Label> { returnValue };
                        }
                    yield return instruction;
                    }
                yield return instructionsList.Last();
                }
            }

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


#pragma warning restore IDE0051
            #endregion

        }
    }
