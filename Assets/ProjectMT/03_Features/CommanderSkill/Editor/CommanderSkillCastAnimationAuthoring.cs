using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill.Editor
{
    public static class CommanderSkillCastAnimationAuthoring
    {
        private const string ControllerPath =
            "Assets/ProjectMT/05_Art/Animation/Commander/AC_Commander_MainBattle.controller";
        private const string ClipRoot =
            "Assets/ThirdParty/03_애니메이션/DarkSorceress AnimSet/DarkSorcerer_AnimSet/Animations/Humanoid/Inplace";

        [MenuItem("Tools/ProjectMT/Commander Skill/Apply DarkSorceress Cast Animations")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.layers.Length == 0)
                throw new InvalidOperationException("군단장 MainBattle Animator Controller를 찾을 수 없습니다.");

            var clips = Enumerable.Range(1, 10).ToDictionary(index => index, LoadClip);
            var backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ProjectMT 개인파일/Backups",
                "CommanderSkillCastAnimation_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")));
            Directory.CreateDirectory(backup);
            AssetDatabase.ExportPackage(new[] { ControllerPath }, Path.Combine(backup, "BeforeCastAnimation.unitypackage"),
                ExportPackageOptions.Default);

            var stateMachine = controller.layers[0].stateMachine;
            Undo.RecordObject(controller, "Apply commander skill cast animations");
            Undo.RecordObject(stateMachine, "Apply commander skill cast animations");
            for (var attack = 1; attack <= 10; attack++)
            {
                var stateName = CommanderSkillCastAnimationRules.StateName(attack).Replace("Base Layer.", string.Empty);
                var state = stateMachine.states.Select(child => child.state)
                    .FirstOrDefault(candidate => candidate != null && candidate.name == stateName);
                if (state == null)
                    state = stateMachine.AddState(stateName, new Vector3(250f + (attack % 2) * 260f, 180f + attack * 55f));
                state.motion = clips[attack];
                state.speed = CommanderSkillCastAnimationRules.StatePlaybackSpeed;
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
            }

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            Validate(controller, clips);
            Debug.Log("COMMANDER_SKILL_CAST_ANIMATIONS_APPLIED backup=" + backup);
        }

        private static AnimationClip LoadClip(int attack)
        {
            var id = attack.ToString("00");
            var path = $"{ClipRoot}/attack_{id}_inplace.fbx";
            var expected = "attack_" + id;
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__") &&
                    (candidate.name == expected || candidate.name == expected + "_inplace"));
            return clip != null ? clip : throw new InvalidOperationException(path + " AnimationClip이 없습니다.");
        }

        private static void Validate(AnimatorController controller, System.Collections.Generic.IReadOnlyDictionary<int, AnimationClip> clips)
        {
            var states = controller.layers[0].stateMachine.states.Select(child => child.state).ToArray();
            for (var attack = 1; attack <= 10; attack++)
            {
                var name = CommanderSkillCastAnimationRules.StateName(attack).Replace("Base Layer.", string.Empty);
                var state = states.SingleOrDefault(candidate => candidate != null && candidate.name == name);
                if (state == null || state.motion != clips[attack] ||
                    !Mathf.Approximately(state.speed, CommanderSkillCastAnimationRules.StatePlaybackSpeed))
                    throw new InvalidOperationException(name + " 상태 연결 검증에 실패했습니다.");
            }
        }
    }
}
