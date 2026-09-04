using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// Normal3는 하수도 up 방향, Hard1은 Sewer_Square 월드 X축으로 화살을 발사합니다.
    /// </summary>
    internal static class DemoArrowTrapInstaller
    {
        private const float DefaultTravel = 36f;
        private const float MinTravel = 16f;
        private const float MaxTravel = 48f;

        public static void Install(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            bool isNormal3 = HasMapTag(mapRoot, "Normal3");
            bool isHard1 = HasMapTag(mapRoot, "Hard1");
            if (!isNormal3 && !isHard1)
            {
                return;
            }

            Transform[] transforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || !IsSewerSquare(current.name))
                {
                    continue;
                }

                if (isHard1)
                {
                    InstallHard1XAxis(current);
                }
                else
                {
                    InstallOnSewerSquare(current);
                }
            }
        }

        private static bool HasMapTag(Transform mapRoot, string tag)
        {
            return mapRoot.name.IndexOf(tag, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSewerSquare(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) &&
                   (objectName == "Sewer_Square" || objectName.StartsWith("Sewer_Square "));
        }

        private static int InstallHard1XAxis(Transform sewer)
        {
            Vector3 axis = Vector3.right;
            int installed = 0;
            installed += TryInstallDirection(sewer, axis);
            installed += TryInstallDirection(sewer, -axis);
            return installed;
        }

        private static int TryInstallDirection(Transform sewer, Vector3 fireDir)
        {
            Vector3 origin = sewer.position + fireDir * 0.25f;
            origin.y = Mathf.Max(1.05f, sewer.position.y);
            float travel = MeasureTravel(sewer, origin, fireDir);
            if (travel < 6f)
            {
                return 0;
            }

            CreateEmitter(sewer, origin, fireDir, travel);
            return 1;
        }

        private static int InstallOnSewerSquare(Transform sewer)
        {
            Vector3 fireDir = sewer.up;
            fireDir.y = 0f;
            if (fireDir.sqrMagnitude < 0.01f)
            {
                fireDir = Vector3.forward;
            }

            fireDir.Normalize();

            Vector3 origin = sewer.position + fireDir * 0.25f;
            origin.y = Mathf.Max(1.05f, sewer.position.y);
            float travel = MeasureTravel(sewer, origin, fireDir);
            CreateEmitter(sewer, origin, fireDir, travel);
            return 1;
        }

        private static void CreateEmitter(Transform sewer, Vector3 origin, Vector3 fireDir, float travel)
        {
            GameObject emitter = new GameObject("SewerArrowTrap");
            emitter.transform.SetParent(sewer.parent != null ? sewer.parent : sewer, false);
            emitter.transform.position = origin;
            emitter.transform.rotation = Quaternion.LookRotation(fireDir, Vector3.up);

            DemoArrowTrap trap = emitter.AddComponent<DemoArrowTrap>();
            trap.InitializeBurst(fireDir, travel);
        }

        private static float MeasureTravel(Transform sewer, Vector3 emitterPosition, Vector3 fireDir)
        {
            Vector3 rayOrigin = emitterPosition + fireDir * 1.2f;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, fireDir, MaxTravel, ~0, QueryTriggerInteraction.Ignore);
            float nearest = DefaultTravel;
            bool foundWall = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                Transform hitTransform = hit.collider.transform;
                if (hitTransform == sewer || hitTransform.IsChildOf(sewer))
                {
                    continue;
                }

                if (hit.normal.y > 0.55f)
                {
                    continue;
                }

                float distanceFromEmitter = Vector3.Distance(emitterPosition, hit.point);
                if (!foundWall || distanceFromEmitter < nearest)
                {
                    nearest = distanceFromEmitter;
                    foundWall = true;
                }
            }

            return foundWall
                ? Mathf.Clamp(nearest - 0.15f, MinTravel * 0.5f, MaxTravel)
                : DefaultTravel;
        }
    }
}
