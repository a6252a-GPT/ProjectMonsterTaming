using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    public enum ContentOpenMode // 콘텐츠 실행 위치
    {
        MainBattleHosted,
        SeparateScene
    }

    public abstract class ContentResultAdapter : ScriptableObject // 결과를 진행 변경으로 번역
    {
        public abstract bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change);

        public virtual bool TryCreateRewardPresentation(
            IContentResultData result,
            out RewardPresentationRequest presentation)
        {
            presentation = null;
            return false; // 실제 보상이 있는 Adapter만 표시값 제공
        }
    }

    public abstract class ContentStartDataFactory : ScriptableObject // 부대 사진으로 시작값 생성
    {
        public abstract IContentStartData Create(BattlePartySnapshot party);
    }

    [CreateAssetMenu(menuName = "ProjectMT/Content/Catalog", fileName = "ContentCatalog")]
    public sealed class ContentCatalog : ScriptableObject // 콘텐츠 등록부
    {
        [SerializeField] private List<ContentDefinition> definitions = new List<ContentDefinition>(); // 등록 Definition 목록

        public IReadOnlyList<ContentDefinition> Definitions => definitions;

        public bool TryGet(ContentId contentId, out ContentDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].ContentId == contentId)
                {
                    definition = definitions[i]; // ID가 같은 첫 항목 반환
                    return true;
                }
            }

            definition = null;
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetDefinitions(IEnumerable<ContentDefinition> values)
        {
            definitions = values == null ? new List<ContentDefinition>() : new List<ContentDefinition>(values);
        }
#endif
    }

}
