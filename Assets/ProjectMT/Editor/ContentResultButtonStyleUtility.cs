using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Editor
{
    public static class ContentResultButtonStyleUtility
    {
        private const string ResultOverlayPath =
            "Assets/ProjectMT/01_Core/Bootstrap/Prefabs/PF_ContentResultOverlay.prefab";

        private const string FlatButtonSpritePath =
            "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Shared/Sprite_Common/Button/Button_01_White_Bg.Png";

        [MenuItem("Tools/ProjectMT/UI/Apply Clean Result Button Style")]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(ResultOverlayPath);
            try
            {
                var confirmButton = FindChild(root.transform, "ConfirmButton");
                var image = confirmButton == null ? null : confirmButton.GetComponent<Image>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlatButtonSpritePath);
                var rewardHeader = FindChild(root.transform, "RewardHeader");
                var rewardHeaderLabel = rewardHeader == null ? null : rewardHeader.Find("Label") as RectTransform;

                if (confirmButton == null || image == null || sprite == null || rewardHeaderLabel == null)
                {
                    throw new InvalidOperationException("결과창 버튼·보상 제목 또는 GUI Pro 버튼 스프라이트를 찾지 못했습니다.");
                }

                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                SetChildActive(confirmButton, "Light", false);
                SetChildActive(confirmButton, "Highlight", false);
                rewardHeaderLabel.anchoredPosition = new Vector2(
                    rewardHeaderLabel.anchoredPosition.x,
                    28f);

                PrefabUtility.SaveAsPrefabAsset(root, ResultOverlayPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[ContentResultButtonStyleUtility] 결과창 버튼을 GUI Pro Button_01 플랫 스타일로 적용했습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetChildActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }
    }
}
