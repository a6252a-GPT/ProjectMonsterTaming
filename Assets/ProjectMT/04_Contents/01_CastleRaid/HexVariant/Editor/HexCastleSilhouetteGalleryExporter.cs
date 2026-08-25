using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleSilhouetteGalleryExporter // 동일 카메라로 실루엣 후보를 일괄 비교한다
    {
        public const int DefaultSeed = 10801;
        public const int DefaultDefenseLayers = 3;
        public const int CaptureWidth = 1280;
        public const int CaptureHeight = 800;
        public const string MenuPath = "JC Tool/군단의 역습 육각/정식 테마/A~I 전체 배치 비교 이미지 생성";

        [MenuItem(MenuPath)]
        public static void ExportDefaultGallery()
        {
            var paths = ExportAll(DefaultSeed, DefaultDefenseLayers);
            Debug.Log($"[Hex Silhouette Gallery] {paths.Count}장 생성 완료: {ResolveOutputFolder()}");
            EditorUtility.RevealInFinder(paths[paths.Count - 1]);
        }

        public static IReadOnlyList<string> ExportAll(int seed, int defenseLayerCount)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Play Mode에서는 실루엣 갤러리를 생성할 수 없습니다.");
            }

            var originalScene = SceneManager.GetActiveScene();
            if (!originalScene.IsValid() || originalScene.isDirty || string.IsNullOrWhiteSpace(originalScene.path))
            {
                throw new InvalidOperationException("현재 씬이 저장된 Clean 상태여야 안전하게 갤러리를 생성할 수 있습니다.");
            }

            var originalScenePath = originalScene.path;
            var outputFolder = ResolveOutputFolder();
            Directory.CreateDirectory(outputFolder);
            var outputPaths = new List<string>();
            var textures = new List<Texture2D>();
            RenderTexture renderTexture = null;
            try
            {
                var previewScene = EditorSceneManager.OpenScene(
                    HexCastleSceneSetupUtility.ScenePath,
                    OpenSceneMode.Single);
                var camera = previewScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault();
                if (camera == null)
                {
                    throw new InvalidOperationException("육각 개발용 씬의 Camera를 찾지 못했습니다.");
                }

                renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "RT_CRHex_SilhouetteGallery",
                    antiAliasing = 4
                };
                renderTexture.Create();
                var tuning = HexCastleThemeOneRulesAssetUtility.LoadOrCreate().Tuning;
                foreach (var theme in HexCastleThemeCatalog.ComparisonThemes)
                {
                    HexCastleFoundationVisualGate.Create(
                        seed,
                        defenseLayerCount,
                        theme,
                        tuning,
                        true);
                    var previewRoot = GameObject.Find(HexCastleFoundationVisualGate.RootName);
                    if (previewRoot == null)
                    {
                        throw new InvalidOperationException($"{theme} 3D 미리보기 Root가 없습니다.");
                    }

                    PrepareFormalPreview(previewRoot);

                    ReframeCamera(camera, previewRoot);
                    var texture = Capture(camera, renderTexture, theme);
                    textures.Add(texture);
                    var code = HexCastleThemeCatalog.ResolveCode(theme);
                    var path = Path.Combine(
                        outputFolder,
                        $"{code}_{theme}_{defenseLayerCount}W_Seed{seed}.png");
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    outputPaths.Add(path);
                    HexCastleFoundationVisualGate.Remove(previewScene);
                }

                var contactSheet = BuildContactSheet(textures, 3, 640, 400);
                try
                {
                    var contactPath = Path.Combine(
                        outputFolder,
                        $"00_A-I_Formal_{defenseLayerCount}W_Seed{seed}.png");
                    File.WriteAllBytes(contactPath, contactSheet.EncodeToPNG());
                    outputPaths.Insert(0, contactPath);
                }
                finally
                {
                    Object.DestroyImmediate(contactSheet);
                }

                return outputPaths;
            }
            finally
            {
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    HexCastleFoundationVisualGate.Remove(activeScene);
                }
                foreach (var texture in textures)
                {
                    Object.DestroyImmediate(texture);
                }
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }
                if (!string.Equals(SceneManager.GetActiveScene().path, originalScenePath, StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }
        }

        public static string ResolveOutputFolder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var workspaceRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;
            return Path.Combine(
                workspaceRoot,
                "ProjectMT 개인파일",
                "Docs",
                "Images",
                "50C_육각성_정식테마_A_I_20260825");
        }

        private static void PrepareFormalPreview(GameObject previewRoot)
        {
            foreach (var cell in previewRoot.GetComponentsInChildren<HexCastleCellRuntime>(true))
            {
                if (cell.TileVisualRoot != null)
                {
                    cell.TileVisualRoot.gameObject.SetActive(false);
                }
            }

            foreach (var childName in new[] { "01_HexGridOverlay", "02_ActualMonsterScale" })
            {
                var child = previewRoot.transform.Find(childName);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void ReframeCamera(Camera camera, GameObject previewRoot)
        {
            var renderers = previewRoot.GetComponentsInChildren<Renderer>(false)
                .Where(renderer => renderer.enabled)
                .ToArray();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("실루엣 후보에 Renderer가 없습니다.");
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            var controller = camera.GetComponent<HexCastleCameraController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<HexCastleCameraController>();
            }
            controller.ConfigureBounds(bounds);
            controller.SetRotationFocus(bounds.center);
            controller.ResetView();
            FitBoundsInsideViewport(camera, bounds, 0.07f);
        }

        private static void FitBoundsInsideViewport(Camera camera, Bounds bounds, float margin)
        {
            var corners = new[]
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var allVisible = corners
                    .Select(camera.WorldToViewportPoint)
                    .All(point => point.z > 0f &&
                                  point.x >= margin && point.x <= 1f - margin &&
                                  point.y >= margin && point.y <= 1f - margin);
                if (allVisible)
                {
                    return;
                }

                var offset = camera.transform.position - bounds.center;
                camera.transform.position = bounds.center + offset * 1.12f;
            }

            throw new InvalidOperationException("실루엣 전체가 촬영 화면 안에 들어오지 않습니다.");
        }

        private static Texture2D Capture(
            Camera camera,
            RenderTexture renderTexture,
            HexCastleTheme theme)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.aspect = CaptureWidth / (float)CaptureHeight;
                camera.Render();
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false)
                {
                    name = $"CRHex_{theme}_Capture"
                };
                texture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0, false);
                texture.Apply(false, false);
                DrawThemeBadge(texture, HexCastleThemeCatalog.ResolveCode(theme));
                return texture;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }
        }

        private static Texture2D BuildContactSheet(
            IReadOnlyList<Texture2D> sources,
            int columns,
            int tileWidth,
            int tileHeight)
        {
            var rows = Mathf.CeilToInt(sources.Count / (float)columns);
            var texture = new Texture2D(
                columns * tileWidth,
                rows * tileHeight,
                TextureFormat.RGBA32,
                false)
            {
                name = "CRHex_Silhouette_ContactSheet"
            };
            var background = Enumerable.Repeat(
                new Color32(18, 21, 24, 255),
                texture.width * texture.height).ToArray();
            texture.SetPixels32(background);

            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                var sourcePixels = source.GetPixels32();
                var scaled = new Color32[tileWidth * tileHeight];
                for (var y = 0; y < tileHeight; y++)
                {
                    var sourceY = Mathf.Min(source.height - 1, y * source.height / tileHeight);
                    for (var x = 0; x < tileWidth; x++)
                    {
                        var sourceX = Mathf.Min(source.width - 1, x * source.width / tileWidth);
                        scaled[y * tileWidth + x] = sourcePixels[sourceY * source.width + sourceX];
                    }
                }

                var column = index % columns;
                var rowFromTop = index / columns;
                var row = rows - rowFromTop - 1;
                texture.SetPixels32(column * tileWidth, row * tileHeight, tileWidth, tileHeight, scaled);
            }

            texture.Apply(false, false);
            return texture;
        }

        private static void DrawThemeBadge(Texture2D texture, char code)
        {
            var pixels = texture.GetPixels32();
            const int badgeSize = 78;
            for (var y = texture.height - badgeSize - 18; y < texture.height - 18; y++)
            {
                for (var x = 18; x < badgeSize + 18; x++)
                {
                    pixels[y * texture.width + x] = new Color32(8, 12, 18, 225);
                }
            }

            var pattern = ResolveGlyph(code);
            const int scale = 8;
            var originX = 37;
            var originY = texture.height - 34;
            for (var row = 0; row < pattern.Length; row++)
            {
                for (var column = 0; column < pattern[row].Length; column++)
                {
                    if (pattern[row][column] != '1')
                    {
                        continue;
                    }
                    for (var py = 0; py < scale; py++)
                    {
                        for (var px = 0; px < scale; px++)
                        {
                            var x = originX + column * scale + px;
                            var y = originY - (row + 1) * scale + py;
                            pixels[y * texture.width + x] = new Color32(245, 250, 255, 255);
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private static string[] ResolveGlyph(char glyph)
        {
            switch (glyph)
            {
                case 'A': return new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
                case 'B': return new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" };
                case 'C': return new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" };
                case 'D': return new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" };
                case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
                case 'F': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
                case 'G': return new[] { "01111", "10000", "10000", "10111", "10001", "10001", "01111" };
                case 'H': return new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" };
                case 'I': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };


                default: return new[] { "11111", "00001", "00010", "00100", "00100", "00000", "00100" };
            }
        }
    }
}
