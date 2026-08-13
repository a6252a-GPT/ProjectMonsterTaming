using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FollowerAI))]
    public class FollowerSpawner : MonoBehaviour
    {
        [Header("스폰할 팔로워 비주얼 프리팹")]
        [SerializeField] private GameObject followerPrefab;
        [SerializeField] private Transform commanderTransform;

        // 빈 오브젝트에 설정된 원본 컴포넌트들
        private NavMeshAgent templateAgent;
        private CapsuleCollider templateCollider;
        private Rigidbody templateRigidbody;
        private FollowerAI templateAI;

        private void Awake()
        {
            // 빈 오브젝트에 세팅된 컴포넌트 데이터 가져오기
            templateAgent = GetComponent<NavMeshAgent>();
            templateCollider = GetComponent<CapsuleCollider>();
            templateRigidbody = GetComponent<Rigidbody>();
            templateAI = GetComponent<FollowerAI>();

            // 스포너 자체의 동작은 비활성화 (템플릿 용도)
            templateAgent.enabled = false;
            templateAI.enabled = false;
        }

        public GameObject SpawnFollower(Vector3 spawnPosition)
        {
            if (followerPrefab == null) return null;

            // 1. 프리팹 생성
            GameObject instance = Instantiate(followerPrefab, spawnPosition, Quaternion.identity);

            // 2. NavMeshAgent 복사 및 '비활성화' 유지
            NavMeshAgent newAgent = instance.AddComponent<NavMeshAgent>();
            newAgent.enabled = false; // ★ 일단 꺼둡니다.
            CopyNavMeshAgentSettings(templateAgent, newAgent);

            // 3. 콜라이더 및 리지드바디 설정
            CapsuleCollider newCollider = instance.AddComponent<CapsuleCollider>();
            newCollider.center = templateCollider.center;
            newCollider.radius = templateCollider.radius;
            newCollider.height = templateCollider.height;

            Rigidbody newRigidbody = instance.AddComponent<Rigidbody>();
            newRigidbody.isKinematic = templateRigidbody.isKinematic;

            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 15.0f, NavMesh.AllAreas))
            {
                newAgent.enabled = true;
                newAgent.Warp(hit.position); // Transform 위치 대입 대신 Agent.Warp 사용
            }

            // 1. FollowerAI 컴포넌트 추가
            FollowerAI newAI = instance.AddComponent<FollowerAI>();

            // 2. 군단장(Commander) Transform 전달 확인
            if (commanderTransform != null)
            {
                newAI.Initialize(commanderTransform);
            }
            else
            {
                Debug.LogError("❌ FollowerSpawner에 commanderTransform이 연결되지 않았습니다!");
            }

            return instance;
        }

        // NavMeshAgent 설정값 복사 헬퍼 함수
        private void CopyNavMeshAgentSettings(NavMeshAgent source, NavMeshAgent destination)
        {
            destination.speed = source.speed;
            destination.angularSpeed = source.angularSpeed;
            destination.acceleration = source.acceleration;
            destination.stoppingDistance = source.stoppingDistance;
            destination.radius = source.radius;
            destination.height = source.height;
            destination.obstacleAvoidanceType = source.obstacleAvoidanceType;
            destination.avoidancePriority = source.avoidancePriority;
        }
    }
}