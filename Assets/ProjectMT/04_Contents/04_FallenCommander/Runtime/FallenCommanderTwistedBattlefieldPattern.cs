using System.Collections.Generic;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    // 연속 장판 공격의 안전지대 반전·경고·피해·정리를 전담한다.
    public sealed class FallenCommanderTwistedBattlefieldPattern
    {
        private enum PatternState
        {
            Inactive,
            Warning,
            Interval
        }

        private enum BattlefieldLayout
        {
            VerticalStripes,
            HorizontalStripes,
            Checkerboard
        }

        private sealed class BattlefieldTile
        {
            public Vector3 Center;
            public Vector2 Size;
            public bool IsDangerous;
            public FallenCommanderTelegraphView Telegraph;
        }

        private readonly struct RecordedBeat
        {
            public RecordedBeat(BattlefieldLayout recordedLayout, bool inverted)
            {
                Layout = recordedLayout;
                IsInverted = inverted;
            }

            public BattlefieldLayout Layout { get; }
            public bool IsInverted { get; }
        }

        private readonly List<BattlefieldTile> tiles = new();
        private readonly List<RecordedBeat> recordedBeats = new();
        private readonly FallenCommanderResolveVfxPool resolveVfxPool = new();

        private FallenCommanderTwistedBattlefieldData data;
        private FallenCommanderTwistedBattlefieldPhaseData phaseData;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private Transform effectParent;
        private Vector3 arenaCenter;
        private PatternState state;
        private BattlefieldLayout layout;
        private BattlefieldLayout previousLayout;
        private float remainingTime;
        private int beatIndex;
        private int resolveBeatIndex;
        private bool hasPreviousLayout;
        private bool isResolvingRecordedBeats;
        private System.Action<float, System.Action> damageDelayScheduler;

        public bool IsActive => state != PatternState.Inactive;
        public FallenCommanderTelegraphView ActiveTelegraph =>
            tiles.Count == 0 ? null : tiles[0].Telegraph;

        // 현재 페이즈 설정으로 첫 전장 장판과 시전 연출을 시작한다.
        public bool Begin(
            FallenCommanderTwistedBattlefieldData patternData,
            FallenCommanderTwistedBattlefieldPhaseData currentPhaseData,
            UnitActor boss,
            Transform commander,
            HealthComponent health,
            FallenCommanderBossAnimationPresenter animations,
            Transform parent,
            Vector3 battlefieldCenter,
            System.Action<float, System.Action> delayScheduler)
        {
            Cancel();
            if (patternData == null ||
                patternData.TelegraphPrefab == null ||
                currentPhaseData == null ||
                boss == null ||
                commander == null ||
                health == null)
            {
                return false;
            }

            data = patternData;
            phaseData = currentPhaseData;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = health;
            animationPresenter = animations;
            effectParent = parent;
            arenaCenter = battlefieldCenter;
            damageDelayScheduler = delayScheduler;
            beatIndex = 0;
            resolveBeatIndex = 0;
            hasPreviousLayout = false;
            isResolvingRecordedBeats = false;
            recordedBeats.Clear();

            animationPresenter?.PlayPreCast(
                data.PreCastMotion,
                playbackSpeed: data.PreCastMotionSpeed,
                normalizedStart: data.PreCastMotionStart,
                normalizedEnd: data.PreCastMotionEnd);
            FallenCommanderAttackEffectPlayer.PlayStart(
                data.Effects,
                bossActor.transform.position,
                bossActor.transform.forward,
                effectParent,
                bossActor.transform,
                commanderRoot);

            BeginBeat();
            return tiles.Count > 0;
        }

        // 현재 장판의 진행도와 박자 전환을 갱신하고 전체 패턴 종료 여부를 반환한다.
        public bool Tick(float deltaTime)
        {
            if (!IsActive)
            {
                return true;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Mathf.Max(0f, deltaTime));
            if (state == PatternState.Interval)
            {
                if (remainingTime <= 0f)
                {
                    if (isResolvingRecordedBeats)
                    {
                        ResolveRecordedBeat();
                        return !IsActive;
                    }

                    BeginBeat();
                }

                return false;
            }

            var fillRemaining = Mathf.Max(
                0f,
                remainingTime - phaseData.TelegraphHoldDuration);
            var progress = 1f - fillRemaining / phaseData.WarningDuration;
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].IsDangerous)
                {
                    tiles[index].Telegraph?.SetProgress(progress);
                }
            }

            if (remainingTime > 0f)
            {
                return false;
            }

            recordedBeats.Add(new RecordedBeat(layout, beatIndex % 2 == 1));
            beatIndex++;
            DestroyTiles();
            if (beatIndex >= phaseData.BeatCount)
            {
                isResolvingRecordedBeats = true;
                resolveBeatIndex = 0;
            }

            state = PatternState.Interval;
            remainingTime = phaseData.BeatInterval;
            if (remainingTime <= 0f)
            {
                if (isResolvingRecordedBeats)
                {
                    ResolveRecordedBeat();
                    return !IsActive;
                }

                BeginBeat();
            }

            return false;
        }

        // 짝수 박자는 새 배치를 선택하고 홀수 박자는 직전 위험·안전 영역을 반전한다.
        private void BeginBeat()
        {
            resolveVfxPool.ReleaseAll();
            var isInverted = beatIndex % 2 == 1;
            if (!isInverted)
            {
                layout = SelectNextLayout();
                previousLayout = layout;
                hasPreviousLayout = true;
            }
            else
            {
                layout = previousLayout;
            }

            BuildTiles(layout, isInverted);
            remainingTime = phaseData.WarningDuration + phaseData.TelegraphHoldDuration;
            state = PatternState.Warning;
        }

        // 기억 단계에서 저장한 장판을 같은 순서로 복원한 뒤 경고 표시 없이 공격만 발동한다.
        private void ResolveRecordedBeat()
        {
            if (resolveBeatIndex >= recordedBeats.Count)
            {
                ReleaseRuntimeState();
                return;
            }

            resolveVfxPool.ReleaseAll();
            var recordedBeat = recordedBeats[resolveBeatIndex];
            BuildTiles(recordedBeat.Layout, recordedBeat.IsInverted);
            HideTileTelegraphs();
            ResolveBeat();
            DestroyTiles();
            resolveBeatIndex++;
            if (resolveBeatIndex >= recordedBeats.Count)
            {
                ReleaseRuntimeState();
                return;
            }

            state = PatternState.Interval;
            remainingTime = data.AttackInterval;
        }

        private void HideTileTelegraphs()
        {
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].Telegraph != null)
                {
                    tiles[index].Telegraph.gameObject.SetActive(false);
                }
            }
        }

        // 같은 배치가 연속 쌍에서 반복되지 않도록 다음 전장 배치를 무작위로 고른다.
        private BattlefieldLayout SelectNextLayout()
        {
            var candidate = (BattlefieldLayout)Random.Range(0, 3);
            if (!hasPreviousLayout || candidate != previousLayout)
            {
                return candidate;
            }

            return (BattlefieldLayout)(((int)candidate + Random.Range(1, 3)) % 3);
        }

        // 선택된 세로·가로·격자 배치를 전장 전체를 덮는 사각 장판으로 생성한다.
        private void BuildTiles(BattlefieldLayout selectedLayout, bool isInverted)
        {
            DestroyTiles();
            switch (selectedLayout)
            {
                case BattlefieldLayout.VerticalStripes:
                    BuildGrid(data.ColumnCount, 1, isInverted);
                    break;
                case BattlefieldLayout.HorizontalStripes:
                    BuildGrid(1, Mathf.Max(2, data.RowCount * 2), isInverted);
                    break;
                default:
                    BuildGrid(data.ColumnCount, data.RowCount, isInverted);
                    break;
            }
        }

        // 지정된 격자를 위험·안전 장판이 번갈아 나타나는 형태로 채운다.
        private void BuildGrid(int columns, int rows, bool isInverted)
        {
            var extents = data.ArenaHalfExtents;
            var fullWidth = extents.x * 2f;
            var fullLength = extents.y * 2f;
            var cellWidth = fullWidth / columns;
            var cellLength = fullLength / rows;
            var visibleWidth = Mathf.Max(0.1f, cellWidth - data.TileGap);
            var visibleLength = Mathf.Max(0.1f, cellLength - data.TileGap);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var center = arenaCenter + new Vector3(
                        -extents.x + cellWidth * (column + 0.5f),
                        0f,
                        -extents.y + cellLength * (row + 0.5f));
                    var isDangerous = ((row + column) & 1) == 0;
                    if (isInverted)
                    {
                        isDangerous = !isDangerous;
                    }

                    CreateTile(
                        center,
                        new Vector2(cellWidth, cellLength),
                        new Vector2(visibleWidth, visibleLength),
                        isDangerous);
                }
            }
        }

        // 하나의 전장 칸을 빈틈없는 판정 크기와 간격이 적용된 표시 크기로 생성한다.
        private void CreateTile(Vector3 center, Vector2 hitSize, Vector2 visibleSize, bool isDangerous)
        {
            var origin = center - Vector3.forward * (visibleSize.y * 0.5f);
            var telegraph = FallenCommanderTelegraphView.CreateRectangle(
                data.TelegraphPrefab,
                effectParent,
                origin,
                Vector3.forward,
                visibleSize.x,
                visibleSize.y,
                isDangerous
                    ? FallenCommanderTelegraphPalette.Danger
                    : FallenCommanderTelegraphPalette.Safe);
            telegraph?.SetProgress(isDangerous ? 0f : 1f);
            tiles.Add(new BattlefieldTile
            {
                Center = center,
                Size = hitSize,
                IsDangerous = isDangerous,
                Telegraph = telegraph
            });
        }

        // 현재 박자의 발동 연출을 재생하고 위험 칸에 있는 군단장에게 하트 한 칸만 적용한다.
        private void ResolveBeat()
        {
            animationPresenter?.Play(
                data.CastMotion,
                stopAfterMotion: true,
                durationOverride: data.CastMotionDuration,
                playbackSpeed: data.CastMotionSpeed,
                normalizedStart: data.CastMotionStart,
                normalizedEnd: data.CastMotionEnd);
            var dangerTiles = new List<BattlefieldTile>();
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (!tile.IsDangerous)
                {
                    continue;
                }

                dangerTiles.Add(tile);

                resolveVfxPool.Play(
                    data.Effects,
                    tile.Center,
                    Vector3.forward,
                    effectParent,
                    bossActor == null ? null : bossActor.transform,
                    commanderRoot,
                    ResolveTileVfxScale(tile.Size));
            }

            FallenCommanderAttackEffectPlayer.PlayResolveSfx(
                data.Effects,
                arenaCenter,
                Vector3.forward,
                bossActor == null ? null : bossActor.transform,
                commanderRoot);

            var attacker = bossActor;
            var target = commanderRoot;
            var targetHealth = commanderHealth;
            var effects = data.Effects;
            var delay = data.DamageDelay;
            var parent = effectParent;
            ScheduleDamage(delay, () =>
            {
                if (attacker == null ||
                    !attacker.IsAlive ||
                    target == null ||
                    targetHealth == null ||
                    !targetHealth.IsAlive)
                {
                    return;
                }

                for (var index = 0; index < dangerTiles.Count; index++)
                {
                    var tile = dangerTiles[index];
                    var offset = target.position - tile.Center;
                    if (Mathf.Abs(offset.x) > tile.Size.x * 0.5f ||
                        Mathf.Abs(offset.z) > tile.Size.y * 0.5f)
                    {
                        continue;
                    }

                    FallenCommanderAttackEffectPlayer.PlayHit(
                        effects,
                        target.position,
                        Vector3.forward,
                        parent,
                        attacker.transform,
                        target);
                    targetHealth.ApplyDamage(new DamageRequest(
                        attacker,
                        1f,
                        target.position));
                    return;
                }
            });
        }

        private void ScheduleDamage(float delay, System.Action apply)
        {
            if (damageDelayScheduler == null)
            {
                apply?.Invoke();
                return;
            }

            damageDelayScheduler.Invoke(Mathf.Max(0f, delay), apply);
        }

        private static Vector3 ResolveTileVfxScale(Vector2 tileSize)
        {
            return new Vector3(
                Mathf.Max(0.01f, tileSize.x),
                1f,
                Mathf.Max(0.01f, tileSize.y));
        }

        // 군단장의 현재 위치가 포함된 위험 칸을 하나만 찾아 중복 피해를 방지한다.
        private bool TryFindCommanderDangerTile(out Vector3 hitPosition)
        {
            hitPosition = commanderRoot == null ? arenaCenter : commanderRoot.position;
            if (commanderRoot == null || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return false;
            }

            var position = commanderRoot.position;
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (!tile.IsDangerous)
                {
                    continue;
                }

                var offset = position - tile.Center;
                if (Mathf.Abs(offset.x) <= tile.Size.x * 0.5f &&
                    Mathf.Abs(offset.z) <= tile.Size.y * 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        // 중단·브레이크·씬 종료 시 생성한 모든 장판과 런타임 참조를 정리한다.
        public void Cancel()
        {
            DestroyTiles();
            resolveVfxPool.ReleaseAll();
            ReleaseRuntimeState();
        }

        // 현재 박자에 생성된 전장 장판 오브젝트를 모두 제거한다.
        private void DestroyTiles()
        {
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].Telegraph != null)
                {
                    Object.Destroy(tiles[index].Telegraph.gameObject);
                }
            }

            tiles.Clear();
        }

        // 다음 실행을 위해 외부 참조와 진행 상태를 초기값으로 되돌린다.
        private void ReleaseRuntimeState()
        {
            data = null;
            phaseData = null;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            animationPresenter = null;
            effectParent = null;
            arenaCenter = Vector3.zero;
            state = PatternState.Inactive;
            remainingTime = 0f;
            beatIndex = 0;
            resolveBeatIndex = 0;
            hasPreviousLayout = false;
            isResolvingRecordedBeats = false;
            recordedBeats.Clear();
        }
    }
}
