using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace DimensionalTurrets
    {
    public class EarlyLoader : Mod
        {
        public EarlyLoader(ModContentPack content) : base(content)
            {
            var harmony = new Harmony("princess.DimensionalTurrets");
            Log.Message("Dimensional Turrets has got it going on!");

            //TODO: re-enable when i split it off
            harmony.Patch(AccessTools.Method(typeof(ShaderParameter), nameof(ShaderParameter.LoadDataFromXmlCustom)), prefix: new HarmonyMethod(typeof(ShaderParameter_3D.Patch), nameof(ShaderParameter_3D.Patch.override_LoadDataFromXmlCustom)));
            
            
            new MultiClassProcessor(harmony, typeof(AlwaysPatch)).Patch();
            }
        }

    public struct MultiClassProcessor
        {
        private readonly Harmony harmony;
        private readonly Type type;
        public MultiClassProcessor(Harmony harmony, Type type)
            {
            this.harmony = harmony;
            this.type = type;
            }

        public void Patch()
            {
            type.AllSubclassesNonAbstract().ForEach(CreateClassProcessor);
            }

        private void CreateClassProcessor(Type type)
            {
            //Log.Message(type.AssemblyQualifiedName);
            harmony.CreateClassProcessor(type).Patch();
            }
        }

    public abstract class AlwaysPatch { }
    }
