using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoMapUtil
    {
        public static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (parent.name == childName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static List<Transform> CollectMarkers(Transform mapRoot, string markerName)
        {
            List<Transform> markers = new List<Transform>();
            if (mapRoot == null || string.IsNullOrEmpty(markerName))
            {
                return markers;
            }

            Transform[] allTransforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i].name == markerName)
                {
                    markers.Add(allTransforms[i]);
                }
            }

            return markers;
        }

        public static Transform FindRoomRoot(Transform marker)
        {
            Transform current = marker != null ? marker.parent : null;
            while (current != null)
            {
                if (IsRoomRootName(current.name))
                {
                    return current;
                }

                current = current.parent;
            }

            return marker != null ? marker.parent : null;
        }

        public static bool IsRoomRootName(string roomName)
        {
            return !string.IsNullOrEmpty(roomName) &&
                   (roomName.StartsWith("Room_") || roomName == "StartRoom" || roomName == "EndRoom");
        }
    }
}
