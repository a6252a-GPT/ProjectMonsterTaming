using System;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Contents.FallenCommander
{
    public sealed class FallenCommanderDebugController : IBossDungeonDebugController
    {
        private readonly Func<bool> isRunning;
        private readonly Func<bool> isStartDelayActive;
        private readonly Func<bool> isFinishing;
        private readonly Func<bool> isTimeoutWipeActive;
        private readonly Func<bool> isFinalChargeActive;
        private readonly Func<UnitActor> getBossActor;
        private readonly Func<FallenCommanderBossStateMachine> getStateMachine;
        private readonly Action prepareStandardAttack;
        private readonly FallenCommanderBattleFlow battleFlow;
        private readonly Action beginTimeoutWipe;
        private readonly Action publishHudState;
        private readonly Action<int> setBossPhase;
        private readonly Action startFinalCharge;

        public FallenCommanderDebugController(
            Func<bool> isRunning,
            Func<bool> isStartDelayActive,
            Func<bool> isFinishing,
            Func<bool> isTimeoutWipeActive,
            Func<bool> isFinalChargeActive,
            Func<UnitActor> getBossActor,
            Func<FallenCommanderBossStateMachine> getStateMachine,
            Action prepareStandardAttack,
            FallenCommanderBattleFlow battleFlow,
            Action beginTimeoutWipe,
            Action publishHudState,
            Action<int> setBossPhase,
            Action startFinalCharge)
        {
            this.isRunning = isRunning ?? throw new ArgumentNullException(nameof(isRunning));
            this.isStartDelayActive = isStartDelayActive ?? throw new ArgumentNullException(nameof(isStartDelayActive));
            this.isFinishing = isFinishing ?? throw new ArgumentNullException(nameof(isFinishing));
            this.isTimeoutWipeActive = isTimeoutWipeActive ?? throw new ArgumentNullException(nameof(isTimeoutWipeActive));
            this.isFinalChargeActive = isFinalChargeActive ?? throw new ArgumentNullException(nameof(isFinalChargeActive));
            this.getBossActor = getBossActor ?? throw new ArgumentNullException(nameof(getBossActor));
            this.getStateMachine = getStateMachine ?? throw new ArgumentNullException(nameof(getStateMachine));
            this.prepareStandardAttack = prepareStandardAttack ??
                throw new ArgumentNullException(nameof(prepareStandardAttack));
            this.battleFlow = battleFlow ?? throw new ArgumentNullException(nameof(battleFlow));
            this.beginTimeoutWipe = beginTimeoutWipe ?? throw new ArgumentNullException(nameof(beginTimeoutWipe));
            this.publishHudState = publishHudState ?? throw new ArgumentNullException(nameof(publishHudState));
            this.setBossPhase = setBossPhase ?? throw new ArgumentNullException(nameof(setBossPhase));
            this.startFinalCharge = startFinalCharge ?? throw new ArgumentNullException(nameof(startFinalCharge));
        }

        public void DebugTimeout()
        {
            if (!isRunning())
            {
                return;
            }

            battleFlow.ReduceTime(float.MaxValue);
            beginTimeoutWipe();
        }

        public void DebugReduceTimeTenSeconds()
        {
            if (!isRunning() || isFinishing() || isTimeoutWipeActive())
            {
                return;
            }

            var timedOut = battleFlow.ReduceTime(10f);
            publishHudState();
            if (timedOut)
            {
                beginTimeoutWipe();
            }
        }

        public void DebugKillBoss()
        {
            var bossActor = getBossActor();
            if (!isRunning() || isTimeoutWipeActive() || bossActor == null || !bossActor.IsAlive)
            {
                return;
            }

            bossActor.Health.ApplyDamage(new DamageRequest(
                null,
                bossActor.Health.CurrentHealth,
                bossActor.transform.position));
        }

        public void DebugDamageBossTenPercent()
        {
            var bossActor = getBossActor();
            if (!isRunning() || isTimeoutWipeActive() || bossActor == null || !bossActor.IsAlive)
            {
                return;
            }

            bossActor.Health.ApplyDamage(new DamageRequest(
                null,
                bossActor.Health.MaxHealth * 0.1f,
                bossActor.transform.position));
        }

        public void DebugSetBossPhase(int phaseNumber)
        {
            setBossPhase(phaseNumber);
        }

        public void DebugBasicAttack() => ForceStandardAttack(stateMachine => stateMachine.DebugForceBasicAttack());

        public void DebugMeleeAttack() => ForceStandardAttack(stateMachine => stateMachine.DebugForceMeleeAttack());

        public void DebugMarkStrike() => ForceStandardAttack(stateMachine => stateMachine.DebugForceMarkStrike());

        public void DebugTrackingMark() => ForceStandardAttack(stateMachine => stateMachine.DebugForceTrackingMark());

        public void DebugWideBurst() => ForceStandardAttack(stateMachine => stateMachine.DebugForceBlackHole());

        public void DebugLineStrike() => ForceStandardAttack(stateMachine => stateMachine.DebugForceLineStrike());

        public void DebugCorruptionRing() => ForceStandardAttack(stateMachine => stateMachine.DebugForceCorruptionRing());

        public void DebugTwistedBattlefield() => ForceStandardAttack(stateMachine => stateMachine.DebugForceTwistedBattlefield());

        public void DebugFallingBarrage() => ForceStandardAttack(stateMachine => stateMachine.DebugForceFallingBarrage());

        public void DebugChargedWideBurst()
        {
            if (!isRunning() || isStartDelayActive() || isTimeoutWipeActive())
            {
                return;
            }

            startFinalCharge();
        }

        private void ForceStandardAttack(Action<FallenCommanderBossStateMachine> forceAttack)
        {
            if (!isRunning() ||
                isStartDelayActive() ||
                isFinalChargeActive() ||
                isTimeoutWipeActive())
            {
                return;
            }

            prepareStandardAttack();
            var stateMachine = getStateMachine();
            if (stateMachine != null)
            {
                forceAttack(stateMachine);
            }
        }
    }
}
