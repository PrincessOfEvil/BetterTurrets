using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using HarmonyLib;
using System.IO;
using System.Xml;
using RimWorld;

namespace DimensionalTurrets
    {
    public static class VectorExtensions
        {

        // If only our space was 6-dimensional.
        public static Vector3 ToVVV(this float[] array)
            {
            return new Vector3(array[0], array[1], array[2]);
            }
        public static Vector2 ToVV(this float[] array)
            {
            return new Vector2(array[0], array[1]);
            }

        public static Vector3 ConvertWithY(this Vector2 vec)
            {
            return new Vector3(vec.x, 1f, vec.y);
            }

        public static float MaxCoord(this Vector3 vec)
            {
            return Mathf.Max(vec.x, vec.y, vec.z);
            }
        }

    public class Graphic_3D : Graphic_Single
        {
        public ModelDef mDef;
        public Mesh model;
        public Quaternion rotation;
        public Quaternion postRotation;
        public float scale = 1f;
        protected float scaleInt = 1f;

        protected Material outlineMat;

        public override void Init(GraphicRequest req)
            {
            base.Init(req);

            mDef = (req.shaderParameters[0] as ShaderParameter_3D).modelDef;
            rotation = Quaternion.Euler((req.shaderParameters[0] as ShaderParameter_3D).rotation);
            postRotation = Quaternion.Euler((req.shaderParameters[0] as ShaderParameter_3D).postRotation);

            model = new Mesh();
            model.name = "StandardModel";

            model.SetVertices(mDef.vertices);
            if (!mDef.uvs.NullOrEmpty()) model.SetUVs(0, mDef.uvs);
            model.SetTriangles(mDef.faces, 0);
            if (!mDef.normals.NullOrEmpty()) model.SetNormals(mDef.normals);

            model.RecalculateNormals();
            model.RecalculateBounds();

            if (mDef.center)
                {
                var points = mDef.vertices.Select(v => v - model.bounds.center).ToArray();
                model.SetVertices(points);

                model.RecalculateNormals();
                model.RecalculateBounds();
                }


            if (mDef.rescale) scale = 1f / model.bounds.size.MaxCoord();

            if (mDef.outline > 1.005f)
                {
                outlineMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.clear);
                outlineMat.shader = ShaderTypeDefOf.EdgeDetect.Shader;
                outlineMat.SetColor(ShaderPropertyIDs.Color, Color.black);
                outlineMat.SetColor(ShaderPropertyIDs.ColorTwo, Color.clear);
                }

            // GL.wireframe = true;
            }

        public override Mesh MeshAt(Rot4 rot)
            {
            /*
            Log.ErrorOnce(model.ToString(), model.GetHashCode());
            Log.ErrorOnce(model.vertices.ToStringSafeEnumerable(), model.GetHashCode()+1);
            Log.ErrorOnce(model.uv.ToStringSafeEnumerable(), model.GetHashCode()+2);
            Log.ErrorOnce(model.triangles.ToStringSafeEnumerable(), model.GetHashCode()+3);
            Log.ErrorOnce(model.normals.ToStringSafeEnumerable(), model.GetHashCode()+4);
            */
            return model;
            }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
            {
            return GraphicDatabase.Get(typeof(Graphic_3D), path, newShader, base.drawSize, newColor, newColorTwo, data, data.shaderParameters);
            }

        public override void Print(SectionLayer layer, Thing thing, float extraRotation)
            {
            Log.ErrorOnce("Printing 3D model for " + thing.Label + ", use <drawerType>RealtimeOnly</drawerType>", GetHashCode());
            base.Print(layer, thing, extraRotation);
            }

        public void DrawScaled(Vector3 loc, Rot4 rot, Thing thing, float extraRotation = 0f, float scal = 1f)
            {
            scaleInt = scal;
            this.Draw(loc, rot, thing, extraRotation);
            }

        protected override void DrawMeshInt(Mesh mesh, Vector3 loc, Quaternion quat, Material mat)
            {
            DrawMeshInt(mesh, loc, quat, mat, scale);
            }

        protected virtual void DrawMeshInt(Mesh mesh, Vector3 loc, Quaternion quat, Material mat, float scal)
            {
            if (mDef.outline > 1.005f)
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(loc + Vector3.down, quat * postRotation, drawSize.ConvertWithY() * scal * scaleInt * mDef.outline), outlineMat, 0);

            Graphics.DrawMesh(mesh, Matrix4x4.TRS(loc, quat * postRotation, drawSize.ConvertWithY() * scal * scaleInt), mat, 0);


            scaleInt = 1f;
            }

        [HarmonyPatch(typeof(Graphic))]
        public class Patch // : AlwaysPatch
            {
            /*
                [HarmonyPatch(nameof(Graphic.DrawOffset))]
                [HarmonyPostfix]
                public static Vector3 override_DrawOffset(Vector3 ret, Graphic __instance)
                    {
                    if (__instance is Graphic_3D _3D && _3D.mDef.center)
                        {
                        ret += _3D.rotation * (-_3D.model.bounds.center * _3D.scale);
                        }
                    return ret;
                    }
            */
            [HarmonyPatch("QuatFromRot")]
            [HarmonyPostfix]
            public static Quaternion override_QuatFromRot(Quaternion ret, Graphic __instance)
                {
                if (__instance is Graphic_3D _3D)
                    {
                    ret *= _3D.rotation;
                    }
                return ret;
                }
            }
        }

    public class ShaderParameter_3D : ShaderParameter
        {
        public ModelDef modelDef;

        // Scale and offset are within GraphicData.
        public Vector3 rotation;
        public Vector3 postRotation;


        //[HarmonyPatch(typeof(ShaderParameter))]
        public class Patch : AlwaysPatch
            {/*
            [HarmonyPatch(nameof(ShaderParameter.LoadDataFromXmlCustom))]
            [HarmonyPrefix]*/
            public static bool override_LoadDataFromXmlCustom(XmlNode xmlRoot, ref ShaderParameter __instance)
                {
                if (__instance is ShaderParameter_3D _3D)
                    {
                    AccessTools.Field(typeof(ShaderParameter_3D), "name")?.SetValue(_3D, xmlRoot.Name);
                    AccessTools.Field(typeof(ShaderParameter_3D), "type")?.SetValue(_3D, 2); // Matrix shader paramemter: unused by Rimworld itself

                    LongEventHandler.ExecuteWhenFinished(delegate
                        {
                            _3D.modelDef = DefDatabase<ModelDef>.GetNamed(xmlRoot.ChildNodes.OfType<XmlNode>().First(n => n.Name == "modelDef").InnerText);
                            _3D.rotation = ParseHelper.FromString<Vector3>(xmlRoot.ChildNodes.OfType<XmlNode>().First(n => n.Name == "rotation")?.InnerText ?? "0,0,0");
                            _3D.postRotation = ParseHelper.FromString<Vector3>(xmlRoot.ChildNodes.OfType<XmlNode>().First(n => n.Name == "postRotation")?.InnerText ?? "0,0,0");
                            });

                    return false;
                    }
                return true;
                }
            }
        }

    public class ModelDef : Def 
        {
        public const string modelPath = "models";

        public List<Vector3> vertices = new();
        public List<int> faces = new();
        public List<Vector2> uvs = new();
        public List<Vector3> normals = new();

        public bool rescale = false;
        public bool center = false;
        public float outline = 1.0f;

        public override void PostLoad()
            {
            loadModel();
            }

        protected virtual void loadModel()
            {
            // Null model: defined in xml
            }
        }

    // Primitive obj model loader
    public class ObjModelDef : ModelDef
        {
        public string objModelPath;
        public string objModel;

        protected override void loadModel()
            {
            if (objModel == null)
                {
                List<ModContentPack> runningModsListForReading = LoadedModManager.RunningModsListForReading;
                for (int num = runningModsListForReading.Count - 1; num >= 0; num--)
                    {
                    string path = Path.Combine(runningModsListForReading[num].RootDir, modelPath, objModelPath);
                    if (new FileInfo(path).Exists)
                        {
                        objModel = GenFile.TextFromRawFile(path);
                        break;
                        }
                    }
                }

            string[] lines = objModel.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // obj defines these not near vertices, but in triangle definition.
            List<Vector2> storeduvs = new();
            List<Vector3> stnormals = new();

            Dictionary<int, Vector2> uvdict = new();
            Dictionary<int, Vector3> nmdict = new();

            foreach (string line in lines)
                {
                switch (line.Substring(0, 2))
                    {
                    case "v ":
                        vertices.Add(line.Split(' ').Skip(1).Select(float.Parse).ToArray().ToVVV());
                        break;
                    case "vt":
                        storeduvs.Add(line.Split(' ').Skip(1).Select(float.Parse).ToArray().ToVV());
                        break;
                    case "vn":
                        stnormals.Add(line.Split(' ').Skip(1).Select(float.Parse).ToArray().ToVVV());
                        break;

                    case "f ":
                        // This is a big one.
                        // By the way, loader doesn't actually allow switching between different `f` definition types mid-model, but most exporters are already as verbose as they can be.
                        {
                        // f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3
                        var data = line.Split(' ').Skip(1).SelectMany(s => s.Split('/')).Select(int.Parse).ToList();

                        // Log.Message(data.ToStringSafeEnumerable());

                        //No non-triangles allowed.
                        if (data.Count > 9 || data.Count % 3 != 0) break;

                        if (data.Count == 3)
                            {
                            data.InsertRange(3, new int[] { 0 , 0 });
                            data.InsertRange(2, new int[] { 0 , 0 });
                            data.InsertRange(1, new int[] { 0 , 0 });
                            }
                        else if (data.Count == 6)
                            {
                            if (line.Split(' ').Skip(1).First().Contains("//"))
                                {
                                data.InsertRange(6, new int[] { 0 });
                                data.InsertRange(4, new int[] { 0 });
                                data.InsertRange(2, new int[] { 0 });
                                }
                            else
                                {
                                data.InsertRange(7, new int[] { 0 });
                                data.InsertRange(5, new int[] { 0 });
                                data.InsertRange(3, new int[] { 0 });
                                }
                            }

                        int point0 = data[0] - 1;
                        int point1 = data[3] - 1;
                        int point2 = data[6] - 1;
                        if (data[1] != 0)
                            {
                            //Log.Message("" + uvdict.Count + ":"+ point0);

                            if (uvdict.Count > point0 && uvdict.TryGetValue(point0, out var val0) && val0 != storeduvs[data[1] - 1])
                                {
                                vertices.Add(vertices[point0]);
                                point0 = vertices.LastIndexOf(vertices[point0]);
                                }
                            if (uvdict.Count > point1 && uvdict.TryGetValue(point1, out var val1) && val1 != storeduvs[data[4] - 1])
                                {
                                vertices.Add(vertices[point1]);
                                point1 = vertices.LastIndexOf(vertices[point1]);
                                }
                            if (uvdict.Count > point2 && uvdict.TryGetValue(point2, out var val2) && val2 != storeduvs[data[7] - 1])
                                {
                                vertices.Add(vertices[point2]);
                                point2 = vertices.LastIndexOf(vertices[point2]);
                                }

                            uvdict[point0] = storeduvs[data[1] - 1];
                            uvdict[point1] = storeduvs[data[4] - 1];
                            uvdict[point2] = storeduvs[data[7] - 1];

                            if (data[2] != 0)
                                {
                                nmdict[point0] = stnormals[data[2] - 1];
                                nmdict[point1] = stnormals[data[5] - 1];
                                nmdict[point2] = stnormals[data[8] - 1];
                                }
                            }

                        faces.AddRange(new int[] { point0, point1, point2 });

                        /*
                        faces.AddRange(new int[] { data[0] - 1, data[3] - 1, data[6] - 1 });
                        if (data[1] != 0) 
                            {
                            uvs[data[0] - 1] = storeduvs[data[1] - 1];
                            uvs[data[3] - 1] = storeduvs[data[4] - 1];
                            uvs[data[6] - 1] = storeduvs[data[7] - 1];
                            }

                        if (data[2] != 0)
                            {
                            normals.Add(stnormals[data[2] - 1]);
                            normals.Add(stnormals[data[5] - 1]);
                            normals.Add(stnormals[data[8] - 1]);
                            }
                        */

                        break;
                        }
                    default: break;
                    }
                }

            for (int i = 0; i < vertices.Count; i++)
                {
                if (uvdict.TryGetValue(i, out Vector2 value))
                    {
                    uvs.Add(value);
                    normals.Add(nmdict[i]);
                    }
                else
                    {
                    uvs.Add(Vector2.zero);
                    normals.Add(Vector3.zero);
                    }
                }
            }
        }
    }
