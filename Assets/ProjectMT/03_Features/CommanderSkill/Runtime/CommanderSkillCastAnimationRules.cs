using System;

namespace ProjectMT.Features.CommanderSkill
{
    public static class CommanderSkillCastAnimationRules
    {
        public const float StatePlaybackSpeed = 1.6f;
        public const string StatePrefix = "Base Layer.CommanderSkill_Attack_";

        public static int ResolveAttackNumber(string skillId) => skillId switch
        {
            "CS_TrackingBlade" => 1,       // 빠른 마력검 연사
            "CS_DoomSpear" => 4,           // 힘을 모아 위에서 내려꽂는 강한 시전
            "CS_AbyssChain" => 6,          // 팔을 뻗어 붙잡는 사슬 제압
            "CS_PhantomCharge" => 5,       // 전방으로 몸을 싣는 돌진 명령
            "CS_ConquerorSigil" => 2,      // 양팔을 펼치는 아군 강화 의식
            "CS_PhantomBarrage" => 3,      // 넓게 지시하는 연속 포격
            "CS_DeathSentence" => 8,       // 한 대상을 지정하는 저주
            "CS_RuptureMarch" => 10,       // 지면을 연속으로 휩쓰는 파열
            "CS_HeartOfBattlefield" => 2,  // 양팔을 펼치는 회복·보호 의식
            "CS_MarchOfDead" => 7,         // 양손을 들어 대규모 군세 소환
            "CS_WarGodBrand" => 8,         // 한 대상을 지정하는 낙인
            "CS_ApocalypseWar" => 9,       // 가장 큰 전신 동작의 궁극 의식
            _ => 1
        };

        public static string StateName(int attackNumber)
        {
            if (attackNumber < 1 || attackNumber > 10)
                throw new ArgumentOutOfRangeException(nameof(attackNumber));
            return StatePrefix + attackNumber.ToString("00");
        }

        public static string ClipName(int attackNumber)
        {
            if (attackNumber < 1 || attackNumber > 10)
                throw new ArgumentOutOfRangeException(nameof(attackNumber));
            return "attack_" + attackNumber.ToString("00");
        }
    }
}
