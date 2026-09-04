using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProjectMT.Core.SaveIO;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.OfflineReward;
using ProjectMT.Features.Settings;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Debugging;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class AppRootHost : MonoBehaviour // 앱 전역 서비스 조립
    {
        [SerializeField] private ProjectConfig projectConfig; // 시작 설정 모음
        [SerializeField] private SceneLoader sceneLoader; // 정식 씬 전환 담당
        [SerializeField] private RewardAcquirePresenter rewardPresenter; // 저장 확정 보상 연출
        [SerializeField] private ContentFinishFeedbackPresenter finishFeedbackPresenter; // 콘텐츠 저장 재시도 표시
        [SerializeField] private ContentResultOverlayPresenter resultOverlayPresenter; // 저장 확정 공통 결과창
        [SerializeField] private OfflineRewardPopupPresenter offlineRewardPresenter; // 접속·복귀 방치 정산창
        [SerializeField] private SceneLoadingOverlayPresenter sceneLoadingOverlay; // 씬 전환 공통 로딩
        [SerializeField] private SleepModeController sleepModeController; // 전역 절전 화면
        [SerializeField] private GlobalAudioController globalAudioController; // Mixer·BGM 전역 소유

        private GameDataService gameDataService; // 진행 데이터 관리자
        private ContentFlow contentFlow; // 콘텐츠 실행 흐름
        private GrowthDungeonSweepService growthDungeonSweepService; // Runtime 없는 1회 소탕
        private BattlePartySnapshotBuilder partyBuilder; // 저장 편성 해석기
        private CommanderGrowthConfig commanderGrowthConfig; // 군단장 경험치·레벨 규칙
        private OfflineRewardCoordinator offlineRewardCoordinator; // 종료·복귀 방치 정산 흐름
        private bool retryingOfflineSettlement;
        private DebugPanelController debugPanel; // 개발 빌드 전용 도구 패널
        private bool initialized; // 중복 초기화 방지
        private SceneId readySceneId; // 마지막 초기화 완료 씬

        public static AppRootHost Instance { get; private set; } // 전역 AppRoot 한 개
        public bool IsInitialized => initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 중복 AppRoot 제거
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 뒤에도 유지
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task InitializeAsync()
        {
            if (initialized)
            {
                return;
            }

            if (projectConfig == null || projectConfig.SceneCatalog == null ||
                projectConfig.ContentCatalog == null || projectConfig.MonsterCatalog == null ||
                projectConfig.ItemCatalog == null)
            {
                throw new InvalidOperationException("ProjectConfig and its catalogs must be assigned.");
            }

            if (!projectConfig.ItemCatalog.TryValidateRuntimeCatalog(out var itemCatalogError))
            {
                throw new InvalidOperationException($"ItemCatalog is invalid. {itemCatalogError}");
            }

            ItemCatalogHub.Bind(projectConfig.ItemCatalog); // Bootstrap↔Features 순환 참조 없이 카탈로그 공유
            RewardPresentationHub.Bind(rewardPresenter); // Bootstrap↔Features 순환 참조 없이 보상 연출 공유

            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<SceneLoader>();
            }

            if (sceneLoader == null)
            {
                throw new InvalidOperationException("SceneLoader is missing from AppRoot.");
            }

            if (finishFeedbackPresenter == null)
            {
                throw new InvalidOperationException("ContentFinishFeedbackPresenter is missing from AppRoot.");
            }

            if (resultOverlayPresenter == null)
            {
                resultOverlayPresenter = GetComponentInChildren<ContentResultOverlayPresenter>(true);
            }

            if (resultOverlayPresenter == null)
            {
                throw new InvalidOperationException("ContentResultOverlayPresenter is missing from AppRoot.");
            }

            if (offlineRewardPresenter == null)
            {
                offlineRewardPresenter = GetComponentInChildren<OfflineRewardPopupPresenter>(true);
            }

            if (offlineRewardPresenter == null)
            {
                throw new InvalidOperationException("OfflineRewardPopupPresenter is missing from AppRoot.");
            }

            var offlineRewardError = "Config is missing.";
            if (projectConfig.OfflineRewardConfig == null ||
                !projectConfig.OfflineRewardConfig.TryValidate(out offlineRewardError))
            {
                throw new InvalidOperationException($"OfflineRewardConfig is invalid. {offlineRewardError}");
            }

            var savePath = Path.Combine(Application.persistentDataPath, "ProjectMT_seed_save.json"); // 시드 저장 위치
            AccountSessionStore.Prepare(File.Exists(savePath)); // 기존 저장은 최초 1회 게스트 자동 로그인 승계
            var saveService = new SaveService(new AtomicFileStore(), savePath);
            commanderGrowthConfig = projectConfig.CommanderGrowthConfig;
            if (commanderGrowthConfig == null || !commanderGrowthConfig.TryValidate(out _))
            {
                commanderGrowthConfig = CommanderGrowthConfig.RuntimeDefault; // 미할당·손상 설정 안전 복구
            }

            var commanderSkillBalanceConfig = projectConfig.CommanderSkillBalanceConfig;
            if (commanderSkillBalanceConfig == null || !commanderSkillBalanceConfig.TryValidate(out _))
            {
                commanderSkillBalanceConfig = CommanderSkillBalanceConfig.RuntimeDefault; // SO 누락 시 현재 2종 시드 유지
            }

            var commanderSkillSummonConfig = projectConfig.CommanderSkillSummonConfig;
            if (commanderSkillSummonConfig == null ||
                !commanderSkillSummonConfig.TryValidate(commanderSkillBalanceConfig, out _))
            {
                commanderSkillSummonConfig = CommanderSkillSummonConfig.RuntimeDefault; // 전용 소환 시드 복구
            }

            gameDataService = new GameDataService(
                saveService,
                commanderGrowthConfig,
                projectConfig.ItemCatalog,
                projectConfig.EquipmentBalanceConfig,
                commanderSkillBalanceConfig,
                commanderSkillSummonConfig);
            await gameDataService.LoadAsync(); // 씬 초기화 전 저장 로드
            CombatWorld.ConfigureSharedStatRules(
                projectConfig.CombatStatConfig ?? CombatStatConfig.RuntimeDefault);
            CombatImpactTuning.Configure(projectConfig.CombatTuningConfig);
            await RefreshGrowthDungeonKeysAsync(); // 접속 1회 KST 05:00 기준 충전
            await RefreshAttendanceAsync(); // 접속 1회 KST 05:00 기준 출석 갱신
            await CleanupExpiredMailAsync(); // 만료된 미수령 우편 정리
            offlineRewardCoordinator = new OfflineRewardCoordinator(
                gameDataService,
                projectConfig.OfflineRewardConfig,
                null,
                projectConfig.EquipmentBalanceConfig);
            if (!await offlineRewardCoordinator.PrepareOnLoginAsync())
            {
                throw new InvalidOperationException("Offline reward login settlement could not be saved.");
            }

            gameDataService.Changed += RetryBlockedOfflineSettlement;

            partyBuilder = new BattlePartySnapshotBuilder(
                projectConfig.MonsterCatalog,
                projectConfig.MonsterRarityCatalog,
                projectConfig.CombatStatConfig ?? CombatStatConfig.RuntimeDefault);

            sceneLoader.Configure(projectConfig.SceneCatalog);
            contentFlow = new ContentFlow(
                projectConfig.ContentCatalog,
                gameDataService,
                sceneLoader,
                projectConfig.MainBattleSceneId,
                projectConfig.ItemCatalog,
                rewardPresenter,
                finishFeedbackPresenter,
                resultOverlayPresenter);
            contentFlow.HostedRunStarted += HandleHostedRunStarted;
            contentFlow.HostedRunFinished += HandleHostedRunFinished;
            growthDungeonSweepService = new GrowthDungeonSweepService(
                projectConfig.ContentCatalog,
                gameDataService,
                projectConfig.ItemCatalog,
                rewardPresenter,
                finishFeedbackPresenter,
                resultOverlayPresenter);
            sceneLoader.ContextFactory = CreateSceneContext; // 씬별 권한 봉투 생성
            sceneLoadingOverlay ??= GetComponentInChildren<SceneLoadingOverlayPresenter>(true);
            sleepModeController ??= GetComponentInChildren<SleepModeController>(true);
            globalAudioController ??= GetComponentInChildren<GlobalAudioController>(true);
            sceneLoader.SceneLoadStarted += HandleSceneLoadStarted;
            sceneLoader.SceneReady += HandleSceneReady;
            sceneLoader.SceneFailed += HandleSceneFailed;
            AccountRuntimeBridge.LogoutRequested += HandleLogoutRequested;
            AccountRuntimeBridge.DeleteProgressRequested = HandleDeleteProgressRequestedAsync;

            initialized = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CreateDebugPanel();
#endif
            sceneLoader.InitializeCurrentScene(); // 현재 Entry부터 초기화
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void CreateDebugPanel()
        {
            if (debugPanel != null)
            {
                return;
            }

            var prefab = Resources.Load<DebugPanelController>("Debug/PF_DebugPanel");
            if (prefab == null)
            {
                Debug.LogWarning("Debug panel prefab is missing: Resources/Debug/PF_DebugPanel");
                return;
            }

            debugPanel = Instantiate(prefab, transform);
            debugPanel.name = "DebugPanel";
            debugPanel.Configure(
                ResetGameDataForDebugAsync,
                DrawMonsterForDebugAsync,
                AcquireEquipmentForDebugAsync,
                AcquireAllItemsForDebugAsync,
                SendRandomMailForDebugAsync);
        }

        private async Task<bool> ResetGameDataForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return false; // 콘텐츠 실행·씬 전환 중 초기화 금지
            }

            await gameDataService.ResetToDefaultAsync();
            OfflineRewardAdClaimStore.Clear(); // 계정 세이브가 아닌 로컬 PlayerPrefs라 별도로 같이 초기화
            sceneLoader.Load(projectConfig.EntrySceneId); // 새 Snapshot으로 다시 진입
            return true;
        }

        private async Task<string> DrawMonsterForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return "현재 몬스터를 뽑을 수 없습니다";
            }

            var roster = gameDataService.View.Monsters;
            var candidates = new List<MonsterDefinition>();
            var definitions = projectConfig.MonsterCatalog.Definitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition != null && !roster.Owns(definition.MonsterId))
                {
                    candidates.Add(definition);
                }
            }

            if (candidates.Count == 0)
            {
                return "모든 카탈로그 몬스터를 보유 중입니다";
            }

            var selected = candidates[UnityEngine.Random.Range(0, candidates.Count)]; // 미보유 항목 균등 선택
            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.AcquireMonster(selected.MonsterId));
            return saved
                ? $"{selected.DisplayName} 획득 완료"
                : "획득 정보를 저장하지 못했습니다";
        }

        private async Task<string> AcquireEquipmentForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return "현재 장비를 받을 수 없습니다";
            }

            var before = gameDataService.View.Equipment.Instances.Count;
            var basisStage = ExpeditionEquipmentLevelResolver.ResolveHighestClearedStage(gameDataService.View);
            var drops = EquipmentDropRoller.RollDrop(
                projectConfig.EquipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault,
                basisStage);
            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.AcquireEquipment(drops));
            if (!saved)
            {
                return "장비 획득 정보를 저장하지 못했습니다";
            }

            var acquired = gameDataService.View.Equipment.Instances.Count - before;
            return acquired > 0 ? $"장비 {acquired}개 획득 완료" : "장비 보유 한도입니다";
        }

        private async Task<string> AcquireAllItemsForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return "현재 아이템을 받을 수 없습니다";
            }

            var catalog = projectConfig.ItemCatalog;
            if (catalog == null || !catalog.TryValidateRuntimeCatalog(out _))
            {
                return "아이템 카탈로그가 올바르지 않습니다";
            }

            var definitions = catalog.Definitions;
            if (definitions == null || definitions.Count == 0)
            {
                return "지급할 아이템이 없습니다";
            }

            var rewards = new List<ItemAmount>(definitions.Count);
            var inventory = gameDataService.View.Items;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    return "아이템 카탈로그가 올바르지 않습니다";
                }

                inventory.TryGetQuantity(definition.ItemId, out var currentQuantity);
                if (currentQuantity < definition.MaxQuantity)
                {
                    // 던전 열쇠류처럼 MaxQuantity가 작은 아이템(예: 3개)이 섞여 있으면 200000을 그대로
                    // 지급하려다 한도를 넘겨서, ItemInventoryData.TryGrant가 배치 전체를 통째로
                    // 거부해버린다(한 종류라도 한도 초과면 전부 실패). 남은 여유만큼만 지급해 항상
                    // 한도 이내로 들어오게 한다.
                    var amount = Math.Min(200000L, definition.MaxQuantity - currentQuantity);
                    rewards.Add(new ItemAmount(definition.ItemId, amount));
                }
            }

            if (rewards.Count == 0)
            {
                return "모든 아이템이 보유 한도입니다";
            }

            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.GrantItems(rewards.ToArray()));
            return saved
                ? $"{rewards.Count}종 아이템 획득 완료(최대 200000개씩)"
                : "아이템 획득 정보를 저장하지 못했습니다";
        }

        private async Task<string> SendRandomMailForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return "현재 우편을 보낼 수 없습니다";
            }

            if (gameDataService.View.Mail.Entries.Count >= MailProgressData.MaximumStoredMail)
            {
                return "우편함이 가득 찼습니다";
            }

            var catalog = projectConfig.ItemCatalog;
            if (catalog == null || !catalog.TryValidateRuntimeCatalog(out _))
            {
                return "아이템 카탈로그가 올바르지 않습니다";
            }

            var inventory = gameDataService.View.Items;
            var candidates = new List<ItemDefinition>();
            var definitions = catalog.Definitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    continue;
                }

                inventory.TryGetQuantity(definition.ItemId, out var currentQuantity);
                if (currentQuantity < definition.MaxQuantity)
                {
                    candidates.Add(definition);
                }
            }

            if (candidates.Count == 0)
            {
                return "첨부할 수 있는 아이템이 없습니다";
            }

            var attachmentCount = UnityEngine.Random.Range(1, Math.Min(3, candidates.Count) + 1);
            var attachments = new List<ItemAmount>(attachmentCount);
            var rewardNames = new List<string>(attachmentCount);
            for (var index = 0; index < attachmentCount; index++)
            {
                var selectedIndex = UnityEngine.Random.Range(0, candidates.Count);
                var selected = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);
                inventory.TryGetQuantity(selected.ItemId, out var currentQuantity);
                var capacity = Math.Max(0L, selected.MaxQuantity - currentQuantity);
                var desired = GetDebugMailRewardAmount(selected.Category);
                var amount = Math.Min(desired, capacity);
                if (amount <= 0L)
                {
                    continue;
                }

                attachments.Add(new ItemAmount(selected.ItemId, amount));
                rewardNames.Add($"{selected.DisplayName} {amount:N0}");
            }

            if (attachments.Count == 0)
            {
                return "첨부할 수 있는 아이템이 없습니다";
            }

            var category = (MailCategory)UnityEngine.Random.Range(0, 3);
            var titles = new[] { "시스템 점검 보상", "깜짝 이벤트 선물", "원정대 지원 보급" };
            var bodies = new[]
            {
                "안정적인 플레이 환경을 위한 점검 보상입니다. 첨부 보상을 확인해 주세요.",
                "군단장님을 위해 준비한 깜짝 선물입니다. 우편함에서 보상을 받아 주세요.",
                "다음 원정을 위한 지원 물자입니다. 전투 준비에 활용해 주세요."
            };
            var now = DateTime.UtcNow;
            var mail = MailEntryData.Create(
                $"debug_mail_{now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
                titles[(int)category],
                bodies[(int)category],
                category,
                now,
                now.AddDays(UnityEngine.Random.Range(3, 31)),
                attachments);
            var saved = await gameDataService.TryApplyAndSaveAsync(GameProgressChange.AddMail(mail));
            return saved
                ? $"우편 발송 완료: {string.Join(", ", rewardNames)}"
                : "우편 발송 정보를 저장하지 못했습니다";
        }

        private static long GetDebugMailRewardAmount(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Currency => UnityEngine.Random.Range(100, 501),
                ItemCategory.UpgradeMaterial => UnityEngine.Random.Range(3, 11),
                ItemCategory.SummonTicket => UnityEngine.Random.Range(1, 4),
                ItemCategory.DungeonKey => 1L,
                _ => UnityEngine.Random.Range(1, 4)
            };
        }
