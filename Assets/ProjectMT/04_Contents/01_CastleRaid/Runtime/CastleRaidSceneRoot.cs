using System;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleRaidSceneRoot : MonoBehaviour, ISceneRoot // 성 침공 씬 수명 관리
    {
        [SerializeField] private SceneId sceneId = new SceneId("castle_raid"); // CastleRaid 씬 식별자
        [SerializeField] private CastleRaidSceneConfig sceneConfig; // 콘텐츠 ID 검증 설정
        [SerializeField] private CastleRaidController controller; // 실제 성 침공 실행

        public SceneId SceneId => sceneId;
        public bool IsInitialized { get; private set; }

        public void Initialize(ISceneContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            var contentSceneContext = context as ContentSceneContext;
            if (contentSceneContext == null)
            {
                throw new ArgumentException("ContentSceneContext is required.", nameof(context));
            }

            if (controller == null || sceneConfig == null ||
                contentSceneContext.Definition.ContentId != sceneConfig.ContentId)
            {
                throw new InvalidOperationException("Castle Raid scene configuration is invalid.");
            }

            controller.Initialize(contentSceneContext.ContentContext); // 검증된 Context로 플레이 시작
            IsInitialized = true;
        }

        public void Shutdown()
        {
            controller?.Shutdown();
            IsInitialized = false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(CastleRaidSceneConfig config, CastleRaidController raidController)
        {
            sceneConfig = config;
            controller = raidController;
        }
#endif
    }
}
