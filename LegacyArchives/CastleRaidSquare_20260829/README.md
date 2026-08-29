# 사각 그리드 군단의 역습 Legacy 보관본

- 상태: **Legacy / 현재 게임 미사용**
- 보관일: 2026-08-29
- 목적: 사각 그리드 시스템을 삭제하지 않고, Unity의 컴파일·Resources·Build Settings·실제 플레이 진입에서 완전히 분리한다.
- 현역 구현: `Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant`
- 현역 씬: `Assets/ProjectMT/00_Scenes/03_CastleRaidHex.unity`

## 포함 범위

- 사각 콘텐츠 원본: `UnityProject/Assets/ProjectMT/04_Contents/01_CastleRaid`
- 사각 정식 씬: `UnityProject/Assets/ProjectMT/00_Scenes/02_CastleRaid.unity`
- 사각 Bake 도구: `UnityProject/Assets/ProjectMT/90_Tools/CastleBake`
- 사각 전장 선택 팝업: `UnityProject/Assets/ProjectMT/03_Features/MainBattle/Runtime/CastleRaidGridModeDialog.cs`
- 사각 전용 테스트: `UnityProject/Assets/ProjectMT/99_Tests`
- 사각 AI에서 Hex AI로 옮기던 일회성 동기화 도구: `UnityProject/Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Editor/HexCastleAssaultAIProfileSyncUtility.cs`

## 복원 주의사항

이 보관본은 Unity `Assets` 밖에 있으므로 현재 프로젝트에서는 Import·컴파일·빌드되지 않는다.

HUD와 포탑 외형 등 일부 자산은 현역 Hex 구현에도 같은 GUID로 보존되어 있다. 따라서 이 보관본을 현재 프로젝트의 `Assets`에 그대로 덮어쓰면 GUID 중복이 생길 수 있다. 재사용할 때는 다음 중 하나로 진행한다.

1. 별도 Unity 프로젝트에 `UnityProject/Assets` 아래 구조를 복원한다.
2. 현재 프로젝트에서 재활성화해야 한다면 먼저 Hex 현역 자산과의 GUID 충돌 목록을 확인하고, 별도 브랜치에서 경로·asmdef·SceneCatalog·Build Settings를 다시 연결한다.

이 폴더는 보관 원본이므로 현역 Hex 수정 과정에서 함께 갱신하지 않는다.
