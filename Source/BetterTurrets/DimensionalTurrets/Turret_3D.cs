using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace DimensionalTurrets
    {
    [HarmonyPatch(typeof(TurretTop), nameof(TurretTop.DrawTurret))]
    public class Turret_3D
        {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TurretTop_DrawTurret_transpiler(IEnumerable<CodeInstruction> instructions)
            {
            return instructions.MethodReplacer(AccessTools.Method(typeof(Graphics), nameof(Graphics.DrawMesh), new Type[] { typeof(Mesh), typeof(Matrix4x4), typeof(Material), typeof(int) }), AccessTools.Method(typeof(Dummy), nameof(Dummy.DrawMesh)));
            }


        protected static AccessTools.FieldRef<TurretTop, Building_Turret> parentTurret = AccessTools.FieldRefAccess<Building_Turret>(typeof(TurretTop), "parentTurret");
        private static Dictionary<TurretTop, Graphic_3D> graphics = new();
        [HarmonyPostfix]
        public static void TurretTop_DrawTurret_postfix(Vector3 recoilDrawOffset, float recoilAngleOffset, TurretTop __instance)
            {
            var parent = parentTurret(__instance);
            Vector3 v = new Vector3(parent.def.building.turretTopOffset.x, 0f, parent.def.building.turretTopOffset.y).RotatedBy(__instance.CurRotation);
            v = v.RotatedBy(recoilAngleOffset) + recoilDrawOffset;
            v += recoilDrawOffset;

            var trans = parent.DrawPos + Altitudes.AltIncVect + v;
            var rot = __instance.CurRotation + (float)TurretTop.ArtworkRotation;

            var ext = parent.def.GetModExtension<DefExtension_Turret_3D>();
            if (ext != null)
                {
                Graphic_3D graphic;
                if (!graphics.TryGetValue(__instance, out graphic))
                    {
                    graphic = ext.graphic.GraphicColoredFor(parent) as Graphic_3D;
                    graphics.Add(__instance, graphic);
                    }
                graphic?.DrawScaled(trans, Rot4.North, parent, rot, parent.def.building.turretTopDrawSize);
                }
            else
                {
                Matrix4x4 matrix = default;
                matrix.SetTRS(trans, (rot).ToQuat(), new Vector3(parent.def.building.turretTopDrawSize, 1f, parent.def.building.turretTopDrawSize));

                Graphics.DrawMesh(MeshPool.plane10, matrix, parent.def.building.turretTopMat, 0);
                }

            }
        }
    public class DefExtension_Turret_3D : DefModExtension
        {
        public GraphicData graphic;
        public DefExtension_Turret_3D() { }
        }

    public static class Dummy
        {
        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer) { }
        }
    }
