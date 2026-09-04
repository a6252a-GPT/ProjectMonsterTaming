using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoDoorBinder
    {
        public static void Bind(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            Transform[] doors = CollectDoors(mapRoot);

            for (int i = 0; i < doors.Length; i++)
            {
                Transform doorTransform = doors[i];

                DemoDoor door = doorTransform.GetComponent<DemoDoor>();
                if (door == null)
                {
                    door = doorTransform.gameObject.AddComponent<DemoDoor>();
                }

                door.Configure(doorTransform);
            }
        }

        private static Transform[] CollectDoors(Transform mapRoot)
        {
            Transform[] allTransforms = mapRoot.GetComponentsInChildren<Transform>(true);
            List<Transform> doors = new List<Transform>(allTransforms.Length);

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (IsDungeonDoorName(candidate.name))
                {
                    doors.Add(candidate);
                }
            }

            return doors.ToArray();
        }

        private static bool IsDungeonDoorName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || objectName.Contains("DoorStand"))
            {
                return false;
            }

            return objectName == "NorthDoor" ||
                   objectName == "SouthDoor" ||
                   objectName == "EastDoor" ||
                   objectName == "WestDoor";
        }
    }
}
