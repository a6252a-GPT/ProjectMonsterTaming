# Monster Maker V1 레거시 보관본

이 폴더는 2026-08-30에 V2 단독 운용으로 전환하면서 `Assets` 밖으로 이동한
Monster Maker V1 소스와 당시 회귀 테스트의 원본 보관본이다.

## 현재 상태

- Unity의 `Assets` 밖에 있으므로 현재 프로젝트에서는 컴파일·메뉴 등록·창 생성 대상이 아니다.
- 운영 진입점은 `JC Tool/Monster/Monster Maker`의 V2 하나다.
- V1 소스와 테스트는 삭제하지 않았으며 이 폴더의 `UnityProject/Assets/...` 원래 상대 경로로 보관한다.
- 현재 V2와 공용 Runtime·Writer·Validator·Draft 데이터 계약은 계속 활성 코드가 소유한다.

## 복원 규칙

운영 브랜치에 그대로 복사하지 않는다. V1 조사나 비교가 꼭 필요할 때만 별도 브랜치 또는
별도 Unity 프로젝트에서 복원한다.

1. 보관본의 `UnityProject/Assets` 아래 파일을 동일한 상대 경로로 복사한다.
2. `MonsterBasicAttackWorkshopWindow.cs` 안의 공용 제작 모델은 현재
   `Assets/ProjectMT/Editor/MonsterMaker/MonsterBasicAttackAuthoringModel.cs`로 분리되어 있으므로
   중복 선언을 제거한 뒤 컴파일한다.
3. `MonsterMakerWindow.cs` 안의 Preview 제작 타입은 현재
   `Assets/ProjectMT/Editor/MonsterMaker/MonsterMakerPreviewAuthoringTypes.cs`로 분리되어 있으므로
   중복 선언을 제거한 뒤 컴파일한다.
4. `MonsterActiveAttackWorkshopPreview.cs`는 현재 활성 코드의
   `MonsterActiveAttackAuthoringPreview.cs`와 역할이 겹친다. 둘 중 하나만 컴파일한다.
5. 보관된 EditMode 테스트는 V1 소스와 함께 복원할 때만 사용한다.

## 보관 범위

- Monster Maker V1 창
- 기본공격·공격 액티브·효과형 액티브 V1 조립소
- V1 좌표 조정 창과 시각 테마
- V1이 포함되어 있던 시점의 관련 EditMode 테스트 원본

`MANIFEST.sha256`은 이 README를 포함한 보관 파일의 무결성 확인용이다.