#endif

        private ISceneContext CreateSceneContext(SceneId sceneId)
        {
            if (sceneId == projectConfig.EntrySceneId)
            {
                return new EntrySceneContext(() => sceneLoader.Load(projectConfig.MainBattleSceneId)); // Entry 출구 연결
            }

            if (sceneId == projectConfig.MainBattleSceneId)
            {
                return new MainSceneContext(
                    gameDataService,
                    contentFlow,
                    projectConfig.MonsterCatalog,
                    projectConfig.ItemCatalog,
                    commanderGrowthConfig,
                    projectConfig.EquipmentBalanceConfig,
                    BuildCurrentParty, // 저장 확정 시 모든 군단 보너스로 새 부대 사진 생성
                    rewardPresenter,
                    growthDungeonSweepService);
            }

            return contentFlow.CreateSeparateSceneContext(sceneId); // 별도 콘텐츠 실행 봉투
        }

        private BattlePartySnapshot BuildCurrentParty()
        {
            var modifiers = LegionStatModifierProvider.Build(
                gameDataService.View,
                commanderGrowthConfig,
                projectConfig.EquipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault);
            var snapshot = partyBuilder.Build(gameDataService.View, modifiers);
            QuestRuntime.ReportCommanderPower(snapshot.TotalPower); // CommanderPowerReach 반복 퀘스트가 참조하는 캐시값 갱신
            return snapshot;
        }

        private void HandleSceneFailed(SceneId failedSceneId, string error)
        {
            sceneLoadingOverlay?.Hide();
            Debug.LogError($"Scene flow failed. Scene={failedSceneId}, Error={error}");
            contentFlow?.NotifySceneLoadFailed(failedSceneId); // 별도 콘텐츠 진입 실패 잠금 해제
            if (failedSceneId != projectConfig.EntrySceneId && !sceneLoader.IsTransitioning)
            {
                sceneLoader.Load(projectConfig.EntrySceneId); // 실패 시 Entry로 복귀
            }
        }

        private void HandleSceneReady(SceneId sceneId)
        {
            readySceneId = sceneId;
            sceneLoadingOverlay?.Hide();
            if (sceneId == projectConfig.EntrySceneId)
            {
                globalAudioController?.PlayEntryBgm();
            }

            if (sceneId == projectConfig.MainBattleSceneId)
            {
                globalAudioController?.PlayMainBattleBgm();
                if (!ShowPendingOfflineRewards())
                {
                    ShowPendingAttendance(); // 오프라인 정산이 없으면 출석부터 표시
                }
            }
        }

        private void HandleHostedRunStarted(ContentId contentId)
        {
            globalAudioController?.ApplyHostedContentBgm(contentId);
        }

        private void HandleHostedRunFinished(ContentId contentId)
        {
            if (readySceneId == projectConfig.MainBattleSceneId)
            {
                globalAudioController?.PlayMainBattleBgm();
            }
        }

        private void HandleSceneLoadStarted(SceneId sceneId)
        {
            sceneLoadingOverlay?.Show(sceneId);
        }

        private void HandleLogoutRequested()
        {
            if (!initialized || sceneLoader == null || sceneLoader.IsTransitioning ||
                contentFlow == null || contentFlow.IsRunning)
            {
                return;
            }

            AccountSessionStore.Logout(); // 진행 저장은 유지하고 세션만 종료
            sceneLoader.Load(projectConfig.EntrySceneId);
        }

        private async Task<bool> HandleDeleteProgressRequestedAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null || contentFlow.IsRunning ||
                sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return false; // 콘텐츠 실행·씬 전환 중에는 진행 데이터 삭제 금지
            }

            await gameDataService.ResetToDefaultAsync(); // 저장 성공 뒤 기본 진행값으로 확정
            sceneLoader.Load(projectConfig.EntrySceneId);
            return true;
        }

        private bool ShowPendingOfflineRewards()
        {
            if (offlineRewardPresenter == null || offlineRewardCoordinator == null)
            {
                return false;
            }

            if (offlineRewardCoordinator.TryGetPendingPresentation(out var presentation))
            {
                offlineRewardPresenter.Show(
                    presentation,
                    projectConfig.ItemCatalog,
                    () => offlineRewardCoordinator.AcknowledgeAsync(presentation.ReceiptIds),
                    HandleOfflineRewardConfirmed,
                    GrantOfflineRewardBonusAsync);
                return true;
            }

            if (offlineRewardCoordinator.LastStatus == OfflineRewardCalculationStatus.InventoryBlocked)
            {
                offlineRewardPresenter.ShowInventoryBlocked(() => ShowPendingAttendance());
                return true;
            }

            return false;
        }

        private async void RetryBlockedOfflineSettlement()
        {
            if (!initialized || retryingOfflineSettlement || offlineRewardCoordinator == null ||
                !offlineRewardCoordinator.HasPendingSettlement ||
                offlineRewardCoordinator.LastStatus != OfflineRewardCalculationStatus.InventoryBlocked)
            {
                return;
            }

            retryingOfflineSettlement = true;
            try
            {
                if (await offlineRewardCoordinator.RetryPendingAsync() &&
                    offlineRewardCoordinator.LastStatus == OfflineRewardCalculationStatus.Ready &&
                    readySceneId == projectConfig.MainBattleSceneId &&
                    offlineRewardPresenter != null && !offlineRewardPresenter.IsOpen)
                {
                    ShowPendingOfflineRewards();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                retryingOfflineSettlement = false;
            }
        }

        // 광고 영상을 끝까지 시청했을 때, 이미 지급된 방치 보상과 동일한 만큼을 한 번 더 지급해
        // 결과적으로 2배가 되도록 한다. 장비는 인스턴스 ID가 겹치면 안 되므로 새 ID로 복제한다.
        private async Task<bool> GrantOfflineRewardBonusAsync(OfflineRewardPresentation presentation)
        {
            if (presentation == null || gameDataService == null)
            {
                return false;
            }

            try
            {
                var items = new List<ItemAmount>(3);
                if (presentation.EquipmentSlotUpgradeStone > 0L)
                {
                    items.Add(new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, presentation.EquipmentSlotUpgradeStone));
                }

                if (presentation.CommanderSkillUpgradeStone > 0L)
                {
                    items.Add(new ItemAmount(ItemIds.CommanderSkillUpgradeStone, presentation.CommanderSkillUpgradeStone));
                }

                if (presentation.LegionPotentialUpgradeStone > 0L)
                {
                    items.Add(new ItemAmount(ItemIds.LegionPotentialUpgradeStone, presentation.LegionPotentialUpgradeStone));
                }

                var bundle = new RewardBundle(presentation.Gold, presentation.CommanderExperience, items);
                if (!bundle.IsEmpty &&
                    !await gameDataService.TryApplyAndSaveAsync(GameProgressChange.GrantRewards(bundle)))
                {
                    return false;
                }

                if (presentation.EquipmentRewards.Count > 0)
                {
                    var bonusEquipment = new List<EquipmentInstanceData>(presentation.EquipmentRewards.Count);
                    for (var index = 0; index < presentation.EquipmentRewards.Count; index++)
                    {
                        bonusEquipment.Add(CloneEquipmentWithNewInstanceId(presentation.EquipmentRewards[index]));
                    }

                    if (!await gameDataService.TryApplyAndSaveAsync(GameProgressChange.AcquireEquipment(bonusEquipment)))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static EquipmentInstanceData CloneEquipmentWithNewInstanceId(EquipmentInstanceData source)
        {
            var sourceOptions = source.RandomOptions;
            var clonedOptions = new List<EquipmentOptionRollData>(sourceOptions.Count);
            for (var index = 0; index < sourceOptions.Count; index++)
            {
                if (sourceOptions[index] != null)
                {
                    clonedOptions.Add(sourceOptions[index].Clone());
                }
            }

            return new EquipmentInstanceData(
                Guid.NewGuid().ToString("N"),
                source.Part,
                source.Grade, source.ItemLevel,
                clonedOptions);
        }

        private void HandleOfflineRewardConfirmed(OfflineRewardPresentation presentation)
        {
            try
            {
                var acquirePresentation = offlineRewardPresenter != null
                    ? offlineRewardPresenter.BuildConfirmedAcquirePresentation(presentation)
                    : presentation?.CreateAcquirePresentation(projectConfig.ItemCatalog);
                rewardPresenter?.PlayConfirmed(acquirePresentation);
                if (!ShowPendingOfflineRewards())
                {
                    ShowPendingAttendance(); // 접속 정산을 모두 확인한 뒤 출석 표시
                }
            }
            catch (Exception exception)
            {
                // 팝업은 이미 닫힌 뒤라 여기서 예외가 나면 다음 화면 전환(다음 영수증/출석)이 조용히
                // 끊길 수 있다. 로그만 남기고 최소한 출석 표시는 시도해 화면이 멈추지 않게 한다.
                Debug.LogException(exception);
                try
                {
                    ShowPendingAttendance();
                }
                catch (Exception fallbackException)
                {
                    Debug.LogException(fallbackException);
                }
            }
        }

        private void ShowPendingAttendance()
        {
            if (gameDataService == null || !gameDataService.View.Attendance.HasPendingReward)
            {
                return;
            }

            FindFirstObjectByType<MainBattleManagementUiController>(FindObjectsInactive.Include)
                ?.OpenAttendancePage();
        }

        private async void OnApplicationPause(bool paused)
        {
            if (paused && initialized)
            {
                await FlushQuestProgressSafelyAsync(); // 지연 묶음 저장을 백그라운드 전환 전에 확정
                await SaveOfflineInactiveSafelyAsync(); // 방치 시작점을 진행 데이터와 함께 확정
                await SaveCurrentSafelyAsync(); // 백그라운드 전환 저장
                return;
            }

            if (!paused && initialized)
            {
                await RefreshQuestPeriodsSafelyAsync(); // 앱을 켜둔 채 05:00 경계를 넘긴 경우 즉시 초기화
            }

            if (!paused && initialized && offlineRewardCoordinator != null)
            {
                try
                {
                    if (await offlineRewardCoordinator.ResumeAsync() &&
                        readySceneId == projectConfig.MainBattleSceneId)
                    {
                        ShowPendingOfflineRewards();
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private async void OnApplicationQuit()
        {
            if (initialized)
            {
                await FlushQuestProgressSafelyAsync(); // 종료 직전 대기 중인 전투 이벤트 확정
                await SaveOfflineInactiveSafelyAsync(); // Pause 누락 환경의 종료시각 보완
                await SaveCurrentSafelyAsync(); // 앱 종료 직전 저장
            }
        }

        private static async Task FlushQuestProgressSafelyAsync()
        {
            try
            {
                await QuestRuntime.FlushPendingProgressAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static async Task RefreshQuestPeriodsSafelyAsync()
        {
            try
            {
                await QuestRuntime.RefreshPeriodsAsync(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task SaveOfflineInactiveSafelyAsync()
        {
            if (offlineRewardCoordinator == null)
            {
                return;
            }

            try
            {
                if (!await offlineRewardCoordinator.MarkInactiveAsync())
                {
                    Debug.LogError("Offline inactive time could not be saved.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task SaveCurrentSafelyAsync()
        {
            try
            {
                await gameDataService.SaveCurrentAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task RefreshGrowthDungeonKeysAsync()
        {
            var view = gameDataService.View;
            var currentPeriod = GrowthDungeonDailyKeyRules.GetPeriodId(DateTime.UtcNow);
            var previousPeriod = view.GrowthDungeons.LastDailyKeyPeriod;
            if (currentPeriod <= previousPeriod)
            {
                return;
            }

            var targets = new ItemAmount[GrowthDungeonDailyKeyRules.KeyItemIds.Count];
            for (var index = 0; index < targets.Length; index++)
            {
                var itemId = GrowthDungeonDailyKeyRules.KeyItemIds[index];
                view.Items.TryGetQuantity(itemId, out var currentQuantity);
                targets[index] = new ItemAmount(
                    itemId,
                    GrowthDungeonDailyKeyRules.GetRechargedQuantity(currentQuantity));
            }

            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.RefreshGrowthDungeonDailyKeys(previousPeriod, currentPeriod, targets));
            if (!saved)
            {
                throw new InvalidOperationException("Growth dungeon daily keys could not be refreshed.");
            }
        }

        private async Task RefreshAttendanceAsync()
        {
            var view = gameDataService.View.Attendance;
            var currentPeriod = GrowthDungeonDailyKeyRules.GetPeriodId(DateTime.UtcNow);
            if (currentPeriod <= view.LastProcessedPeriod)
            {
                return;
            }

            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.RefreshAttendance(view.LastProcessedPeriod, currentPeriod));
            if (!saved)
            {
                throw new InvalidOperationException("Attendance could not be refreshed.");
            }
        }

        private async Task CleanupExpiredMailAsync()
        {
            var utcNow = DateTime.UtcNow;
            if (!gameDataService.View.Mail.HasExpired(utcNow))
            {
                return;
            }

            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.CleanupExpiredMail(utcNow));
            if (!saved)
            {
                throw new InvalidOperationException("Expired mail could not be cleaned up.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (sceneLoader != null)
                {
                    sceneLoader.SceneLoadStarted -= HandleSceneLoadStarted;
                    sceneLoader.SceneReady -= HandleSceneReady;
                    sceneLoader.SceneFailed -= HandleSceneFailed;
                }

                if (gameDataService != null)
                {
                    gameDataService.Changed -= RetryBlockedOfflineSettlement;
                }

                if (contentFlow != null)
                {
                    contentFlow.HostedRunStarted -= HandleHostedRunStarted;
                    contentFlow.HostedRunFinished -= HandleHostedRunFinished;
                }

                AccountRuntimeBridge.LogoutRequested -= HandleLogoutRequested;
                AccountRuntimeBridge.DeleteProgressRequested = null;

                Instance = null;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(ProjectConfig config, SceneLoader loader)
        {
            projectConfig = config;
            sceneLoader = loader;
        }

        public void EditorConfigureRewardPresenter(RewardAcquirePresenter presenter)
        {
            rewardPresenter = presenter;
        }

        public void EditorConfigureFinishFeedback(ContentFinishFeedbackPresenter presenter)
        {
            finishFeedbackPresenter = presenter;
        }

        public void EditorConfigureResultOverlay(ContentResultOverlayPresenter presenter)
        {
            resultOverlayPresenter = presenter;
        }

        public void EditorConfigureOfflineRewardPresenter(OfflineRewardPopupPresenter presenter)
        {
            offlineRewardPresenter = presenter;
        }

        public void EditorConfigureGlobalPresentation(
            SceneLoadingOverlayPresenter loading,
            SleepModeController sleep,
            GlobalAudioController audio)
        {
            sceneLoadingOverlay = loading;
            sleepModeController = sleep;
            globalAudioController = audio;
        }
#endif
    }
}
