using System;
using System.IO;
using System.Security.Cryptography;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed class MonsterMakerV2State : IDisposable // V2 작업 사본과 영구 원본의 저장 경계
    {
        private MonsterMakerDraft initialSnapshot;
        private string sourceAssetPath = string.Empty;
        private string sourceMonsterId = string.Empty;
        private string sourceFingerprint = string.Empty;
        private string savedWorkingJson = string.Empty;
        private bool forceDirtyUntilSave;

        public MonsterMakerDraft SourceDraft { get; private set; }
        public MonsterMakerDraft WorkingDraft { get; private set; }
        public SerializedObject SerializedDraft { get; private set; }
        public MonsterMakerValidationReport Validation { get; private set; }
        public MonsterMakerWriteResult LastWriteResult { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsNew => SourceDraft == null;

        public void CreateNew()
        {
            ReleaseWorkingObjects();
            WorkingDraft = ScriptableObject.CreateInstance<MonsterMakerDraft>();
            WorkingDraft.name = "Draft_monster [V2 Working Copy]";
            WorkingDraft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            FinishLoad();
        }

        public void Load(MonsterMakerDraft source)
        {
            if (source == null)
            {
                CreateNew();
                return;
            }

            ReleaseWorkingObjects();
            SourceDraft = source;
            WorkingDraft = UnityEngine.Object.Instantiate(source);
            WorkingDraft.name = source.name + " [V2 Working Copy]";
            WorkingDraft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            WorkingDraft.EditorSyncActiveAttackAuthoring();
            WorkingDraft.EditorSyncActiveEffectAuthoring();
            CaptureSourceIdentity();
            FinishLoad();
        }

        public void MarkChanged()
        {
            SerializedDraft?.ApplyModifiedProperties();
            Validation = null;
            LastWriteResult = null;
            IsDirty = forceDirtyUntilSave || !string.Equals(
                savedWorkingJson,
                CaptureWorkingJson(),
                StringComparison.Ordinal);
        }

        public void SynchronizeActiveAttackAuthoring()
        {
            if (WorkingDraft == null)
            {
                return;
            }

            SerializedDraft?.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(
                WorkingDraft,
                "Monster Maker V2 · 액티브 Step 구조 동기화");
            WorkingDraft.EditorSyncActiveAttackAuthoring();
            SerializedDraft = new SerializedObject(WorkingDraft);
            MarkChanged();
        }

        public void RefreshAfterUndo()
        {
            if (WorkingDraft == null)
            {
                return;
            }

            SerializedDraft = new SerializedObject(WorkingDraft);
            MarkChanged();
        }

        public void RestoreRecovery(string workingJson, bool preserveDirty)
        {
            if (WorkingDraft == null || string.IsNullOrWhiteSpace(workingJson))
            {
                return;
            }

            EditorJsonUtility.FromJsonOverwrite(workingJson, WorkingDraft);
            if (!workingJson.Contains("skillUsageSchemaVersion", StringComparison.Ordinal))
            {
                WorkingDraft.EditorRestoreLegacySkillUsage();
            }
            else
            {
                WorkingDraft.EditorEnsureSplitSkillUsage();
            }
            RestoreInvalidBasicAttackBindingsFromSource();
            SerializedDraft = new SerializedObject(WorkingDraft);
            Validation = null;
            LastWriteResult = null;
            forceDirtyUntilSave = preserveDirty;
            IsDirty = forceDirtyUntilSave || !string.Equals(
                savedWorkingJson,
                CaptureWorkingJson(),
                StringComparison.Ordinal);
        }

        private void RestoreInvalidBasicAttackBindingsFromSource()
        {
            if (SourceDraft == null || WorkingDraft == null)
            {
                return;
            }

            foreach (var workingBinding in WorkingDraft.BasicAttackVfxBindings)
            {
                if (workingBinding == null || workingBinding.TryValidate(out _))
                {
                    continue;
                }

                MonsterBasicAttackVfxBinding sourceBinding = null;
                foreach (var candidate in SourceDraft.BasicAttackVfxBindings)
                {
                    if (candidate == null || !candidate.TryValidate(out _) ||
                        !string.Equals(candidate.AttackId, workingBinding.AttackId,
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(candidate.SlotId, workingBinding.SlotId,
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(candidate.MotionId, workingBinding.MotionId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    sourceBinding = candidate;
                }

                if (sourceBinding == null)
                {
                    continue;
                }

                workingBinding.EditorConfigure(
                    sourceBinding.AttackId,
                    sourceBinding.SlotId,
                    sourceBinding.MotionId,
                    sourceBinding.State,
                    sourceBinding.Prefab,
                    sourceBinding.Lifetime,
                    sourceBinding.LocalPosition,
                    sourceBinding.LocalRotation.eulerAngles,
                    sourceBinding.Scale,
                    sourceBinding.PlaybackOffset,
                    sourceBinding.Sound,
                    sourceBinding.Sfx,
                    sourceBinding.SfxState,
                    sourceBinding.SoundVolume,
                    sourceBinding.EventTimingOffset,
                    sourceBinding.PlaybackSpeed);
            }
        }

        public void RestoreInitial()
        {
            if (WorkingDraft == null || initialSnapshot == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(WorkingDraft, "Monster Maker V2 · 초기 상태 복원");
            var name = WorkingDraft.name;
            var flags = WorkingDraft.hideFlags;
            EditorUtility.CopySerialized(initialSnapshot, WorkingDraft);
            WorkingDraft.name = name;
            WorkingDraft.hideFlags = flags;
            SerializedDraft = new SerializedObject(WorkingDraft);
            MarkChanged();
        }

        public MonsterMakerValidationReport Validate()
        {
            SerializedDraft?.ApplyModifiedProperties();
            Validation = MonsterMakerValidator.Validate(
                WorkingDraft,
                SourceDraft ?? WorkingDraft);
            return Validation;
        }

        public bool TrySave(out string error)
        {
            error = null;
            try
            {
                if (WorkingDraft == null)
                {
                    throw new InvalidOperationException("저장할 V2 작업 사본이 없습니다.");
                }

                SerializedDraft?.ApplyModifiedProperties();
                WorkingDraft.EditorSyncActiveAttackAuthoring();
                WorkingDraft.EditorSyncActiveEffectAuthoring();
                if (SourceDraft == null)
                {
                    CreatePersistentDraft();
                }
                else
                {
                    ValidateSourceOwnership();
                    Undo.RegisterCompleteObjectUndo(SourceDraft, "Monster Maker V2 · 제작 원본 저장");
                    var sourceName = SourceDraft.name;
                    var sourceFlags = SourceDraft.hideFlags;
                    EditorUtility.CopySerialized(WorkingDraft, SourceDraft);
                    SourceDraft.name = sourceName;
                    SourceDraft.hideFlags = sourceFlags;
                    EditorUtility.SetDirty(SourceDraft);
                    AssetDatabase.SaveAssetIfDirty(SourceDraft);
                }

                CaptureSourceIdentity();
                savedWorkingJson = CaptureWorkingJson();
                forceDirtyUntilSave = false;
                IsDirty = false;
                Validation = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryBuildAndRegister(out MonsterMakerWriteResult result, out string error)
        {
            result = null;
            error = null;
            var report = Validate();
            if (report.HasErrors)
            {
                error = "입력 오류를 먼저 수정해야 합니다.";
                return false;
            }

            if (!TrySave(out error))
            {
                return false;
            }

            try
            {
                result = MonsterMakerAssetWriter.BuildAndRegister(SourceDraft);
                LastWriteResult = result;
                CaptureSourceIdentity();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void DiscardChanges()
        {
            if (SourceDraft == null)
            {
                CreateNew();
            }
            else
            {
                Load(SourceDraft);
            }
        }

        public void Dispose()
        {
            ReleaseWorkingObjects();
        }

        private void FinishLoad()
        {
            initialSnapshot = UnityEngine.Object.Instantiate(WorkingDraft);
            initialSnapshot.name = WorkingDraft.name + " [Initial]";
            initialSnapshot.hideFlags = HideFlags.HideAndDontSave;
            SerializedDraft = new SerializedObject(WorkingDraft);
            savedWorkingJson = CaptureWorkingJson();
            forceDirtyUntilSave = false;
            IsDirty = false;
            Validation = null;
            LastWriteResult = null;
        }

        private void CreatePersistentDraft()
        {
            if (!MonsterMakerValidator.UsesSafeId(WorkingDraft.MonsterId))
            {
                throw new InvalidOperationException(
                    "처음 저장하려면 영문·숫자·밑줄·하이픈으로 된 Monster ID가 필요합니다.");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(
                MonsterMakerAssetWriter.MonsterCatalogPath);
            if (catalog != null && catalog.TryGet(WorkingDraft.MonsterId, out _))
            {
                throw new InvalidOperationException(
                    "게임 Catalog에 같은 ID가 있습니다. 왼쪽 목록에서 기존 제작 원본을 여세요.");
            }

            EnsureDraftFolder();
            var path = MonsterMakerAssetWriter.BuildDraftPath(WorkingDraft.MonsterId);
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                throw new InvalidOperationException(
                    $"같은 ID의 제작 원본이 이미 있습니다.\n{path}");
            }

            SourceDraft = UnityEngine.Object.Instantiate(WorkingDraft);
            SourceDraft.name = "Draft_" + WorkingDraft.MonsterId;
            SourceDraft.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(SourceDraft, path);
            AssetDatabase.SaveAssetIfDirty(SourceDraft);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private void ValidateSourceOwnership()
        {
            var currentPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(SourceDraft));
            var expectedPath = NormalizeAssetPath(
                MonsterMakerAssetWriter.BuildDraftPath(WorkingDraft.MonsterId));
            if (!string.Equals(currentPath, sourceAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "열었던 제작 원본의 Asset 경로가 바뀌었습니다. 다시 열어 주세요.");
            }

            if (!string.Equals(sourceMonsterId, WorkingDraft.MonsterId, StringComparison.Ordinal) ||
                !string.Equals(currentPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "저장된 제작 원본의 Monster ID와 파일명은 변경할 수 없습니다.");
            }

            var currentFingerprint = ComputeFileFingerprint(currentPath);
            if (string.IsNullOrWhiteSpace(currentFingerprint) ||
                !string.Equals(currentFingerprint, sourceFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "제작 원본이 창 밖에서 변경되었습니다. 다시 열어 최신 원본으로 시작하세요.");
            }
        }

        private void CaptureSourceIdentity()
        {
            if (SourceDraft == null)
            {
                sourceAssetPath = string.Empty;
                sourceMonsterId = string.Empty;
                sourceFingerprint = string.Empty;
                return;
            }

            sourceAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(SourceDraft));
            sourceMonsterId = SourceDraft.MonsterId;
            sourceFingerprint = ComputeFileFingerprint(sourceAssetPath);
        }

        private string CaptureWorkingJson()
        {
            return WorkingDraft == null ? string.Empty : EditorJsonUtility.ToJson(WorkingDraft);
        }

        private void ReleaseWorkingObjects()
        {
            if (initialSnapshot != null)
            {
                UnityEngine.Object.DestroyImmediate(initialSnapshot);
                initialSnapshot = null;
            }

            if (WorkingDraft != null)
            {
                UnityEngine.Object.DestroyImmediate(WorkingDraft);
                WorkingDraft = null;
            }

            SourceDraft = null;
            SerializedDraft = null;
            Validation = null;
            LastWriteResult = null;
            IsDirty = false;
            sourceAssetPath = string.Empty;
            sourceMonsterId = string.Empty;
            sourceFingerprint = string.Empty;
            savedWorkingJson = string.Empty;
            forceDirtyUntilSave = false;
        }

        private static void EnsureDraftFolder()
        {
            var parts = MonsterMakerAssetWriter.DraftRoot.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string ComputeFileFingerprint(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return string.Empty;
            }

            var fullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(fullPath))
            {
                return string.Empty;
            }

            using var stream = File.OpenRead(fullPath);
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
