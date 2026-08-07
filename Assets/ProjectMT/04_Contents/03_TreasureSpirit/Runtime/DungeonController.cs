using UnityEngine;

namespace ProjectMT.Contents.GrowthDungeon
{
    public class DungeonController : MonoBehaviour
    {
        [SerializeField] private MazeGenerator mazeGenerator;

        private void Start()
        {
            // 테스트용: 게임 시작 시 스스로 초기화
            Initialize();
        }

        public void Initialize()
        {
            if (mazeGenerator != null)
            {
                mazeGenerator.GenerateMaze();
            }
            else
            {
                Debug.LogError("MazeGenerator가 연결되어 있지 않습니다!");
            }
        }
    }
}