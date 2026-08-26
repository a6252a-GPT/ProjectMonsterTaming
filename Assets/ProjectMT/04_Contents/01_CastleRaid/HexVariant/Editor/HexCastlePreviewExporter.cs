using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastlePreviewExporter
    {
        public const string GalleryFolder =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/PreviewGallery/SilhouetteThemes";

        public static string Export(HexCastleCandidate candidate, int imageSize = 900)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            Directory.CreateDirectory(GalleryFolder);
            var layout = candidate.Layout;
            var code = HexCastleThemeCatalog.ResolveCode(layout.Theme);
            var path = $"{GalleryFolder}/HexTheme{code}_{layout.DefenseLayerCount}W_{layout.Seed}.png";
            Texture2D texture = null;
            try
            {
                texture = BuildTexture(candidate, imageSize);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ConfigureTextureImporter(path);
                return path;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        public static IReadOnlyList<string> ExportThemeGallery(int seed, int defenseLayerCount)
        {
            var pipeline = new HexCastleGenerationPipeline();
            var tuning = HexCastleThemeOneRulesAssetUtility.LoadOrCreate().Tuning;
            return HexCastleThemeCatalog.Themes
                .Select(theme => Export(pipeline.GenerateFoundation(
                    seed,
                    defenseLayerCount,
                    theme,
                    tuning)))
                .ToArray();
        }

        public static Texture2D BuildTexture(HexCastleCandidate candidate, int imageSize)
        {
            imageSize = Mathf.Clamp(imageSize, 256, 2048);
            var texture = new Texture2D(imageSize, imageSize, TextureFormat.RGBA32, false)
            {
                name = $"HexPreview_{candidate.Layout.Theme}_{candidate.Layout.DefenseLayerCount}W",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = Enumerable.Repeat(new Color32(7, 13, 22, 255), imageSize * imageSize).ToArray();
            var layout = candidate.Layout;
            var worldPoints = layout.Cells.Keys.Select(value => value.ToWorld(1f)).ToArray();
            var minX = worldPoints.Min(point => point.x) - 1.5f;
            var maxX = worldPoints.Max(point => point.x) + 1.5f;
            var minZ = worldPoints.Min(point => point.z) - 1.5f;
            var maxZ = worldPoints.Max(point => point.z) + 1.5f;
            var scale = Mathf.Min(
                (imageSize - 36f) / Mathf.Max(1f, maxX - minX),
                (imageSize - 36f) / Mathf.Max(1f, maxZ - minZ));
            var routeCells = new HashSet<HexCoordinates>(
                candidate.Validation.EntryRoutes
                    .Where(route => route.IsComplete)
                    .SelectMany(route => route.Path));

            foreach (var cell in layout.Cells.Values.OrderBy(value => value.Kind))
            {
                var world = cell.Coordinates.ToWorld(1f);
                var x = Mathf.RoundToInt(18f + (world.x - minX) * scale);
                var y = Mathf.RoundToInt(18f + (world.z - minZ) * scale);
                var radius = Mathf.Max(2, Mathf.RoundToInt(scale * 0.47f));
                var color = ResolveColor(cell, layout.Theme);
                DrawHex(pixels, imageSize, imageSize, x, y, radius, color);
                if (routeCells.Contains(cell.Coordinates))
                {
                    DrawHexOutline(
                        pixels,
                        imageSize,
                        imageSize,
                        x,
                        y,
                        radius,
                        new Color32(45, 255, 213, 255));
                }
            }

            var accent = (Color32)HexCastleThemeCatalog.ResolveAccent(layout.Theme);
            FillRect(pixels, imageSize, imageSize, 0, imageSize - 12, imageSize, 12, accent);
            var glyphScale = Mathf.Max(3, imageSize / 180);
            DrawGlyph(
                pixels,
                imageSize,
                imageSize,
                18,
                imageSize - 54,
                HexCastleThemeCatalog.ResolveCode(layout.Theme),
                glyphScale,
                new Color32(242, 250, 255, 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void ConfigureTextureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static Color32 ResolveColor(HexCastleCell cell, HexCastleTheme theme)
        {
            return HexCastleVisualPalette.ResolveColor(
                cell,
                theme,
                HexCastlePreviewColorMode.Architecture);
        }

        private static void DrawHex(
            Color32[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            Color32 color)
        {
            var halfHeight = Mathf.Max(1, Mathf.RoundToInt(radius * 0.86f));
            for (var y = -halfHeight; y <= halfHeight; y++)
            {
                var normalized = Mathf.Abs(y) / (float)halfHeight;
                var halfWidth = Mathf.RoundToInt(radius * (1f - 0.5f * normalized));
                for (var x = -halfWidth; x <= halfWidth; x++)
                {
                    SetPixel(pixels, width, height, centerX + x, centerY + y, color);
                }
            }
        }

        private static void DrawHexOutline(
            Color32[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            Color32 color)
        {
            var halfHeight = Mathf.Max(1, Mathf.RoundToInt(radius * 0.86f));
            for (var y = -halfHeight; y <= halfHeight; y++)
            {
                var normalized = Mathf.Abs(y) / (float)halfHeight;
                var halfWidth = Mathf.RoundToInt(radius * (1f - 0.5f * normalized));
                if (Mathf.Abs(y) == halfHeight)
                {
                    for (var x = -halfWidth; x <= halfWidth; x++)
                    {
                        SetPixel(pixels, width, height, centerX + x, centerY + y, color);
                    }

                    continue;
                }

                SetPixel(pixels, width, height, centerX - halfWidth, centerY + y, color);
                SetPixel(pixels, width, height, centerX + halfWidth, centerY + y, color);
            }
        }

        private static void FillRect(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            int rectWidth,
            int rectHeight,
            Color32 color)
        {
            for (var iy = y; iy < y + rectHeight; iy++)
            {
                for (var ix = x; ix < x + rectWidth; ix++)
                {
                    SetPixel(pixels, width, height, ix, iy, color);
                }
            }
        }

        private static void DrawGlyph(
            Color32[] pixels,
            int width,
            int height,
            int originX,
            int originY,
            char glyph,
            int scale,
            Color32 color)
        {
            var pattern = ResolveGlyph(glyph);
            for (var row = 0; row < pattern.Length; row++)
            {
                for (var column = 0; column < pattern[row].Length; column++)
                {
                    if (pattern[row][column] != '1')
                    {
                        continue;
                    }
                    FillRect(
                        pixels,
                        width,
                        height,
                        originX + column * scale,
                        originY - row * scale,
                        scale,
                        scale,
                        color);
                }
            }
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
                case 'J': return new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" };
                case 'K': return new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" };
                case 'L': return new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" };
                case 'M': return new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" };
                case 'N': return new[] { "10001", "11001", "11001", "10101", "10011", "10011", "10001" };
                case 'T': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" };
                case '1': return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
                default: return new[] { "11111", "10001", "00010", "00100", "00100", "00000", "00100" };
            }
        }

        private static void SetPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            Color32 color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }
            pixels[y * width + x] = color;
        }
    }
}
