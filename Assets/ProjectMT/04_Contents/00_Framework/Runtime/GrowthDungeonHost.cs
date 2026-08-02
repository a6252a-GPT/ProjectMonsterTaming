using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    [Serializable]
    public sealed class HostedContentSlot // Config와 Scene Instance 연결
    {
        [SerializeField] private GrowthDungeonConfig config; // 콘텐츠 ID·원본 Prefab 정보
        [SerializeField] private GameObject instance; // MainBattle 안 비활성 Instance

        public HostedContentSlot(GrowthDungeonConfig config, GameObject instance)
        {
            this.config = config;
            this.instance = instance;
        }

        public GrowthDungeonConfig Config => config;
        public GameObject Instance => instance;
    }

    [DisallowMultipleComponent]
    public sealed class GrowthDungeonHost : MonoBehaviour // 성장 던전 Instance 실행 자리
    {
        [SerializeField] private List<HostedContentSlot> slots = new List<HostedContentSlot>(); // ID별 실행 자리

        private HostedContentSlot activeSlot; // 현재 켜진 Instance
        private IContentController activeController; // 현재 실행 Controller

        public bool IsOpen => activeController != null;

        public bool Open(ContentContext context)
        {
            if (context == null || IsOpen)
            {
                return false;
            }

            var slot = FindSlot(context.RunInfo.ContentId);
            if (slot == null || slot.Instance == null)
            {
                Debug.LogError($"Hosted content slot is missing. Content={context.RunInfo.ContentId}");
                return false;
            }

            var controller = FindController(slot.Instance);
            if (controller == null)
            {
                Debug.LogError($"Hosted content controller is missing. Content={context.RunInfo.ContentId}");
                return false;
            }

            try
            {
                slot.Instance.SetActive(true); // Initialize 직전에만 활성화
                controller.Initialize(context);
                activeSlot = slot;
                activeController = controller;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                try
                {
                    controller.Shutdown();
                }
                catch (Exception shutdownException)
                {
                    Debug.LogException(shutdownException);
                }

                slot.Instance.SetActive(false); // 부분 초기화 실패 정리
                activeSlot = null;
                activeController = null;
                return false;
            }
        }

        public void Close()
        {
            if (activeController == null)
            {
                return;
            }

            try
            {
                activeController.Shutdown();
            }
            finally
            {
                if (activeSlot?.Instance != null)
                {
                    activeSlot.Instance.SetActive(false); // Shutdown 뒤 Instance 비활성
                }

                activeSlot = null;
                activeController = null;
            }
        }

        private HostedContentSlot FindSlot(ContentId contentId)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i]?.Config != null && slots[i].Config.ContentId == contentId)
                {
                    return slots[i];
                }
            }

            return null;
        }

        private static IContentController FindController(GameObject root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true); // 비활성 Instance 내부까지 검색
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IContentController controller)
                {
                    return controller;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorSetSlots(IEnumerable<HostedContentSlot> values)
        {
            slots = values == null ? new List<HostedContentSlot>() : new List<HostedContentSlot>(values);
        }
#endif
    }

    public sealed class DebugContentExit : IContentExit // DEV 결과 확인용 출구
    {
        public event Action<ContentOutcome, IContentResultData> Exited;

        public void Complete(IContentResultData result)
        {
            Exited?.Invoke(ContentOutcome.Complete, result);
        }

        public void Fail(IContentResultData result = null)
        {
            Exited?.Invoke(ContentOutcome.Fail, result);
        }

        public void Cancel()
        {
            Exited?.Invoke(ContentOutcome.Cancel, null);
        }
    }
}
