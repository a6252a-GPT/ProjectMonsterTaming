using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 베이크 맵의 Traphome_Mid마다 톱날 함정을 준비합니다.
    /// Sawblade가 집 밖(형제)에 있으면 자식으로 옮기고, 없으면 생성합니다.
    /// </summary>
    internal static class DemoSawbladeTrapInstaller
    {
        public static void Install(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            Transform[] transforms = mapRoot.GetComponentsInChildren<Transform>(true);
            int installedCount = 0;
            int homeCount = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform home = transforms[i];
                if (home == null || !IsTrapHome(home))
                {
                    continue;
                }

                homeCount++;

                Transform blade = FindSawbladeUnder(home);
                bool createdBlade = false;
                if (blade == null)
                {
                    blade = FindNearbyUnassignedSawblade(home);
                    if (blade != null)
                    {
                        blade.SetParent(home, true);
                    }
                }

                if (blade == null)
                {
                    blade = CreateSawblade(home);
                    createdBlade = true;
                }

                DemoSawbladeTrap trap = blade.GetComponent<DemoSawbladeTrap>();
                if (trap == null)
                {
                    trap = blade.gameObject.AddComponent<DemoSawbladeTrap>();
                }

                trap.Initialize(home, createdBlade);
                installedCount++;
            }

            if (homeCount == 0)
            {
                return;
            }

            Debug.Log($"[DemoSawbladeTrapInstaller] 톱날 함정 {installedCount}개 설치 (집 {homeCount}개, {mapRoot.name})");
        }

        private static bool IsTrapHome(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            if (IsTrapHomeName(target.name))
            {
                return true;
            }

            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            return meshFilter != null &&
                   meshFilter.sharedMesh != null &&
                   IsTrapHomeName(meshFilter.sharedMesh.name);
        }

        private static bool IsTrapHomeName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            if (objectName.StartsWith("Traphome_Mid", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string compact = objectName.Replace(" ", string.Empty).Replace("_", string.Empty);
            return compact.StartsWith("TraphomeMid", System.StringComparison.OrdinalIgnoreCase) ||
                   compact.IndexOf("Traphome", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   compact.IndexOf("TrapHome", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSawbladeName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string compact = objectName.Replace(" ", string.Empty).Replace("_", string.Empty);
            return compact.IndexOf("Sawblade", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   compact.Equals("Saw", System.StringComparison.OrdinalIgnoreCase);
        }

        private static Transform FindSawbladeUnder(Transform home)
        {
            Transform[] children = home.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == home)
                {
                    continue;
                }

                if (IsSawbladeName(child.name))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindNearbyUnassignedSawblade(Transform home)
        {
            Transform searchRoot = home.parent != null ? home.parent : home;
            Transform[] candidates = searchRoot.GetComponentsInChildren<Transform>(true);
            Transform closest = null;
            float closestDistance = 8f;

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform candidate = candidates[i];
                if (candidate == null || candidate == home || !IsSawbladeName(candidate.name))
                {
                    continue;
                }

                if (IsUnderTrapHome(candidate, home))
                {
                    continue;
                }

                float distance = Vector3.Distance(home.position, candidate.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }

            return closest;
        }

        private static bool IsUnderTrapHome(Transform blade, Transform currentHome)
        {
            Transform parent = blade.parent;
            while (parent != null)
            {
                if (parent == currentHome)
                {
                    return false;
                }

                if (IsTrapHome(parent))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static Transform CreateSawblade(Transform home)
        {
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blade.name = "Sawblade";
            blade.transform.SetParent(home, false);
            blade.transform.localPosition = Vector3.zero;
            blade.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            blade.transform.localScale = new Vector3(0.85f, 0.06f, 0.85f);

            Collider collider = blade.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            MeshRenderer renderer = blade.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.72f, 0.74f, 0.78f);
            }

            return blade.transform;
        }
    }
}
