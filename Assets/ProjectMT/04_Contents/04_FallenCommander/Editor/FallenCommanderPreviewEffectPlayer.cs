using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderPreviewEffectPlayer
    {
        private static readonly Type AudioUtilType =
            typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo PlayPreviewClipMethod =
            AudioUtilType?.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
        private static readonly MethodInfo StopPreviewClipsMethod =
            AudioUtilType?.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        public static bool PlayAudio(AudioClip clip)
        {
            if (clip == null || PlayPreviewClipMethod == null)
            {
                return false;
            }

            PlayPreviewClipMethod.Invoke(null, new object[] { clip, 0, false });
            return true;
        }

        public static void StopAudio()
        {
            StopPreviewClipsMethod?.Invoke(null, null);
        }
    }
}
