using UnityEngine;

namespace ProjectMT.Contents.GrowthDungeon
{
    public class MazeExitArea : MonoBehaviour
    {
        private MazeGenerator mazeGenerator;
        private bool isClear = false;

        public void Init(MazeGenerator generator)
        {
            mazeGenerator = generator;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isClear) return;

            DungeonStarterController starter =
                other.GetComponent<DungeonStarterController>();

            if (starter == null)
                return;

            isClear = true;
            ClearDungeon();
        }

        private void ClearDungeon()
        {
            Debug.Log("🎉 던전 탈출!");

            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            if (mazeGenerator != null)
            {
                mazeGenerator.ClearMaze();
            }
        }
    }
}