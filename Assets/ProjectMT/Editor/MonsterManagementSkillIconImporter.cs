using System;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Editor
{
    public sealed class MonsterManagementSkillIconImporter : AssetPostprocessor
    {
        private const string IconFolder = "Assets/ProjectMT/03_Features/Formation/Resources/MonsterSkillIcons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconFolder, StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 256;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }

        public override uint GetVersion() => 1; // 새 스킬 아이콘 폴더에만 같은 Import 규격 적용
    }
}
