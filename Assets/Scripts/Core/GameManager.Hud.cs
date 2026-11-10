using System.Collections.Generic;
using UnityEngine;

// HUD, 미니맵, 떠오르는 데미지 숫자, 화면 흔들림.
public partial class GameManager
{
    const float VirtualHeight = 720f;
    const float FloatingTextLife = 0.9f;
    const int MinimapPixelsPerTile = 3;

    class FloatingText
    {
        public Vector3 World;
        public string Text;
        public Color Color;
        public float Born;
        public float Size;
    }

    readonly List<FloatingText> floatingTexts = new();

    GUIStyle textStyle;
    GUIStyle bigStyle;
    GUIStyle floatStyle;
    Texture2D minimapTex;
    Color32[] minimapPixels;

    float shakeTime;
    float shakeDuration;
    float shakeStrength;
    Vector3 shakeOffset;

    // ───────────────────────── 연출 ─────────────────────────

    void AddFloatingText(Vector3 world, string text, Color color, float size = 1f)
    {
        // 같은 자리에 방금 뜬 숫자가 있으면 그 위로 쌓아서 겹치지 않게 한다.
        int stacked = 0;
        foreach (var other in floatingTexts)
            if (Time.time - other.Born < 0.35f && Mathf.Abs(other.World.x - world.x) < 0.8f && other.World.y - world.y < 2f)
                stacked++;

        floatingTexts.Add(new FloatingText
        {
            World = world + new Vector3((float)(rng.NextDouble() - 0.5) * 0.3f, 0.55f + stacked * 0.35f, 0),
            Text = text,
            Color = color,
            Born = Time.time,
            Size = size,
        });
    }

    void Shake(float duration, float strength)
    {
        if (shakeTime <= 0f || strength > shakeStrength) shakeStrength = strength;
        shakeDuration = shakeTime = Mathf.Max(shakeTime, duration);
    }

