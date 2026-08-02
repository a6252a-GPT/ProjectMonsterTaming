using System;
using System.Collections;
using ProjectMT.Core.SceneFlow;
using UnityEngine;

namespace ProjectMT.Bootstrap
{
    public sealed class EntrySceneContext : ISceneContext // Entry 전용 이동 권한
    {
        private readonly Action loadMainBattle; // 메인전투 이동 요청

        public EntrySceneContext(Action loadMainBattle)
        {
            this.loadMainBattle = loadMainBattle;
        }

        public void LoadMainBattle()
        {
            loadMainBattle?.Invoke();
        }
    }

    [DisallowMultipleComponent]
    public sealed class EntrySceneRoot : MonoBehaviour, ISceneRoot // 시작 씬 초기화 담당
    {
        [SerializeField] private SceneId sceneId = new SceneId("entry"); // Entry 고정 식별자

        public SceneId SceneId => sceneId;
        public bool IsInitialized { get; private set; }

        public void Initialize(ISceneContext context)
        {
            if (IsInitialized)
            {
                return;
            }

            if (!(context is EntrySceneContext entryContext))
            {
                throw new ArgumentException("EntrySceneContext is required.", nameof(context));
            }

            IsInitialized = true;
            StartCoroutine(LoadMainBattleNextFrame(entryContext)); // 한 프레임 뒤 메인 진입
        }

        public void Shutdown()
        {
            StopAllCoroutines(); // 예약된 이동 취소
            IsInitialized = false;
        }

        private static IEnumerator LoadMainBattleNextFrame(EntrySceneContext context)
        {
            yield return null;
            context.LoadMainBattle();
        }
    }
}
