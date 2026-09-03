using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.ToolHub
{
    public sealed class ProjectMTToolHubWindow : EditorWindow // 핵심 제작 도구 진입점
    {
        public const string MenuPath = "JC Tool/Tool Hub";
        private const string StylePath =
            "Assets/ProjectMT/Editor/ToolHub/ProjectMTToolHubWindow.uss";
        private const string IconRoot =
            "Assets/ProjectMT/Editor/ToolHub/Icons/";
        private const string MonsterMakerPath = "JC Tool/Monster/Monster Maker";

        private static readonly CoreTool MonsterMaker = new CoreTool(
            "몬스터 제작",
            "Monster Maker V2",
            "모델·애니메이션부터 능력치·공격 배정·VFX/SFX까지 제작하고 Live Preview로 검증합니다.",
            MonsterMakerPath,
            "ToolIcon_MonsterMaker.png");

        private static readonly CoreTool[] CoreTools =
        {
            new CoreTool(
                "몬스터 공격 제작", "공격 조립소",
                "기본공격·공격 액티브·효과형 액티브를 한 창의 3개 탭에서 조립하고 Preview합니다.",
                "JC Tool/Monster/기본공격 조립소",
                "ToolIcon_AttackWorkshop.png"),
            new CoreTool(
                "몬스터 밸런스", "몬스터 능력치 표",
                "44종의 체력·공격·방어·공속과 기본/액티브 DPS를 한 표에서 비교·수정합니다.",
                "JC Tool/Monster/Monster Balance Table",
                "ToolIcon_MonsterBalance.png"),
            new CoreTool(
                "원정대 밸런스", "웨이브 표",
                "1~100단계의 웨이브 수·적 수·구성 비율과 HP/공격/방어 배율을 조정합니다.",
                "JC Tool/Balance/Expedition Wave Table",
                "ToolIcon_ExpeditionWave.png"),
            new CoreTool(
                "원정대 밸런스", "적 리스트 표",
                "원정대 적 13종의 역할·체력·공격·방어·공속·이속·사거리를 한 표에서 수정합니다.",
                "JC Tool/Balance/Expedition Enemy Table",
                "ToolIcon_ExpeditionEnemy.png"),
            new CoreTool(
                "스킬 제작", "군단장 스킬 제작소",
                "공격·버프·디버프의 판정·수치·VFX/SFX를 조립하고 실제 캐스팅을 Preview합니다.",
                "JC Tool/Commander/군단장 스킬 제작소",
                "ToolIcon_CommanderSkill.png"),
            new CoreTool(
                "전투 밸런스", "전투 튜닝 테이블",
                "공용 타격감·피격 반응·사거리와 MainBattle 5종 AI 판단값을 조정합니다.",
                "JC Tool/Combat/전투 튜닝 테이블",
                "ToolIcon_CombatTuning.png"),
            new CoreTool(
                "군단의 역습", "육각 성 생성기",
                "테마·방어층·Seed를 정해 육각 성 구조를 생성하고 배치·공략 경로를 검증합니다.",
                "JC Tool/군단의 역습 육각/성 생성기",
                "ToolIcon_HexCastle.png"),
            new CoreTool(
                "사운드 제작", "SFX Manager",
                "영역별 SfxCue를 검색하고 AudioClip·볼륨·피치·2D/3D·동시 재생값을 관리합니다.",
                "JC Tool/Audio/SFX Manager",
                "ToolIcon_SfxManager.png")
        };

        private static readonly CoreTool[] GeneralTools =
        {
            new CoreTool(
                "범용 맵 도구", "Stage Map Slicer",
                "원본 Scene의 회전 사각형/육각 영역을 잘라 독립 Stage Prefab으로 생성합니다.",
                "JC Tool/Map/Stage Map Slicer",
                "ToolIcon_MapSlicer.png"),
            new CoreTool(
                "범용 Preview", "VFX 미리보기",
                "VFX Prefab을 격리 공간에서 재생하며 속도·반복·환경과 10개 카메라 구도를 점검합니다.",
                "JC Tool/VFX/VFX 프리팹 미리보기",
                "ToolIcon_VfxPreview.png"),
            new CoreTool(
                "범용 Preview", "SFX 미리보기",
                "AudioClip을 빠르게 검색·청취하고 후보 바구니와 메모를 정리합니다.",
                "JC Tool/오디오/SFX 미리보기",
                "ToolIcon_SfxPreview.png")
        };

        private HashSet<string> availableMenus;
        private Label statusLabel;

        [MenuItem(MenuPath, false, 0)]
        public static void OpenWindow()
        {
            var window = GetWindow<ProjectMTToolHubWindow>();
            window.titleContent = new GUIContent("ProjectMT Tools");
            window.minSize = new Vector2(1000f, 660f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("ProjectMT Tools");
            minSize = new Vector2(1000f, 660f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.name = "tool-hub-root";
            rootVisualElement.AddToClassList("hub-root");

            availableMenus = DiscoverMenuPaths();
            rootVisualElement.Add(BuildHeader());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "tool-hub-scroll";
            scroll.AddToClassList("hub-scroll");
            scroll.contentContainer.AddToClassList("hub-scroll-content");
            scroll.Add(BuildMonsterMakerCard());
            scroll.Add(BuildCoreTools());
            scroll.Add(BuildGeneralTools());
            rootVisualElement.Add(scroll);
            rootVisualElement.Add(BuildStatusBar());
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("hub-header");

            var titleArea = new VisualElement();
            var eyebrow = new Label("PROJECT MT  /  EDITOR");
            eyebrow.AddToClassList("hub-eyebrow");
            var title = new Label("ProjectMT Tools");
            title.AddToClassList("hub-title");
            var subtitle = new Label("제작과 밸런스 작업에 필요한 핵심 도구만 모았습니다.");
            subtitle.AddToClassList("hub-subtitle");
            titleArea.Add(eyebrow);
            titleArea.Add(title);
            titleArea.Add(subtitle);
            header.Add(titleArea);

            var count = new Label($"핵심  {CoreTools.Length + 1}   ·   범용  {GeneralTools.Length}");
            count.AddToClassList("hub-count");
            header.Add(count);
            return header;
        }

        private VisualElement BuildMonsterMakerCard()
        {
            var available = availableMenus.Contains(MonsterMaker.MenuPath);
            var card = new VisualElement();
            card.name = "monster-maker-card";
            card.AddToClassList("hub-main-card");

            var accent = new VisualElement();
            accent.AddToClassList("hub-main-accent");
            card.Add(accent);

            var iconArea = new VisualElement();
            iconArea.AddToClassList("hub-main-icon-area");
            iconArea.Add(BuildToolIcon(MonsterMaker, "hub-main-tool-icon"));
            card.Add(iconArea);

            var content = new VisualElement();
            content.AddToClassList("hub-main-content");
            var category = new Label(MonsterMaker.Category);
            category.AddToClassList("hub-card-category");
            var title = new Label(MonsterMaker.Title);
            title.AddToClassList("hub-main-title");
            var description = new Label(MonsterMaker.Description);
            description.AddToClassList("hub-main-description");
            content.Add(category);
            content.Add(title);
            content.Add(description);

            var tags = new VisualElement();
            tags.AddToClassList("hub-tags");
            tags.Add(MakeTag("모델·애니메이션"));
            tags.Add(MakeTag("능력치"));
            tags.Add(MakeTag("기본공격·액티브"));
            tags.Add(MakeTag("Live Preview"));
            content.Add(tags);
            card.Add(content);

            var action = new VisualElement();
            action.AddToClassList("hub-main-action");
            var open = MakeOpenButton(MonsterMaker, available, true);
            open.name = "open-monster-maker";
            action.Add(open);
            card.Add(action);
            return card;
        }

        private VisualElement BuildCoreTools()
        {
            var section = new VisualElement();
            section.AddToClassList("hub-section");
            var heading = new VisualElement();
            heading.AddToClassList("hub-section-heading");
            var title = new Label("핵심 도구");
            title.AddToClassList("hub-section-title");
            var line = new VisualElement();
            line.AddToClassList("hub-section-line");
            heading.Add(title);
            heading.Add(line);
            section.Add(heading);

            var grid = new VisualElement();
            grid.name = "core-tool-grid";
            grid.AddToClassList("hub-grid");
            foreach (var tool in CoreTools)
            {
                grid.Add(BuildToolCard(tool, availableMenus.Contains(tool.MenuPath)));
            }
            section.Add(grid);
            return section;
        }

        private VisualElement BuildGeneralTools()
        {
            var section = new VisualElement();
            section.AddToClassList("hub-section");
            section.AddToClassList("hub-general-section");
            var heading = new VisualElement();
            heading.AddToClassList("hub-section-heading");
            var title = new Label("범용 도구");
            title.AddToClassList("hub-section-title");
            var line = new VisualElement();
            line.AddToClassList("hub-section-line");
            heading.Add(title);
            heading.Add(line);
            section.Add(heading);

            var grid = new VisualElement();
            grid.name = "general-tool-grid";
            grid.AddToClassList("hub-grid");
            foreach (var tool in GeneralTools)
            {
                grid.Add(BuildToolCard(tool, availableMenus.Contains(tool.MenuPath)));
            }
            section.Add(grid);
            return section;
        }

        private VisualElement BuildToolCard(CoreTool tool, bool available)
        {
            var card = new Button(() => OpenTool(tool)) { text = string.Empty };
            card.AddToClassList("hub-tool-card");
            card.EnableInClassList("hub-tool-card--missing", !available);
            card.tooltip = tool.MenuPath;
            card.SetEnabled(available);

            var iconArea = new VisualElement();
            iconArea.AddToClassList("hub-card-icon-area");
            iconArea.Add(BuildToolIcon(tool, "hub-card-tool-icon"));

            var copy = new VisualElement();
            copy.AddToClassList("hub-tool-copy");
            var category = new Label(tool.Category);
            category.AddToClassList("hub-card-category");
            var title = new Label(tool.Title);
            title.AddToClassList("hub-tool-title");
            copy.Add(category);
            copy.Add(title);

            var description = new Label(tool.Description);
            description.AddToClassList("hub-tool-description");
            var launch = new Label(available ? "열기  ›" : "메뉴 없음");
            launch.AddToClassList("hub-tool-launch");

            copy.Add(description);
            copy.Add(launch);
            card.Add(iconArea);
            card.Add(copy);
            return card;
        }

        private static VisualElement BuildToolIcon(CoreTool tool, string sizeClass)
        {
            var frame = new VisualElement();
            frame.AddToClassList("hub-tool-icon");
            frame.AddToClassList(sizeClass);
            frame.pickingMode = PickingMode.Ignore;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconRoot + tool.IconFileName);
            var image = new Image
            {
                image = texture,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.AddToClassList("hub-tool-icon-image");
            frame.Add(image);
            frame.EnableInClassList("hub-tool-icon--missing", texture == null);
            return frame;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("hub-status-bar");
            statusLabel = new Label("도구를 선택하면 새 Editor 창으로 열립니다.");
            statusLabel.name = "tool-hub-status";
            statusLabel.AddToClassList("hub-status");
            bar.Add(statusLabel);
            return bar;
        }

        private Button MakeOpenButton(CoreTool tool, bool available, bool primary)
        {
            var button = new Button(() => OpenTool(tool)) { text = primary ? "몬스터 메이커 열기  ›" : "열기  ›" };
            button.userData = tool.MenuPath;
            button.SetEnabled(available);
            button.AddToClassList(primary ? "hub-main-button" : "hub-open-button");
            return button;
        }

        private void OpenTool(CoreTool tool)
        {
            if (EditorApplication.ExecuteMenuItem(tool.MenuPath))
            {
                SetStatus($"{tool.Title} 창을 열었습니다.", false);
                return;
            }

            SetStatus($"{tool.Title} 메뉴를 찾지 못했습니다. 스크립트 컴파일 상태를 확인하세요.", true);
        }

        private void SetStatus(string message, bool error)
        {
            statusLabel.text = message;
            statusLabel.EnableInClassList("hub-status--error", error);
        }

        private static Label MakeTag(string text)
        {
            var tag = new Label(text);
            tag.AddToClassList("hub-tag");
            return tag;
        }

        private static HashSet<string> DiscoverMenuPaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (var item in method.GetCustomAttributes(typeof(MenuItem), false).OfType<MenuItem>())
                {
                    if (!item.validate && !string.IsNullOrWhiteSpace(item.menuItem)) paths.Add(item.menuItem.Trim());
                }
            }
            return paths;
        }

        private sealed class CoreTool
        {
            public CoreTool(
                string category,
                string title,
                string description,
                string menuPath,
                string iconFileName)
            {
                Category = category;
                Title = title;
                Description = description;
                MenuPath = menuPath;
                IconFileName = iconFileName;
            }

            public string Category { get; }
            public string Title { get; }
            public string Description { get; }
            public string MenuPath { get; }
            public string IconFileName { get; }
        }
    }
}
