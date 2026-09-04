using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Active Energy Config", fileName = "MonsterActiveEnergyConfig")]
    public sealed class MonsterActiveEnergyConfig : ScriptableObject // 모든 몬스터가 공유하는 기력 획득 규칙
    {
        public const float StageStartEnergy = 500f;
        public const float FallbackEnergyPerSecond = 20f;
        public const float FallbackEnergyPerBasicAttack = 60f;
        private const string ResourcesPath = "MonsterActiveEnergyConfig";
        [SerializeField, Min(0f)] private float energyPerSecond = FallbackEnergyPerSecond;
        [SerializeField, Min(0f)] private float energyPerBasicAttack = FallbackEnergyPerBasicAttack;
        private static MonsterActiveEnergyConfig cached;

        public float EnergyPerSecond => Mathf.Max(0f, energyPerSecond);
        public float EnergyPerBasicAttack => Mathf.Max(0f, energyPerBasicAttack);
        public static float SharedEnergyPerSecond => Current == null ? FallbackEnergyPerSecond : Current.EnergyPerSecond;
        public static float SharedEnergyPerBasicAttack => Current == null ? FallbackEnergyPerBasicAttack : Current.EnergyPerBasicAttack;
        public static MonsterActiveEnergyConfig Current => cached != null
            ? cached
            : cached = Resources.Load<MonsterActiveEnergyConfig>(ResourcesPath);

        public bool TryValidate(out string error)
        {
            if (float.IsNaN(energyPerSecond) || float.IsInfinity(energyPerSecond) || energyPerSecond < 0f ||
                float.IsNaN(energyPerBasicAttack) || float.IsInfinity(energyPerBasicAttack) || energyPerBasicAttack < 0f)
            {
                error = "공용 액티브 기력 획득량이 유효하지 않습니다.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }

#if UNITY_EDITOR
        public void EditorConfigure(float perSecond, float perBasicAttack)
        {
            energyPerSecond = Mathf.Max(0f, perSecond);
            energyPerBasicAttack = Mathf.Max(0f, perBasicAttack);
            cached = this;
        }
#endif
    }
}
