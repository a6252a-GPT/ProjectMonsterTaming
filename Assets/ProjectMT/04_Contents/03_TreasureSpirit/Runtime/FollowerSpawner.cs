using UnityEngine;
using UnityEngine.AI;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FollowerAI))]
    public class FollowerSpawner : MonoBehaviour
    {
        [Header("몬스터 데이터 설정 (SO)")]
        [SerializeField] private MonsterDefinition monsterDefinition; // MD
        [SerializeField] private MonsterRuntimeAssetSet runtimeAssetSet; // MR

        [Header("지휘관 설정")]
        [SerializeField] private Transform commanderTransform;

        private NavMeshAgent templateAgent;
        private CapsuleCollider templateCollider;
        private Rigidbody templateRigidbody;

        private void Awake()
        {
            templateAgent = GetComponent<NavMeshAgent>();
            templateCollider = GetComponent<CapsuleCollider>();
            templateRigidbody = GetComponent<Rigidbody>();

            templateAgent.enabled = false;
            var templateAI = GetComponent<FollowerAI>();
            if (templateAI != null) templateAI.enabled = false;
        }

        public GameObject SpawnFollower(Vector3 spawnPosition)
        {
            if (monsterDefinition == null)
            {
                Debug.LogError("❌ FollowerSpawner에 MonsterDefinition(MD)이 연결되지 않았습니다!");
                return null;
            }

            // MR 세트가 스포너에 할당되지 않은 경우 MD에 등록된 RuntimeAssetSet 프로퍼티 이용
            MonsterRuntimeAssetSet targetRuntimeSet = runtimeAssetSet != null ? runtimeAssetSet : monsterDefinition.RuntimeAssetSet;

            // 1. Root 게임오브젝트 생성 (public 프로퍼티 MonsterId 사용)
            GameObject instance = new GameObject($"Follower_{monsterDefinition.MonsterId}");
            instance.transform.position = spawnPosition;
            instance.transform.rotation = Quaternion.identity;

            // 2. 비주얼 어댑터 생성
            SetupVisual(instance, targetRuntimeSet);

            MonsterBodyProfile bodyProfile = targetRuntimeSet != null ? targetRuntimeSet.BodyProfile : null;

            // 3. NavMeshAgent 세팅
            NavMeshAgent newAgent = instance.AddComponent<NavMeshAgent>();
            newAgent.enabled = false;
            CopyNavMeshAgentSettings(templateAgent, newAgent);

            if (bodyProfile != null)
            {
                newAgent.radius = bodyProfile.BodyRadius;
                newAgent.height = bodyProfile.BodyHeight;
            }

            // public 프로퍼티 MoveSpeed 적용
            newAgent.speed = monsterDefinition.MoveSpeed;

            // 4. 콜라이더 세팅
            CapsuleCollider newCollider = instance.AddComponent<CapsuleCollider>();
            if (bodyProfile != null)
            {
                newCollider.radius = bodyProfile.BodyRadius;
                newCollider.height = bodyProfile.BodyHeight;
                newCollider.center = new Vector3(0f, bodyProfile.BodyHeight * 0.5f + bodyProfile.GroundOffset, 0f);
            }
            else
            {
                newCollider.radius = templateCollider.radius;
                newCollider.height = templateCollider.height;
                newCollider.center = templateCollider.center;
            }

            // 5. 리지드바디 세팅
            Rigidbody newRigidbody = instance.AddComponent<Rigidbody>();
            newRigidbody.isKinematic = templateRigidbody.isKinematic;

            // 6. NavMesh 위치 보정 및 활성화
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
            {
                newAgent.enabled = true;
                newAgent.Warp(hit.position);
            }

            // 7. FollowerAI 세팅 및 SO 데이터 전달
            FollowerAI newAI = instance.AddComponent<FollowerAI>();
            if (commanderTransform != null)
            {
                newAI.Initialize(commanderTransform, monsterDefinition, targetRuntimeSet);
            }
            else
            {
                Debug.LogError("❌ FollowerSpawner에 commanderTransform이 연결되지 않았습니다!");
            }

            return instance;
        }

        private void SetupVisual(GameObject parent, MonsterRuntimeAssetSet targetSet)
        {
            if (targetSet == null) return;

            GameObject visualObj = null;

            if (targetSet.VisualAdapterPrefab != null)
            {
                visualObj = Instantiate(targetSet.VisualAdapterPrefab, parent.transform);
            }
            else
            {
                visualObj = new GameObject("Visual");
                visualObj.transform.SetParent(parent.transform);
            }

            MonsterBodyProfile bodyProfile = targetSet.BodyProfile;
            if (bodyProfile != null)
            {
                visualObj.transform.localPosition = bodyProfile.VisualLocalPosition + new Vector3(0f, bodyProfile.GroundOffset, 0f);
                visualObj.transform.localRotation = Quaternion.Euler(0f, bodyProfile.FacingYawOffset, 0f);
                visualObj.transform.localScale = bodyProfile.VisualScale;
            }

            Animator animator = visualObj.GetComponentInChildren<Animator>();
            if (animator != null && targetSet.AnimatorController != null)
            {
                animator.runtimeAnimatorController = targetSet.AnimatorController;
            }
        }

        private void CopyNavMeshAgentSettings(NavMeshAgent source, NavMeshAgent destination)
        {
            destination.angularSpeed = source.angularSpeed;
            destination.acceleration = source.acceleration;
            destination.stoppingDistance = source.stoppingDistance;
            destination.obstacleAvoidanceType = source.obstacleAvoidanceType;
            destination.avoidancePriority = source.avoidancePriority;
        }
    }
}