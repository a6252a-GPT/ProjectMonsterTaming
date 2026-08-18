# 퀘스트 폴더 이식

다른 프로젝트에는 아래 **두 폴더만** 복사하면 됩니다.

```
Assets/ProjectMT/02_Shared/Quest
Assets/ProjectMT/03_Features/Quest
```

`MainBattleSceneRoot` 같은 씬 스크립트는 수정하지 않아도 됩니다. `QuestDebugController`를 씬에 붙이고 Catalog만 연결하면, 저장 서비스가 로드될 때 스스로 붙습니다.

## 대상 프로젝트에 한 번만 필요한 훅

저장은 기존 `GameProgressData` 파일을 쓰기 때문에, 퀘스트 코드만으로는 저장 클래스에 붙지 않습니다. 대상 프로젝트의 같은 파일에 아래만 있으면 됩니다. (이 프로젝트에는 이미 들어가 있습니다.)

1. `GameProgressData`, `GameProgressChange`, `GameDataService`, `IGameProgressService` 선언에 `partial` 추가
2. `GameProgressData.Clone()` 끝에서 `CopyQuestTo(clone);`
3. `GameProgressData.Repair()` 끝에서 `RepairQuest();`
4. `TryApply()` 안에서
   - 보상 지급 전: `RejectInvalidQuestClaim(change, ref questRejected);`
   - 진행도 반영: `ApplyQuestProgress(change, ref questRejected);`
   - 수령 확정: `ApplyQuestClaim(change);`
5. `GameDataService.LoadAsync()` / `ResetToDefaultAsync()` 끝에서 `NotifyProgressReady();`
6. 각 클래스에 대응하는 `partial void` 선언 (이 프로젝트 `GameProgressData.cs`, `GameDataService.cs`와 동일)

훅이 없으면 `02_Shared/Quest/GameProgressData.Quest.cs`가 컴파일되지 않습니다.

## 의존

- Shared 어셈블리에 `GameProgressData` / `IGameProgressService` / `RewardDefinition` / `RewardBundle` 이 있어야 합니다.
- Features 어셈블리가 Shared를 참조해야 합니다.
- 에디터 도구(`Editor/QuestTestDataFactory.cs`)는 `ProjectMT.Features`, `ProjectMT.Shared` asmdef 이름을 참조합니다. 대상 프로젝트 이름이 다르면 asmdef references만 바꿔 주세요.

## 테스트 데이터

Unity 메뉴 `JC Tool > Quest > Create Test Data` 로 샘플 퀘스트 2개를 만들 수 있습니다.
