using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleDestructionAssetBuilder // 원본을 보존하고 세 공용 파괴 Prefab을 제작한다
    {
        private const string Root = "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant";
        public const string PrefabFolder = Root + "/Resources/CastleRaidDestruction";
        private const string ArtFolder = Root + "/Art/Destruction";

        [MenuItem("JC Tool/군단의 역습 육각/파괴 잔해 3종 생성")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(ArtFolder);
            var stone = Material("Stone", new Color(0.64f, 0.66f, 0.65f));
            var interior = Material("StoneInterior", new Color(0.46f, 0.48f, 0.47f));
            var roof = Material("RoofBlue", new Color(0.12f, 0.48f, 0.76f));
            var timber = Material("Timber", new Color(0.40f, 0.28f, 0.19f));
            var trim = Material("PalaceTrim", new Color(0.72f, 0.73f, 0.70f));
            var dust = CreateDustMaterial();
            BuildOne("Wall", 24, stone, interior, roof, timber, trim, dust);
            BuildOne("Building", 36, stone, interior, roof, timber, trim, dust);
            BuildOne("Palace", 48, stone, interior, roof, timber, trim, dust);
            AssetDatabase.SaveAssets();
        }

        private static void BuildOne(string kind, int count, Material stone, Material interior,
            Material roof, Material timber, Material trim, Material dustMaterial)
        {
            var root = new GameObject("PF_CRHex_Debris_" + kind);
            root.SetActive(false);
            try
            {
                var pieces = new Transform[count];
                var isWall = kind == "Wall";
                var isPalace = kind == "Palace";
                var random = new System.Random(count * 917);
                for (var i = 0; i < count; i++)
                {
                    var roofPiece = !isWall && i >= count - 12;
                    var pillarPiece = isPalace && i >= 32 && i < 36;
                    var piece = new GameObject(roofPiece ? "Roof_" + i : pillarPiece ? "Pillar_" + i : "Stone_" + i);
                    piece.transform.SetParent(root.transform, false);
                    pieces[i] = piece.transform;
                    if (isWall)
                    {
                        piece.transform.localPosition = new Vector3((i % 4 - 1.5f) * 0.245f,
                            0.16f + i / 8 * 0.33f, (i / 4 % 2 - 0.5f) * 0.4f);
                        piece.transform.localScale = new Vector3(0.255f, 0.335f, 0.42f);
                    }
                    else if (roofPiece)
                    {
                        var n = i - count + 12;
                        piece.transform.localPosition = new Vector3((n % 3 - 1f) * 0.32f, 0.84f,
                            (n / 3 - 1.5f) * 0.24f);
                        piece.transform.localScale = new Vector3(0.335f, 0.23f, 0.255f);
                    }
                    else if (pillarPiece)
                    {
                        var n = i - 32;
                        piece.transform.localPosition = new Vector3((n % 2 - 0.5f) * 0.81f, 0.69f, (n / 2 - 0.5f) * 0.81f);
                        piece.transform.localScale = new Vector3(0.16f, 0.30f, 0.16f);
                    }
                    else
                    {
                        var columns = isPalace ? 4 : 3;
                        piece.transform.localPosition = new Vector3((i % columns - (columns - 1f) * 0.5f) * (0.96f / columns),
                            0.16f + i / (columns * 4) * 0.32f, (i / columns % 4 - 1.5f) * 0.24f);
                        piece.transform.localScale = new Vector3(1f / columns, 0.33f, 0.25f);
                    }

                    var mesh = CreateChunkMesh(random, roofPiece);
                    mesh.name = "SM_Debris_" + kind + "_" + i.ToString("00");
                    var meshPath = ArtFolder + "/" + mesh.name + ".asset";
                    var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existingMesh == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existingMesh);
                        Object.DestroyImmediate(mesh);
                        mesh = existingMesh;
                    }
                    piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = piece.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = new[] { roofPiece ? roof : pillarPiece ? trim : stone,
                        roofPiece ? timber : interior };
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                var dust = CreateDust(root.transform, dustMaterial, isPalace ? 32 : isWall ? 16 : 24);
                root.AddComponent<HexCastleDestructionVisual>().EditorConfigure(pieces, dust,
                    isPalace ? 1.05f : isWall ? 0.75f : 0.9f, isPalace ? 0.22f : 0.30f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + root.name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Mesh CreateChunkMesh(System.Random random, bool roof)
        {
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var x = (i & 1) == 0 ? -0.5f : 0.5f;
                var y = (i & 2) == 0 ? -0.5f : 0.5f;
                var z = (i & 4) == 0 ? -0.5f : 0.5f;
                var inset = 0.04f + (float)random.NextDouble() * 0.40f;
                corners[i] = new Vector3(x * (1f - inset),
                    y * (1f - (float)random.NextDouble() * (roof ? 0.12f : 0.35f)),
                    z * (1f - (float)random.NextDouble() * 0.32f));
                if (roof && y > 0f) corners[i].x *= 0.18f;
            }
            var faces = new[] { 0,4,6,2, 1,3,7,5, 0,1,5,4, 2,6,7,3, 0,2,3,1, 4,5,7,6 };
            var vertices = new Vector3[24];
            var exterior = new List<int>();
            var cut = new List<int>();
            for (var face = 0; face < 6; face++)
            {
                var start = face * 4;
                for (var j = 0; j < 4; j++) vertices[start + j] = corners[faces[start + j]];
                var triangles = face == 0 || face == 2 ? cut : exterior;
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(exterior, 0);
            mesh.SetTriangles(cut, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material Material(string name, Color color)
        {
            var path = ArtFolder + "/MAT_Debris_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader missing");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.08f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateDustMaterial()
        {
            var texturePath = ArtFolder + "/T_DebrisDust.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "T_DebrisDust", wrapMode = TextureWrapMode.Clamp };
                for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 1.6f)));
                }
                texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            var path = ArtFolder + "/MAT_Debris_Dust.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new InvalidOperationException("URP particle shader missing");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem CreateDust(Transform parent, Material material, int count)
        {
            var child = new GameObject("Dust");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.up * 0.18f;
            var particle = child.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.36f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startColor = new Color(0.68f, 0.66f, 0.60f, 0.5f);
            main.maxParticles = count;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particle.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var color = particle.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = particle.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.4f));
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particle;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
