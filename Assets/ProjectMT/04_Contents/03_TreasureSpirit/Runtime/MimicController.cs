using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class MimicController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float bounceSpeed = 10f;
        [SerializeField] private float bounceHeight = 0.2f;

        private NavMeshAgent agent;
        private Transform targetPlayer;
        private Vector3 initialScale;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = moveSpeed;
            initialScale = transform.localScale;
        }

        private void Start()
        {
            // 태그가 Player인 오브젝트 추적
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                targetPlayer = playerObj.transform;
            }
        }

        private void Update()
        {
            if (targetPlayer == null) return;

            agent.SetDestination(targetPlayer.position);

            // 이동 중일 때만 튀는 효과
            if (agent.velocity.magnitude > 0.1f)
            {
                float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
                transform.localScale = initialScale + new Vector3(0, bounce, 0);
            }
        }
    }
}