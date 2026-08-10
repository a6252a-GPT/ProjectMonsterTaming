using System;
using ProjectMT.Shared.Input;
using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.07 안건준 추가 - 수호자의 탑 전용 스크립트.
    // 바닥(Ground) 크기를 조절해도 네 모서리 기둥(Wall)과 그 체력 게이지가
    // 항상 바닥 모서리에서 같은 거리(edgeMarginX / edgeMarginZ)에 위치하도록 매 프레임 보정한다.
    // 에디터에서 바닥 크기만 바꾸면 되고, 기둥 좌표를 손으로 다시 옮길 필요가 없다.
    // 다른 던전에는 영향이 없는 수호자의 탑 전용 컴포넌트다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GuardiansTowerFieldLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class CornerEntry
        {
            [SerializeField] private Transform pillar; // Wall_1~4
            [SerializeField] private Transform healthBar; // 같은 기둥의 월드 스페이스 체력 게이지
            [SerializeField] private Vector2 cornerSign = new Vector2(-1f, -1f); // 바닥 기준 모서리 방향(X, Z 각각 -1 또는 1)

            public Transform Pillar => pillar;
            public Transform HealthBar => healthBar;
            public Vector2 CornerSign => cornerSign;

            public CornerEntry()
            {
            }

            public CornerEntry(Transform pillarTransform, Transform healthBarTransform, Vector2 sign)
            {
                pillar = pillarTransform;
                healthBar = healthBarTransform;
                cornerSign = sign;
            }
        }

        [SerializeField] private Transform ground; // 바닥 Transform (기본 Cube 메시 기준 localScale = 전체 크기)
        [SerializeField] private CornerEntry[] corners = new CornerEntry[0]; // 네 모서리 기둥·게이지 목록
        // 08.07 안건준 수정 - 좌우(X)와 위아래(Z) 여백을 따로 조절할 수 있게 분리.
        // 카메라가 군단장을 따라갈 때 화면 좌우로 바닥 끝이 보이면 edgeMarginX를 키우고 Ground.x도 같이 키우면 된다.
        // (기둥 위치 = 바닥 반쪽 크기 - edgeMargin 이므로, 기둥 자리를 유지하려면 둘을 같은 양만큼 키워야 한다)
        [SerializeField, Min(0f)] private float edgeMarginX = 1f; // 좌우: 바닥 끝에서 기둥이 안쪽으로 들어가는 거리
        [SerializeField, Min(0f)] private float edgeMarginZ = 1f; // 위아래: 바닥 끝에서 기둥이 안쪽으로 들어가는 거리
        // 08.07 안건준 추가 - 기둥을 손으로 늘리거나 줄여도(높이가 바뀌어도) 체력 게이지·버프 글자가
        // 기둥 속에 파묻히지 않도록, 매 프레임 기둥의 실제 렌더링 높이(맨 위)를 구해서 그 위에 띄운다.
        [SerializeField, Min(0f)] private float healthBarHeightMargin = 0.4f; // 기둥 맨 위에서 게이지까지 띄우는 여유 높이

        [Header("Player Movement")]
        [SerializeField] private CommanderMoveController commanderMove; // 있으면 이동 가능 범위도 기둥 위치 기준으로 보정
        [SerializeField, Min(0f)] private float movementReachBuffer = 0.5f; // 기둥 위치에서 조금 더 다가갈 수 있게 더하는 여유(값)
        // 08.07 안건준 수정 - 이동 범위는 "바닥 전체 크기"가 아니라 "기둥 위치(halfExtent - edgeMargin)"를 기준으로 계산한다.
        // 바닥을 기둥보다 훨씬 크게 만들어 화면에 바닥 끝이 안 보이게 해도, 플레이어가 그 여백까지 걸어나가지 않도록 하기 위함.

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Apply(); // 에디터에서 바닥 크기를 바꾸는 즉시 반영되도록 매 프레임 보정 (연산량이 매우 적어 실행 중에도 문제 없음)
        }

        // 바닥 크기·위치를 기준으로 기둥의 X/Z 좌표를 재계산한다.
        // 체력 게이지는 X/Z는 기둥과 맞추고, Y는 기둥의 실제 렌더링 높이 위로 재계산한다.
        // 08.07 안건준 수정 - 기존에는 Y를 손으로 설정한 값 그대로 썼는데, 기둥 높이를 늘리면
        // 게이지가 기둥 속에 파묻혀 안 보이는 문제가 있어 기둥 위쪽 높이를 기준으로 자동 보정하도록 변경.
        public void Apply()
        {
            if (ground == null || corners == null)
            {
                return;
            }

            var halfExtentX = Mathf.Max(0f, ground.localScale.x * 0.5f - edgeMarginX);
            var halfExtentZ = Mathf.Max(0f, ground.localScale.z * 0.5f - edgeMarginZ);
            var groundLocalPosition = ground.localPosition;

            foreach (var corner in corners)
            {
                if (corner?.Pillar == null)
                {
                    continue;
                }

                var targetX = groundLocalPosition.x + corner.CornerSign.x * halfExtentX;
                var targetZ = groundLocalPosition.z + corner.CornerSign.y * halfExtentZ;

                var pillarPosition = corner.Pillar.localPosition;
                pillarPosition.x = targetX;
                pillarPosition.z = targetZ;
                corner.Pillar.localPosition = pillarPosition;

                if (corner.HealthBar == null)
                {
                    continue;
                }

                var barPosition = corner.HealthBar.localPosition;
                barPosition.x = targetX;
                barPosition.z = targetZ;
                barPosition.y = ResolveHealthBarHeight(corner.Pillar, barPosition.y); // 08.07 안건준 추가
                corner.HealthBar.localPosition = barPosition;
            }

            if (commanderMove != null)
            {
                // 08.07 안건준 추가 - 군단장 이동 가능 범위를 바닥 중심 기준으로 재계산.
                // (군단장 시작 위치 기준 반경만 쓰면 바닥이 커져도 기둥까지 다가갈 수 없었음)
                // 바닥 전체 크기가 아닌 기둥 위치(halfExtentX/Z) 기준이라, 바닥을 기둥보다 크게 키워
                // 화면 밖 여백을 만들어도 플레이어가 그 여백으로 걸어나가지 않는다.
                var movementHalfExtents = new Vector2(
                    halfExtentX + movementReachBuffer,
                    halfExtentZ + movementReachBuffer);
                commanderMove.SetMovementBounds(ground.position, movementHalfExtents);
            }
        }

        // 08.07 안건준 추가 - 기둥의 실제 렌더링 범위(Renderer.bounds)를 기준으로 맨 위 높이를 구해
        // 게이지가 항상 기둥 표면 위쪽에 뜨도록 한다. 렌더러를 못 찾으면 기존에 손으로 설정해둔 높이를 그대로 쓴다.
        private float ResolveHealthBarHeight(Transform pillar, float fallbackLocalY)
        {
            if (pillar == null || !pillar.TryGetComponent<Renderer>(out var pillarRenderer))
            {
                return fallbackLocalY;
            }

            var topWorldPosition = new Vector3(
                pillarRenderer.bounds.center.x,
                pillarRenderer.bounds.max.y,
                pillarRenderer.bounds.center.z);
            var topLocalY = transform.InverseTransformPoint(topWorldPosition).y;
            return topLocalY + healthBarHeightMargin;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Transform groundTransform,
            CornerEntry[] cornerList,
            float marginX,
            float marginZ,
            CommanderMoveController moveController = null,
            float reachBuffer = 0.5f,
            float barHeightMargin = 0.4f)
        {
            ground = groundTransform;
            corners = cornerList ?? new CornerEntry[0];
            edgeMarginX = marginX;
            edgeMarginZ = marginZ;
            commanderMove = moveController;
            movementReachBuffer = reachBuffer;
            healthBarHeightMargin = barHeightMargin;
        }
#endif
    }
}
