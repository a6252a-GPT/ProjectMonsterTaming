using System;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillContentBridge : MonoBehaviour, ICommanderSkillContentBridge
    {
        [SerializeField] private CommanderSkillCatalog catalog;
        [SerializeField] private CommanderSkillHudView hud;

        private CommanderSkillRuntime runtime;
        private IGameProgressService activeProgress;

        public CommanderSkillRuntime Runtime => runtime;
        public CommanderSkillHudView Hud => hud;

        public void Configure(
            IGameProgressService progress,
            CombatWorld world,
            Transform castOrigin,
            Func<bool> isInputBlocked,
            Func<float> damageMultiplier = null)
        {
            Shutdown();
            catalog ??= Resources.Load<CommanderSkillCatalog>("CommanderSkills/CommanderSkillCatalog");
            hud ??= GetComponentInChildren<CommanderSkillHudView>(true);
            runtime = GetComponent<CommanderSkillRuntime>();
            if (runtime == null)
            {
                runtime = gameObject.AddComponent<CommanderSkillRuntime>();
            }

            if (catalog == null || hud == null || world == null || castOrigin == null)
            {
                Debug.LogError("Commander skill content bridge references are missing.", this);
                return;
            }

            activeProgress = progress ?? new InMemoryGameProgressService();
            runtime.Configure(activeProgress, catalog, world, castOrigin, isInputBlocked, damageMultiplier);
            hud.gameObject.SetActive(true);
            hud.Configure(activeProgress, catalog, runtime);
        }

        public void Shutdown()
        {
            hud?.Shutdown();
            runtime?.Shutdown();
            activeProgress = null;
            if (hud != null)
            {
                hud.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

#if UNITY_EDITOR
        public void EditorConfigure(CommanderSkillCatalog skillCatalog, CommanderSkillHudView hudView)
        {
            catalog = skillCatalog;
            hud = hudView;
        }
#endif
    }
}
