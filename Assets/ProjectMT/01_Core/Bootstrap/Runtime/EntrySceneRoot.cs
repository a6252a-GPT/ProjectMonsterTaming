using System;
using System.Collections;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Features.Settings;
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
        [SerializeField] private TitleScreenController titleScreen; // 타이틀·게스트 진입 화면

        public SceneId SceneId => sceneId;
        public bool IsInitialized { get; private set; }
        private bool enteringMainBattle;

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

            titleScreen ??= GetComponentInChildren<TitleScreenController>(true);
            if (titleScreen == null)
            {
                throw new InvalidOperationException("TitleScreenController is missing from Entry scene.");
            }

            IsInitialized = true;
            titleScreen.Configure(
                () => LoginAsGuest(entryContext),
                () => titleScreen.ShowStatus("Google 로그인은 추후 인증 계약 연결 후 제공됩니다."),
                AccountSessionStore.IsLoggedIn ? () => ContinueSession(entryContext) : (Action)null);
            titleScreen.ShowTitle(); // 저장된 세션도 매 실행마다 화면 터치를 기다림
        }

        public void Shutdown()
        {
            StopAllCoroutines(); // 예약된 이동 취소
            titleScreen?.Shutdown();
            IsInitialized = false;
            enteringMainBattle = false;
        }

        private void ContinueSession(EntrySceneContext context)
        {
            if (!IsInitialized || enteringMainBattle)
            {
                return;
            }

            enteringMainBattle = true;
            titleScreen.ShowLoading("저장 데이터를 불러오는 중입니다...");
            StartCoroutine(LoadMainBattleNextFrame(context));
        }

        private void LoginAsGuest(EntrySceneContext context)
        {
            if (!IsInitialized || enteringMainBattle)
            {
                return;
            }

            enteringMainBattle = true;
            AccountSessionStore.LoginAsGuest();
            titleScreen.ShowLoading("게스트 데이터를 준비하는 중입니다...");
            StartCoroutine(LoadMainBattleNextFrame(context));
        }

        private static IEnumerator LoadMainBattleNextFrame(EntrySceneContext context)
        {
            yield return null;
            context.LoadMainBattle();
        }
    }
}
