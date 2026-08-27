using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    internal static class HexCastlePreviewUtility // 현재 Foundation Preview의 공통 시각 도우미
    {
        private static readonly string[] MonsterPrefabPaths =
        {
            "Assets/ProjectMT/05_Art/Monsters/mukuk_01/PF_mukuk_01_VisualAdapter.prefab",
            "Assets/ProjectMT/05_Art/Monsters/shakun_01/PF_shakun_01_VisualAdapter.prefab",
            "Assets/ProjectMT/05_Art/Monsters/lumi_01/PF_lumi_01_VisualAdapter.prefab",
            "Assets/ProjectMT/05_Art/Monsters/aru_01/PF_aru_01_VisualAdapter.prefab",
            "Assets/ProjectMT/05_Art/Monsters/rabi_queen_01/PF_rabi_queen_01_VisualAdapter.prefab"
        };

        public static Dictionary<string, Material> CreateMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? shader;
            return new Dictionary<string, Material>
            {
                ["ground"] = CreateMaterial(shader, "MAT_CRHex_PreviewGround", new Color(0.18f, 0.22f, 0.20f, 1f)),
                ["build"] = CreateMaterial(shader, "MAT_CRHex_PreviewBuild", new Color(0.22f, 0.26f, 0.23f, 1f)),
                ["grid"] = CreateMaterial(lineShader, "MAT_CRHex_PreviewGrid", new Color(0.10f, 0.32f, 0.28f, 1f)),
                ["stone"] = CreateMaterial(shader, "MAT_CRHex_PreviewStone", new Color(0.68f, 0.69f, 0.67f, 1f)),
                ["marker"] = CreateMaterial(shader, "MAT_CRHex_PreviewMarker", new Color(0.12f, 0.62f, 0.56f, 1f))
            };
        }

        public static IReadOnlyList<float> CreateMonsterScaleRow(
            Transform parent,
            IReadOnlyDictionary<string, Material> materials,
            float centerX,
            float worldZ,
            float spacing)
        {
            var monsterRoot = CreateChild("Monsters", parent);
            var heights = new List<float>();
            var startX = centerX - spacing * (MonsterPrefabPaths.Length - 1) * 0.5f;
            for (var index = 0; index < MonsterPrefabPaths.Length; index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPaths[index]);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Monster Scale Prefab을 찾지 못했습니다: {MonsterPrefabPaths[index]}");
                }

                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException($"Monster Scale Prefab 생성에 실패했습니다: {prefab.name}");
                }

                instance.name = $"MONSTER_{index + 1:00}_{prefab.name}";
                instance.hideFlags = HideFlags.DontSaveInEditor;
                instance.transform.SetParent(monsterRoot, false);
                instance.transform.position = new Vector3(startX + spacing * index, 0f, worldZ);
                instance.transform.rotation = Quaternion.identity;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var bounds = ResolveBounds(renderers);
                instance.transform.position += Vector3.up * -bounds.min.y;
                heights.Add(ResolveBounds(renderers).size.y);

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "ScaleDisc";
                marker.hideFlags = HideFlags.DontSaveInEditor;
                marker.transform.SetParent(monsterRoot, false);
                marker.transform.position = new Vector3(
                    instance.transform.position.x,
                    0.025f,
                    instance.transform.position.z);
                marker.transform.localScale = new Vector3(0.72f, 0.025f, 0.72f);
                marker.GetComponent<Renderer>().sharedMaterial = materials["marker"];
                marker.GetComponent<Collider>().enabled = false;
                marker.transform.SetSiblingIndex(index);
            }

            return heights;
        }

        public static Bounds ResolveBounds(IReadOnlyList<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name)
            {
                hideFlags = HideFlags.DontSaveInEditor
            };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Material CreateMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.12f);
            }

            return material;
        }
    }
}
