using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Definition", fileName = "MonsterDefinition")]
    public sealed class MonsterDefinition : ScriptableObject // 몬스터 한 종류의 고정 전투 데이터
    {
        [SerializeField] private string monsterId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite portrait;
        [SerializeField] private GameObject previewPrefab;
        [SerializeField] private Color visualTint = Color.white; // 임시 3D 모델 표시 색상 배율
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float attackPower = 10f;
        [SerializeField] private float defense;
        [SerializeField] private float attackSpeed = 1f; // 초당 공격 횟수
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private bool ranged;
        [SerializeField] private string runtimeAssetKey; // 정식 실행 자산 조회 키
        [SerializeField] private MonsterRuntimeAssetSet runtimeAssetSet; // 첫 Provider가 해석할 직접 참조

        public string MonsterId => monsterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? monsterId : displayName;
        public bool HasExplicitDisplayName => !string.IsNullOrWhiteSpace(displayName);
        public Sprite Portrait => portrait;
        public GameObject PreviewPrefab => previewPrefab;
        public Color VisualTint => visualTint.a <= 0f ? Color.white : visualTint;
        public float MaxHealth => maxHealth;
        public float AttackPower => attackPower;
        public float Defense => defense;
        public float AttackSpeed => attackSpeed;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public bool Ranged => runtimeAssetSet != null && runtimeAssetSet.CombatProfile != null
            ? runtimeAssetSet.CombatProfile.CombatType == MonsterCombatType.Ranged
            : ranged;
        public MonsterCombatType CombatType => runtimeAssetSet != null && runtimeAssetSet.CombatProfile != null
            ? runtimeAssetSet.CombatProfile.CombatType
            : ranged ? MonsterCombatType.Ranged : MonsterCombatType.Melee;
        public string RuntimeAssetKey => runtimeAssetKey ?? string.Empty;
        public MonsterRuntimeAssetSet RuntimeAssetSet => runtimeAssetSet;
        public bool UsesFormalRuntime => runtimeAssetSet != null;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(monsterId))
            {
                error = $"Monster ID is blank. Asset={name}";
                return false;
            }

            if (maxHealth <= 0f || attackPower < 0f || defense < 0f ||
                attackSpeed <= 0f || moveSpeed < 0f || attackRange <= 0f)
            {
                error = $"Monster stats are invalid. Monster={monsterId}";
                return false;
            }

            if (runtimeAssetSet != null)
            {
                if (string.IsNullOrWhiteSpace(runtimeAssetKey))
                {
                    error = $"Formal Monster Runtime Asset Key is blank. Monster={monsterId}";
                    return false;
                }

                if (!runtimeAssetSet.TryValidate(out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            float health,
            float attack,
            float defenseValue,
            float attacksPerSecond,
            float movementSpeed,
            float range,
            bool isRanged)
        {
            monsterId = id?.Trim();
            maxHealth = health;
            attackPower = attack;
            defense = defenseValue;
            attackSpeed = attacksPerSecond;
            moveSpeed = movementSpeed;
            attackRange = range;
            ranged = isRanged;
        }

        public void EditorConfigurePresentation(string localizedName, Sprite portraitSprite, GameObject modelPrefab)
        {
            displayName = localizedName?.Trim();
            portrait = portraitSprite;
            previewPrefab = modelPrefab;
        }

        public void EditorConfigureVisualTint(Color tint)
        {
            visualTint = new Color(
                Mathf.Max(0f, tint.r),
                Mathf.Max(0f, tint.g),
                Mathf.Max(0f, tint.b),
                1f);
        }

        public void EditorConfigureFormalRuntime(string assetKey, MonsterRuntimeAssetSet assetSet)
        {
            runtimeAssetKey = assetKey?.Trim();
            runtimeAssetSet = assetSet;
        }
#endif
    }
}
