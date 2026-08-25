using System;
using ProjectMT.Contents.Framework;
using ProjectMT.Core.SceneFlow;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleRaidSceneRoot : MonoBehaviour, ISceneRoot // 육각 군단의 역습 전용 씬 수명
    {
        [SerializeField] private SceneId sceneId = new SceneId("castle_raid_hex");
        [SerializeField] private ContentId contentId = new ContentId("castle_raid");
        [SerializeField] private ContentVariantId variantId = new ContentVariantId("hex");
        [SerializeField] private HexCastleRaidController controller;

        public SceneId SceneId => sceneId;
        public bool IsInitialized { get; private set; }

        public void Initialize(ISceneContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            if (!(context is ContentSceneContext contentSceneContext))
            {
                throw new ArgumentException("ContentSceneContext가 필요합니다.", nameof(context));
            }

            var runInfo = contentSceneContext.ContentContext.RunInfo;
            if (controller == null || contentSceneContext.Definition.ContentId != contentId ||
                runInfo.ContentId != contentId || runInfo.VariantId != variantId)
            {
                throw new InvalidOperationException("육각 군단의 역습 씬 연결이 올바르지 않습니다.");
            }

            controller.Initialize(contentSceneContext.ContentContext);
            IsInitialized = true;
        }

        public void Shutdown()
        {
            controller?.Shutdown();
            IsInitialized = false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(HexCastleRaidController raidController)
        {
            controller = raidController;
        }
#endif
    }
}
