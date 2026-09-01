using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class FogOfWarManager : MonoBehaviour
    {
        public static FogOfWarManager Instance { get; private set; }

        private readonly List<FogArea> areas = new List<FogArea>();
        private readonly HashSet<FogArea> occupied = new HashSet<FogArea>();
        private FogArea currentArea;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Register(FogArea area)
        {
            if (area != null && !areas.Contains(area))
            {
                areas.Add(area);
            }
        }

        public void NotifyEntered(FogArea area)
        {
            if (area == null)
            {
                return;
            }

            occupied.Add(area);
            currentArea = PreferCurrent();
            RefreshStates();
        }

        public void NotifyExited(FogArea area)
        {
            if (area == null)
            {
                return;
            }

            occupied.Remove(area);
            currentArea = PreferCurrent();
            RefreshStates();
        }

        public void RevealContaining(Vector3 worldPosition)
        {
            FogArea matchRoom = null;
            float roomVolume = float.MaxValue;
            FogArea matchCorridor = null;
            float corridorVolume = float.MaxValue;

            for (int i = 0; i < areas.Count; i++)
            {
                FogArea area = areas[i];
                if (area == null || !area.Contains(worldPosition))
                {
                    continue;
                }

                float volume = area.WorldBounds.size.x * area.WorldBounds.size.z;
                if (!area.IsCorridor)
                {
                    if (volume < roomVolume)
                    {
                        roomVolume = volume;
                        matchRoom = area;
                    }

                    continue;
                }

                if (volume < corridorVolume)
                {
                    corridorVolume = volume;
                    matchCorridor = area;
                }
            }

            FogArea match = matchRoom != null ? matchRoom : matchCorridor;
            if (match != null)
            {
                occupied.Add(match);
                currentArea = PreferCurrent();
                RefreshStates();
            }
        }

        public void Clear()
        {
            areas.Clear();
            occupied.Clear();
            currentArea = null;
        }

        private FogArea PreferCurrent()
        {
            FogArea roomPick = null;
            float roomVolume = float.MaxValue;
            FogArea corridorPick = null;
            float corridorVolume = float.MaxValue;

            foreach (FogArea area in occupied)
            {
                if (area == null)
                {
                    continue;
                }

                float volume = area.WorldBounds.size.x * area.WorldBounds.size.z;
                if (!area.IsCorridor)
                {
                    if (volume < roomVolume)
                    {
                        roomVolume = volume;
                        roomPick = area;
                    }

                    continue;
                }

                if (volume < corridorVolume)
                {
                    corridorVolume = volume;
                    corridorPick = area;
                }
            }

            return roomPick != null ? roomPick : corridorPick;
        }

        private void RefreshStates()
        {
            for (int i = 0; i < areas.Count; i++)
            {
                FogArea area = areas[i];
                if (area == null)
                {
                    continue;
                }

                if (area == currentArea)
                {
                    area.SetState(FogAreaState.Visible);
                }
                else if (area.HasBeenVisited)
                {
                    area.SetState(FogAreaState.Explored);
                }
            }
        }
    }
}
