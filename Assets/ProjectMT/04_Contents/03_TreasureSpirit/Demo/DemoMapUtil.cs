using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoMapUtil
    {
        public const string StartMarkerName = "Start_pt";
        public const string PrisonMarkerName = "Prison_pt";
        public const string ChestMarkerName = "Chest_pt";
        public const string GuardMarkerName = "Guard_pt";

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

        public static Transform FindChildByPrefix(Transform parent, string namePrefix)
        {
            if (parent == null || string.IsNullOrEmpty(namePrefix))
            {
                return null;
            }

            Transform[] allTransforms = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i].name.StartsWith(namePrefix))
                {
                    return allTransforms[i];
                }
            }

            return null;
        }

        public static bool IsChestMarkerName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            if (objectName.StartsWith(ChestMarkerName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return objectName.StartsWith("Chest-pt", System.StringComparison.OrdinalIgnoreCase);
        }

        public static List<Transform> CollectChestMarkers(Transform mapRoot)
        {
            List<Transform> markers = new List<Transform>();
            if (mapRoot == null)
            {
                return markers;
            }

            Transform[] allTransforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform current = allTransforms[i];
                if (current != null && IsChestMarkerName(current.name))
                {
                    markers.Add(current);
                }
            }

            return markers;
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

        public static bool IsCorridorRootName(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) &&
                   objectName.StartsWith("Corridor", StringComparison.Ordinal);
        }

        public static List<Transform> CollectCorridorRoots(Transform mapRoot)
        {
            List<Transform> corridors = new List<Transform>();
            if (mapRoot == null)
            {
                return corridors;
            }

            List<Transform> rooms = CollectRoomRoots(mapRoot);
            Transform[] allTransforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform current = allTransforms[i];
                if (current == null || !IsCorridorRootName(current.name))
                {
                    continue;
                }

                if (IsInsideAny(current, rooms) ||
                    IsInsideCorridorParent(current) ||
                    corridors.Contains(current))
                {
                    continue;
                }

                corridors.Add(current);
            }

            return corridors;
        }

        public static string GetCorridorGroupKey(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return objectName;
            }

            string key = objectName;
            int cloneSuffix = key.IndexOf(" (", StringComparison.Ordinal);
            if (cloneSuffix >= 0)
            {
                key = key.Substring(0, cloneSuffix);
            }

            int lastUnderscore = key.LastIndexOf('_');
            if (lastUnderscore > 0 && lastUnderscore < key.Length - 1)
            {
                bool allDigits = true;
                for (int i = lastUnderscore + 1; i < key.Length; i++)
                {
                    if (!char.IsDigit(key[i]))
                    {
                        allDigits = false;
                        break;
                    }
                }

                if (allDigits)
                {
                    key = key.Substring(0, lastUnderscore);
                }
            }

            return key;
        }

        public static List<List<Transform>> CollectCorridorGroups(Transform mapRoot)
        {
            List<List<Transform>> groups = new List<List<Transform>>();
            Dictionary<string, List<Transform>> grouped = new Dictionary<string, List<Transform>>();
            List<Transform> corridors = CollectCorridorRoots(mapRoot);
            for (int i = 0; i < corridors.Count; i++)
            {
                Transform corridor = corridors[i];
                if (corridor == null)
                {
                    continue;
                }

                string key = GetCorridorGroupKey(corridor.name);
                if (!grouped.TryGetValue(key, out List<Transform> members))
                {
                    members = new List<Transform>();
                    grouped.Add(key, members);
                    groups.Add(members);
                }

                members.Add(corridor);
            }

            return groups;
        }

        private static bool IsInsideCorridorParent(Transform target)
        {
            Transform current = target != null ? target.parent : null;
            while (current != null)
            {
                if (IsCorridorRootName(current.name))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsInsideAny(Transform target, List<Transform> ancestors)
        {
            Transform current = target.parent;
            while (current != null)
            {
                if (ancestors.Contains(current))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        public static List<Transform> CollectRoomRoots(Transform mapRoot)
        {
            List<Transform> rooms = new List<Transform>();
            if (mapRoot == null)
            {
                return rooms;
            }

            Transform[] allTransforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform current = allTransforms[i];
                if (current == null || !IsRoomRootName(current.name))
                {
                    continue;
                }

                if (!rooms.Contains(current))
                {
                    rooms.Add(current);
                }
            }

            rooms.Sort(CompareRooms);
            return rooms;
        }

        public static Transform FindStartPoint(Transform mapRoot)
        {
            Transform startMarker = FindDeepChild(mapRoot, StartMarkerName);
            if (startMarker != null)
            {
                return startMarker;
            }

            return FindStartRoom(mapRoot);
        }

        public static Transform FindStartRoom(Transform mapRoot)
        {
            Transform named = FindDeepChild(mapRoot, "StartRoom");
            if (named != null)
            {
                return named;
            }

            List<Transform> rooms = CollectRoomRoots(mapRoot);
            return rooms.Count > 0 ? rooms[0] : FindChildByPrefix(mapRoot, "Room_0");
        }

        public static Transform FindEndRoom(Transform mapRoot)
        {
            Transform named = FindDeepChild(mapRoot, "EndRoom");
            if (named != null)
            {
                return named;
            }

            List<Transform> rooms = CollectRoomRoots(mapRoot);
            if (rooms.Count == 0)
            {
                return null;
            }

            return rooms[rooms.Count - 1];
        }

        private static int CompareRooms(Transform a, Transform b)
        {
            int indexCompare = GetRoomSortIndex(a.name).CompareTo(GetRoomSortIndex(b.name));
            if (indexCompare != 0)
            {
                return indexCompare;
            }

            return string.CompareOrdinal(a.name, b.name);
        }

        private static int GetRoomSortIndex(string roomName)
        {
            if (roomName == "StartRoom")
            {
                return int.MinValue;
            }

            if (roomName == "EndRoom")
            {
                return int.MaxValue;
            }

            if (roomName != null && roomName.StartsWith("Room_"))
            {
                int underscore = roomName.IndexOf('_', 5);
                string numberPart = underscore > 5
                    ? roomName.Substring(5, underscore - 5)
                    : roomName.Substring(5);
                if (int.TryParse(numberPart, out int index))
                {
                    return index;
                }
            }

            return int.MaxValue - 1;
        }
    }
}
