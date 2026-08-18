# 군단의 역습 포탑 시각 자산 이식 기록

- 이식일: 2026-08-18
- 원본 프로젝트: `11팀 미니프로젝트/OZCodingProject`
- 사용 범위: 군단의 역습 방어 건물용 헤드·투사체·VFX·SFX
- 원본 대상: SG01 대포, SG06 발리스타, SG21 화염탑의 Lv1~Lv3
- ProjectMT 이름: `CR_Cannon`, `CR_Ballista`, `CR_Fireball`

## 포함 자산

- 헤드 FBX 9개와 발리스타 화살 FBX 1개
- BaseColor, Normal, Metallic, MetallicRoughness, Roughness, Emission 텍스처 60개
- 외부 Material 14개와 FBX 내장 Material 10개
- 독립 헤드 Prefab 9개, 시각 전용 투사체 Prefab 3개, VFX Prefab 10개
- ProjectMT `SfxCue` 5개: 대포 발사, 발리스타 발사·명중, 화염구 발사·폭발
- 원본 VFX의 실제 폐쇄 의존성은 `Assets/ThirdParty/추가에셋/CastleRaidTurretVisuals` 아래에 별도 보존

## 제외·분리 자산

- 11팀 세그먼트 몸체와 전체 세그먼트 Prefab
- 공격 수치 Profile과 세그먼트 공격 Runtime Script
- 원본 세그먼트의 AudioSource·SFX Emitter·Projectile Runtime·재장전 Script
- Vendor 원본은 ProjectMT 폴더로 복제하거나 이름을 바꾸지 않고 ThirdParty 오버레이에 둔다.

군단의 역습에서는 `PF_CR_TurretHead_*` Prefab을 사용한다. 모든 Prefab은 `Joint_BodyMount / YawPivot / PitchPivot / Model / LoadedProjectiles / Muzzle / VFX_Muzzle` 계약으로 다시 구성하며, 발사 방향은 `Muzzle`의 로컬 `+Z`로 통일한다. 발리스타는 Lv1/2/3에 장전 화살 `1/3/6개`, 화염탑은 장전 구체와 머즐 VFX를 포함한다. 머즐 VFX는 기본 비활성이며 실제 공격 시점 제어는 후속 방어 건물 Runtime이 소유한다.

| 종류 | Lv1 | Lv2 | Lv3 |
|---|---|---|---|
| 대포 | `PF_CR_TurretHead_Cannon_Lv1` | `PF_CR_TurretHead_Cannon_Lv2` | `PF_CR_TurretHead_Cannon_Lv3` |
| 발리스타 | `PF_CR_TurretHead_Ballista_Lv1` | `PF_CR_TurretHead_Ballista_Lv2` | `PF_CR_TurretHead_Ballista_Lv3` |
| 화염탑 | `PF_CR_TurretHead_Fireball_Lv1` | `PF_CR_TurretHead_Fireball_Lv2` | `PF_CR_TurretHead_Fireball_Lv3` |

`Prefabs/TurretProjectiles`와 `Prefabs/TurretVfx`는 시각 전용 독립 Prefab이다. 원본 Runtime MonoBehaviour를 포함하지 않으며 ProjectMT 공격 규칙에는 아직 연결하지 않았다.

원본 모델 제작 출처, Vendor 라이선스, 팀 간 재사용 승인은 최종 배포 전에 별도로 확인한다. ThirdParty 오버레이는 Git 제외 대상이므로 팀 전달 시 같은 경로와 `.meta`를 함께 설치해야 한다.