    void UpdateShake()
    {
        if (shakeTime <= 0f) { shakeOffset = Vector3.zero; shakeStrength = 0f; return; }
        shakeTime -= Time.deltaTime;
        float power = shakeStrength * Mathf.Clamp01(shakeTime / shakeDuration);
        shakeOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0) * power;
    }

    // ───────────────────────── 미니맵 ─────────────────────────

    void RebuildMinimap()
    {
        if (minimapTex == null)
        {
            minimapTex = new Texture2D(MapWidth, MapHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            minimapPixels = new Color32[MapWidth * MapHeight];
        }
        UpdateMinimap();
    }

    void UpdateMinimap()
    {
        if (minimapTex == null || map == null) return;

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                var p = new Vector2Int(x, y);
                Color32 c = new(0, 0, 0, 0);
                if (map.IsExplored(p))
                {
                    c = map.Get(p) switch
                    {
                        TileType.Wall => new Color32(110, 104, 128, 255),
                        TileType.Stairs => new Color32(255, 215, 70, 255),
                        TileType.Altar => new Color32(150, 220, 255, 255),
                        _ => map.IsVisible(p) ? new Color32(80, 80, 100, 255) : new Color32(48, 48, 60, 255),
                    };
                }
                minimapPixels[y * MapWidth + x] = c;
            }
        }
        foreach (var item in items)
            if (map.IsExplored(item.Pos)) minimapPixels[item.Pos.y * MapWidth + item.Pos.x] = new Color32(90, 200, 255, 255);
        foreach (var (pos, trap) in traps)
            if (trap.Revealed && map.IsExplored(pos)) minimapPixels[pos.y * MapWidth + pos.x] = new Color32(255, 120, 60, 255);
        if (merchant != null && map.IsExplored(merchant.Pos))
            minimapPixels[merchant.Pos.y * MapWidth + merchant.Pos.x] = new Color32(255, 230, 120, 255);
        foreach (var enemy in enemies)
            if (map.IsVisible(enemy.Pos))
                minimapPixels[enemy.Pos.y * MapWidth + enemy.Pos.x] = enemy.IsBoss ? new Color32(255, 120, 255, 255) : new Color32(255, 70, 70, 255);
        minimapPixels[player.Pos.y * MapWidth + player.Pos.x] = new Color32(255, 255, 255, 255);

        minimapTex.SetPixels32(minimapPixels);
        minimapTex.Apply();
    }

    // ───────────────────────── HUD ─────────────────────────

    void OnGUI()
    {
        textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
        bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter, richText = true };
        floatStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        float scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = Screen.width / scale;
        const float h = VirtualHeight;

        if (galleryMode)
        {
            DrawGallery(w, h);
            return;
        }
        if (choosingClass)
        {
            DrawClassSelect(w, h);
            DrawSoundStatus(w);
            return;
        }
        if (player == null) return;

        DrawEnemyHealthBars(scale);
        DrawFloatingTexts(scale);
        DrawStatusPanel();
        DrawMinimap(w);
        DrawBossBar(w);
        DrawLog(w, h);
        DrawHelp(w, h);
        if (shopOpen) DrawShop(w, h);
        if (gameOver) DrawGameOver(w, h);
        DrawSoundStatus(w);
    }

    void DrawStatusPanel()
    {
        DrawRect(new Rect(10, 10, 400, 244), new Color(0, 0, 0, 0.55f));
        string floorLabel = IsBossFloor ? $"<color=#ffcc44>{FloorName(depth)} ({(Ko ? "보스" : "Boss")})</color>" : FloorName(depth);
        string classHex = ColorUtility.ToHtmlStringRGB(playerClass.Color);
        FitLabel(new Rect(22, 16, 380, 26),
            $"<b>{floorLabel}</b>   <color=#{classHex}>{playerClass.Name}</color> Lv.{level}   XP {xp}/{XpToNextLevel}", textStyle);

        DrawBar(new Rect(22, 46, 356, 18), player.Hp, player.MaxHp,
            Color.Lerp(new Color(0.85f, 0.15f, 0.15f), new Color(0.3f, 0.8f, 0.3f), (float)player.Hp / player.MaxHp),
            $"HP {Mathf.Max(0, player.Hp)} / {player.MaxHp}");

        FitLabel(new Rect(22, 74, 380, 26), Ko
            ? $"공격 {player.Attack}   방어 {player.Defense}   <color=#ffd966>골드 {gold}</color>   처치 {kills}"
            : $"ATK {player.Attack}   DEF {player.Defense}   <color=#ffd966>Gold {gold}</color>   Kills {kills}", textStyle);

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < GameData.Consumables.Length; i++)
        {
            var kind = GameData.Consumables[i];
            int n = Count(kind);
            string color = n > 0 ? "#ffffff" : "#666666";
            sb.Append($"<color={color}>[{i + 1}]{GameData.ShortName(kind)} {n}</color>  ");
        }
        FitLabel(new Rect(22, 104, 380, 26), sb.ToString().TrimEnd(), textStyle);

        string skillState = SkillReady
            ? $"<color=#88ff88>{(Ko ? "준비 완료" : "Ready")}</color>"
            : Ko ? $"<color=#999999>{skillCooldown}턴 남음</color>" : $"<color=#999999>{skillCooldown} {(skillCooldown == 1 ? "turn" : "turns")} left</color>";
        string stealth = IsHidden ? $"   <color=#99ff99>{(Ko ? "은신 중" : "Hidden")} ({stealthTurns})</color>" : "";
        FitLabel(new Rect(22, 134, 380, 26), $"<color=#{classHex}>[X] {playerClass.SkillName}</color>  {skillState}{stealth}", textStyle);

        var small = new GUIStyle(textStyle) { fontSize = 15 };
        string[] labels = Ko ? new[] { "무기", "갑옷", "반지" } : new[] { "Weapon", "Armor", "Ring" };
        for (int i = 0; i < 3; i++)
        {
            var eq = GetEquipped((EquipSlot)i);
            string text = eq == null ? $"<color=#666666>{(Ko ? "없음" : "none")}</color>" : $"{eq.ColoredName} <color=#999999>{eq.Describe()}</color>";
            FitLabel(new Rect(22, 166 + i * 26, 380, 24), $"<color=#888888>{labels[i]}</color>  {text}", small);
        }
    }

    void DrawClassSelect(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0.03f, 0.03f, 0.05f));
        GUI.Label(new Rect(0, 50, w, 60), "<b>DUNGEON CRAWLER</b>", bigStyle);
        GUI.Label(new Rect(0, 110, w, 36), Ko ? "<color=#aaaaaa>직업을 선택하세요 (숫자 키 1 ~ 3)</color>" : "<color=#aaaaaa>Choose your class (number keys 1 ~ 3)</color>", new GUIStyle(bigStyle) { fontSize = 20 });

        const float cw = 300, ch = 360, gap = 24;
        float total = GameData.Classes.Length * cw + (GameData.Classes.Length - 1) * gap;
        float startX = (w - total) / 2;
        var body = new GUIStyle(textStyle) { fontSize = 16, wordWrap = true };
        var line = new GUIStyle(textStyle) { fontSize = 16 };

        for (int i = 0; i < GameData.Classes.Length; i++)
        {
            var c = GameData.Classes[i];
            string hex = ColorUtility.ToHtmlStringRGB(c.Color);
            float x = startX + i * (cw + gap), y = 170;

            DrawRect(new Rect(x, y, cw, ch), new Color(0.1f, 0.1f, 0.14f, 0.95f));
            DrawRect(new Rect(x, y, cw, 4), c.Color);
            DrawSprite(PixelArt.Get(c.ArtKey), new Rect(x + cw - 64, y + 10, 48, 48));
            string keyHint = Ko ? $"{i + 1}번 키" : $"Key {i + 1}";
            GUI.Label(new Rect(x + 20, y + 16, cw - 40, 34), $"<color=#{hex}><b>{c.Name}</b></color>   <color=#888888>{keyHint}</color>",
                new GUIStyle(textStyle) { fontSize = 26 });
            int hp = c.Hp + MetaBonus(MetaUpgrade.Vitality), atk = c.Attack + MetaBonus(MetaUpgrade.Might), def = c.Defense + MetaBonus(MetaUpgrade.Guard);
            FitLabel(new Rect(x + 20, y + 62, cw - 40, 26), Ko
                ? $"체력 {hp}   공격 {atk}   방어 {def}   치명 {c.Crit * 100:0}%"
                : $"HP {hp}   ATK {atk}   DEF {def}   Crit {c.Crit * 100:0}%", line);
            FitLabel(new Rect(x + 20, y + 104, cw - 40, 26), Ko
                ? $"<color=#{hex}>스킬: {c.SkillName}</color>  <color=#888888>(쿨다운 {c.SkillCooldown}턴)</color>"
                : $"<color=#{hex}>Skill: {c.SkillName}</color>  <color=#888888>({c.SkillCooldown}-turn cooldown)</color>", line);
            GUI.Label(new Rect(x + 20, y + 132, cw - 40, 90), $"<color=#cccccc>{c.SkillDesc}</color>", body);
            GUI.Label(new Rect(x + 20, y + 234, cw - 40, 26), $"<color=#{hex}>{(Ko ? "특성" : "Passive")}</color>", body);
            GUI.Label(new Rect(x + 20, y + 262, cw - 40, 80), $"<color=#cccccc>{c.Passive}</color>", body);
        }

        var footer = new GUIStyle(bigStyle) { fontSize = 18 };
        // 언어 전환 안내는 바꿀 언어의 글자로 보여준다.
        GUI.Label(new Rect(0, h - 100, w, 30), Ko
            ? $"<color=#99ddff>영혼석 {SoulStones}</color>   <color=#aaaaaa>U 키: 영구 강화   T 키: 기록   L 키: English   M 키: 음소거   [ ] 음량</color>"
            : $"<color=#99ddff>Soul Stones {SoulStones}</color>   <color=#aaaaaa>U key: Upgrades   T key: Records   L key: 한국어   M key: Mute   [ ] Volume</color>", footer);
        int best = PlayerPrefs.GetInt(BestDepthKey, 0);
        if (best > 0)
            GUI.Label(new Rect(0, h - 70, w, 30), $"<color=#ffd966>{(Ko ? "최고 기록" : "Best")}: {FloorName(best)}</color>", footer);

        if (metaOpen) DrawMeta(w, h);
        if (recordsOpen) DrawRecords(w, h);
    }

    void DrawMinimap(float w)
    {
        if (minimapTex == null) return;
        float mw = MapWidth * MinimapPixelsPerTile, mh = MapHeight * MinimapPixelsPerTile;
        var rect = new Rect(w - mw - 16, 16, mw, mh);
        DrawRect(new Rect(rect.x - 6, rect.y - 6, mw + 12, mh + 12), new Color(0, 0, 0, 0.6f));
        GUI.DrawTexture(rect, minimapTex);
    }

    // 조작법은 첫 층에서만 펼쳐 두고, 이후에는 Tab으로 열고 닫는다.
    void DrawHelp(float w, float h)
    {
        var style = new GUIStyle(textStyle) { fontSize = 16 };
        if (!showHelp)
        {
            DrawRect(new Rect(w - 130, h - 38, 120, 28), new Color(0, 0, 0, 0.5f));
            GUI.Label(new Rect(w - 120, h - 35, 110, 24), Ko ? "<color=#999999>Tab  조작법</color>" : "<color=#999999>Tab  Controls</color>", style);
            return;
        }
        string help = Ko
            ? "이동  WASD · 방향키\n대각선  Q E Z C\n대기  Space\n스킬  X\n아이템  1 ~ 4 (물약 H)\n계단  F\n소리  M · [ ]\n<color=#777777>Tab  접기</color>"
            : "Move  WASD · Arrows\nDiagonal  Q E Z C\nWait  Space\nSkill  X\nItems  1 ~ 4 (Potion H)\nStairs  F\nSound  M · [ ]\n<color=#777777>Tab  Hide</color>";
        const float hw = 220, hh = 192;
        DrawRect(new Rect(w - hw - 10, h - hh - 10, hw, hh), new Color(0, 0, 0, 0.6f));
        GUI.Label(new Rect(w - hw + 2, h - hh - 4, hw - 12, hh), $"<color=#aaaaaa>{help}</color>", style);
    }

    // 다친 몬스터 머리 위에 작은 체력 바를 띄운다. 보스는 화면 위쪽 큰 바로 보여주므로 제외.
    void DrawEnemyHealthBars(float scale)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsShown || enemy.IsBoss || enemy.Hp >= enemy.MaxHp) continue;
            float top = 0.55f * enemy.transform.lossyScale.y;
            var screen = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * top);
            float x = screen.x / scale, y = (Screen.height - screen.y) / scale;
            const float bw = 34, bh = 5;
            float ratio = Mathf.Clamp01((float)enemy.Hp / enemy.MaxHp);
            DrawRect(new Rect(x - bw / 2 - 1, y - bh - 1, bw + 2, bh + 2), new Color(0, 0, 0, 0.8f));
            DrawRect(new Rect(x - bw / 2, y - bh, bw * ratio, bh),
                Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.95f, 0.8f, 0.2f), ratio));
        }
    }

    void DrawBossBar(float w)
    {
        if (boss == null || boss.IsDead || !bossSeen) return;
        const float bw = 420;
        float x = (w - bw) / 2;
        GUI.Label(new Rect(x, 8, bw, 26), $"<b><color=#ffcc44>{boss.DisplayName}</color></b>",
            new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter });
        DrawBar(new Rect(x, 36, bw, 16), boss.Hp, boss.MaxHp, new Color(0.8f, 0.2f, 0.6f), null);
    }

    void DrawLog(float w, float h)
    {
        const float lineHeight = 21;
        float logHeight = MaxLogLines * lineHeight + 14;
        DrawRect(new Rect(10, h - logHeight - 10, Mathf.Min(660, w - 250), logHeight), new Color(0, 0, 0, 0.5f));
        var style = new GUIStyle(textStyle) { fontSize = 16 };
        var prev = GUI.color;
        for (int i = 0; i < log.Count; i++)
        {
            // 오래된 줄일수록 흐리게. 색이 들어간 줄도 함께 흐려지도록 GUI.color의 투명도를 쓴다.
            int age = log.Count - 1 - i;
            GUI.color = new Color(1f, 1f, 1f, age == 0 ? 1f : age < 3 ? 0.8f : 0.5f);
            FitLabel(new Rect(22, h - logHeight + i * lineHeight - 4, Mathf.Min(660, w - 250) - 20, 24), log[i], style);
        }
        GUI.color = prev;
    }

    void DrawGameOver(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.6f));
        var small = new GUIStyle(bigStyle) { fontSize = 22 };
        GUI.Label(new Rect(0, h / 2 - 110, w, 60), "<b><color=#ff5555>GAME OVER</color></b>", bigStyle);
        GUI.Label(new Rect(0, h / 2 - 50, w, 36), Ko
            ? $"지하 {depth}층 · Lv.{level} · 처치 {kills} · {turn}턴"
            : $"Floor {depth} · Lv.{level} · {kills} {(kills == 1 ? "kill" : "kills")} · {turn} turns", small);
        GUI.Label(new Rect(0, h / 2 - 16, w, 36), Ko ? $"사인: {killedBy}" : $"Slain by {killedBy}", small);
        string record = newRecord
            ? (Ko ? "<color=#ffdd55>★ 최고 기록 갱신! ★</color>" : "<color=#ffdd55>★ New record! ★</color>")
            : $"{(Ko ? "최고 기록" : "Best")}: {FloorName(bestDepth)}";
        GUI.Label(new Rect(0, h / 2 + 22, w, 36), record, small);
        GUI.Label(new Rect(0, h / 2 + 56, w, 36), Ko
            ? $"<color=#99ddff>영혼석 +{soulStonesEarned}</color>  (보유 {SoulStones})"
            : $"<color=#99ddff>Soul Stones +{soulStonesEarned}</color>  (total {SoulStones})", small);
        GUI.Label(new Rect(0, h / 2 + 104, w, 36), Ko ? "R 키를 눌러 다시 시작" : "Press R to restart", small);
    }

    void DrawFloatingTexts(float scale)
    {
        floatingTexts.RemoveAll(t => Time.time - t.Born > FloatingTextLife);
        foreach (var t in floatingTexts)
        {
            float age = (Time.time - t.Born) / FloatingTextLife;
            var screen = cam.WorldToScreenPoint(t.World + Vector3.up * age * 0.9f);
            float x = screen.x / scale, y = (Screen.height - screen.y) / scale;
            floatStyle.fontSize = Mathf.RoundToInt(22 * t.Size);
            var rect = new Rect(x - 180, y - 20, 360, 40); // 긴 장비 이름도 한 줄에 들어가게 넉넉히
            float alpha = 1f - age * age;

            floatStyle.normal.textColor = new Color(0, 0, 0, alpha);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), t.Text, floatStyle);
            floatStyle.normal.textColor = new Color(t.Color.r, t.Color.g, t.Color.b, alpha);
            GUI.Label(rect, t.Text, floatStyle);
        }
    }

    void DrawBar(Rect rect, int value, int max, Color fill, string label)
    {
        float ratio = Mathf.Clamp01((float)value / max);
        DrawRect(rect, new Color(0.2f, 0.05f, 0.05f));
        DrawRect(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), fill);
        if (label != null) GUI.Label(new Rect(rect.x, rect.y - 3, rect.width, 24), $"<b>{label}</b>", textStyle);
    }

    static string FloorName(int floor) => Ko ? $"지하 {floor}층" : $"Floor {floor}";

    // 글이 영역보다 길면 들어갈 때까지 글자 크기를 줄여서 한 줄로 그린다(영어처럼 문장이 길어지는 경우).
    static void FitLabel(Rect rect, string text, GUIStyle style, int minSize = 10)
    {
        int originalSize = style.fontSize;
        bool originalWrap = style.wordWrap;
        style.wordWrap = false;
        var content = new GUIContent(text);
        while (style.fontSize > minSize && style.CalcSize(content).x > rect.width) style.fontSize--;
        GUI.Label(rect, content, style);
        style.fontSize = originalSize;
        style.wordWrap = originalWrap;
    }

    static void DrawSprite(Sprite sprite, Rect rect)
    {
        if (sprite == null) return;
        var tex = sprite.texture;
        var r = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(rect, tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
    }

    // 개발용: 모든 픽셀아트를 한 화면에 늘어놓는다 (-gallery 실행 인자).
    void DrawGallery(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0.12f, 0.12f, 0.16f));
        const float cell = 150, size = 96;
        int columns = Mathf.Max(1, (int)(w / cell));
        int i = 0;
        var label = new GUIStyle(textStyle) { fontSize = 15, alignment = TextAnchor.UpperCenter };
        foreach (var name in PixelArt.Names)
        {
            float x = (i % columns) * cell + (cell - size) / 2, y = (i / columns) * (cell + 10) + 20;
            DrawRect(new Rect(x - 4, y - 4, size + 8, size + 8), new Color(0.2f, 0.2f, 0.26f));
            DrawSprite(PixelArt.Get(name), new Rect(x, y, size, size));
            GUI.Label(new Rect(x - 30, y + size + 6, size + 60, 24), name, label);
            i++;
        }
    }

    static void DrawRect(Rect rect, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
