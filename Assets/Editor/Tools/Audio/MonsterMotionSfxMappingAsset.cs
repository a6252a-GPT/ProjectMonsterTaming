using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용자가 선택한 모션-SFX 조합을 저장하는 Editor 전용 데이터다.
/// </summary>
[CreateAssetMenu(menuName = "JC Tool/Animation/Monster Motion SFX Mappings", fileName = "MonsterMotionSfxMappings")]
public sealed class MonsterMotionSfxMappingAsset : ScriptableObject
{
    public List<MonsterMotionSfxMapping> Mappings = new List<MonsterMotionSfxMapping>();
}

[Serializable]
public sealed class MonsterMotionSfxMapping
{
    public string MotionKey;
    public string MonsterName;
    public string MotionName;
    public AnimationClip Motion;
    public GameObject MonsterPrefab;
    public AudioClip Sfx;
    public float SfxDelaySeconds;
    public float Volume = 1f;
}
