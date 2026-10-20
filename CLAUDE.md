# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6000.x / 2D URP 길드 경영 + 전술 RPG. 기획서: `길드 매니저.pdf` (기능 작업 전 해당 섹션 확인).
한국어로 소통할 것.

## Build & Test

- Unity Editor에서 빌드/실행 (CLI 빌드 없음)
- 테스트 프레임워크 미설정 — 테스트 어셈블리 없음
- 스크립트 수정 후 **반드시 Unity 콘솔 컴파일 에러 확인** (MCP `recompile_scripts` + `get_console_logs` 사용)
- 단일 어셈블리: `Assembly-CSharp.csproj` (`.asmdef` 없음)

## Architecture

### Scene Flow
TitleScene → LobbyScene (persistent GameManager) → DungeonScene → LobbyScene

### Folder Convention
```
Assets/Scripts/AI/           — BattleUnit 서브클래스 + 투사체 (Arrow, Fireball)
Assets/Scripts/Character Scripts/ — Adventurer 데이터 클래스, AdventurerGenerator, LobbyWalking
Assets/Scripts/Data/         — 런타임 데이터 클래스 (Party, QuestData, StageData, TraitData 등)
Assets/Scripts/Managers/     — 싱글톤 매니저 (GameManager, BattleManager, LobbyManager 등)
Assets/Scripts/UI Scripts/   — 팝업 및 UI 컨트롤러
Assets/Editor/               — 에디터 도구 (타일셋 임포터, 프리팹 연결 등)
Assets/Data/                 — ScriptableObject 에셋 (Drops/, Quest/, Stage/, Trait/)
```

### Core Singletons
- **GameManager** — 전역 상태, 씬 전환 시 DontDestroyOnLoad. 길드 자원(gold, reputation), 모험가 목록, 파티, 시설 레벨, 주간/월간 진행 관리
- **BattleManager** — 던전 전투 오케스트레이션. 스테이지별 적 스폰, 승리/패배 판정, 전리품/보상
- **LobbyManager** — 로비 UI 조율, 파티 편성, 시설 업그레이드
- **MapGenerator** — 타일맵 기반 절차적 던전 생성

### Battle System
`BattleUnit` (base) → Ally 5종 (Warrior, Archer, Mage, Healer, Rogue) + Enemy 5종 (Slime, Goblin, Bandit, Orc, Skeleton)

전투 루프: `SearchForTarget()` → `FindTarget()` → 이동/공격 판정. 어그로 시스템으로 타겟 우선순위 결정.
적 전멸 시 보물상자 → 다음 스테이지. 아군 전멸 시 게임오버.
슬라임은 사망 시 3-4개 미니 슬라임으로 분열.

### Trait System (특성)
`TraitData` = 조건(Condition) + 효과(Modifier) 조합.
- Condition 종류: `Always`, `NoAllyNearby`, `SpecificEnemyExists`, `HpBelowPercentage`, `AllyCountBelow`
- Effect 종류: `ModifyAttackPower`, `ModifyMoveSpeed`, `ChangeAI_HideBehindAlly`
- `BattleUnit.CheckTraits()`에서 매 프레임 평가. 새 특성 추가 시 enum + switch case 확장 필요.

### Quest System
`QuestGenerator`가 런타임에 `QuestData` ScriptableObject 인스턴스 생성. 랭크(D~S)별 적 풀, 보상, 추천 레벨 결정.
특수 퀘스트: 구출(isRescueQuest) — 낙오 모험가 구출, 시체 회수(isCorpseQuest) — 사망 모험가 유품 회수.

### Weekly/Monthly Cycle
`PassWeek()` — 부상 회복, 낙오 카운트다운, 선술집 사기 회복.
`MonthlySettlement()` — 급여 지급, 파산 체크.

## Rules

- **밸런스 수치 하드코딩 금지** → ScriptableObject 또는 직렬화 필드로
- `.meta` / `.unity` / `.prefab` 직접 텍스트 수정 금지 → MCP 도구 또는 에디터 스크립트 사용
- 도트 리소스: PPU 16, Filter Point, Compression None, Pixel Perfect Camera
- 특성(Trait)은 반드시 조건(Condition) + 효과(Modifier) 조합으로 구현
- `Scripts/Core` (순수 C# 로직, MonoBehaviour 금지)와 `Scripts/Runtime` (MonoBehaviour, 표현/입력만) 분리 원칙 유지 — 현재 코드는 이 분리가 아직 미적용이므로, 새 코드 작성 시 이 구조를 따를 것
