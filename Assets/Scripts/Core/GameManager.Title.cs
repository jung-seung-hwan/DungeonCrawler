using UnityEngine;

// 타이틀 화면, 그리고 직업 선택 화면의 커서(마우스·키보드) 조작.
// 타이틀은 게임을 켰을 때 한 번만 나온다. 죽고 다시 시작하면 직업 선택 화면으로 바로 간다.
// 봇 모드(-autoshot, -simulate)는 입력을 읽지 않고 BeginRun으로 바로 들어가므로 타이틀에 막히지 않는다.
public partial class GameManager
{
    public const string GameTitle = "DungeonTamheomga";

    const float ClassCardWidth = 300, ClassCardHeight = 360, ClassCardGap = 24, ClassCardTop = 170;

    bool showingTitle;
    int classCursor;        // 직업 선택 화면에서 지금 가리키는 카드
    Vector3 lastMousePos;

    void ShowTitle()
    {
        ShowClassSelect();
        showingTitle = true;
    }

    // 아무 키나 마우스 버튼을 누르면 직업 선택으로 넘어간다. 소리 키(M, [ ])는 이미 앞에서 걸러졌다.
    void HandleTitleInput(PlayerCommand cmd)
    {
        if (cmd.Kind == CommandKind.Language) { Loc.Toggle(); Play(Sfx.UiMove); return; }
        if (cmd.Kind == CommandKind.Cancel || !Input.anyKeyDown) return;
        showingTitle = false;
        lastMousePos = Input.mousePosition;
        Play(Sfx.UiOpen);
    }

    // ───────────────────────── 직업 카드 ─────────────────────────

    // HUD와 같은 가상 좌표(높이 720 기준)로 바꾼 마우스 위치.
    static Vector2 VirtualMouse()
    {
        float scale = Screen.height / VirtualHeight;
        return new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
    }

    static Rect ClassCardRect(int index, float w)
    {
        int n = GameData.Classes.Length;
        float total = n * ClassCardWidth + (n - 1) * ClassCardGap;
        return new Rect((w - total) / 2 + index * (ClassCardWidth + ClassCardGap), ClassCardTop, ClassCardWidth, ClassCardHeight);
    }

    static int ClassCardAt(Vector2 point)
    {
        float w = Screen.width / (Screen.height / VirtualHeight);
        for (int i = 0; i < GameData.Classes.Length; i++)
            if (ClassCardRect(i, w).Contains(point)) return i;
        return -1;
    }

    // 마우스를 카드 위로 옮기면 커서가 따라가고, 카드를 클릭하면 그 직업으로 시작한다. 시작했으면 true.
    // 마우스가 움직였을 때만 커서를 옮겨서, 가만히 둔 마우스가 키보드로 옮긴 커서를 되돌리지 않게 한다.
    bool HandleClassSelectMouse()
    {
        int hover = ClassCardAt(VirtualMouse());
        if (Input.mousePosition != lastMousePos)
        {
            lastMousePos = Input.mousePosition;
            if (hover >= 0 && hover != classCursor)
            {
                classCursor = hover;
                Play(Sfx.UiMove);
            }
        }
        if (hover < 0 || !Input.GetMouseButtonDown(0)) return false;
        classCursor = hover;
        BeginDebugRun(GameData.Classes[hover].Id);
        return true;
    }

    // ───────────────────────── 그리기 ─────────────────────────

    void DrawTitle(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0.03f, 0.03f, 0.05f));
        DrawRect(new Rect(0, 0, w, 6), new Color(1f, 0.85f, 0.4f, 0.5f));
        DrawRect(new Rect(0, h - 6, w, 6), new Color(1f, 0.85f, 0.4f, 0.5f));

        var titleStyle = new GUIStyle(bigStyle) { fontSize = 84 };
        GUI.Label(new Rect(5, 135, w, 110), $"<b><color=#000000>{GameTitle}</color></b>", titleStyle);
        GUI.Label(new Rect(0, 130, w, 110), $"<b><color=#ffd966>{GameTitle}</color></b>", titleStyle);
        GUI.Label(new Rect(0, 244, w, 36),
            $"<color=#aaaaaa>{Loc.T("던전 탐험가 · 턴제 로그라이크", "A turn-based dungeon roguelike")}</color>",
            new GUIStyle(bigStyle) { fontSize = 22 });

        // 세 직업이 나란히 서서 천천히 오르내린다.
        const float size = 96, gap = 64;
        int n = GameData.Classes.Length;
        float startX = (w - (n * size + (n - 1) * gap)) / 2;
        for (int i = 0; i < n; i++)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 2f + i * 1.3f) * 5f;
            DrawSprite(PixelArt.Get(GameData.Classes[i].ArtKey), new Rect(startX + i * (size + gap), 340 + bob, size, size));
        }

        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f));
        GUI.Label(new Rect(0, 500, w, 44),
            Loc.T("아무 키나 누르거나 마우스를 클릭해 시작", "Press any key or click to start"),
            new GUIStyle(bigStyle) { fontSize = 28 });
        GUI.color = prev;

        var footer = new GUIStyle(bigStyle) { fontSize = 18 };
        // 언어 전환 안내는 바꿀 언어의 글자로 보여준다.
        GUI.Label(new Rect(0, h - 100, w, 30),
            $"<color=#aaaaaa>{Loc.T("L 키: English   M 키: 음소거   [ ] 음량", "L key: 한국어   M key: Mute   [ ] Volume")}</color>", footer);
        int best = PlayerPrefs.GetInt(BestDepthKey, 0);
        if (best > 0)
            GUI.Label(new Rect(0, h - 70, w, 30), $"<color=#ffd966>{Loc.T("최고 기록", "Best")}: {FloorName(best)}</color>", footer);
    }
}
