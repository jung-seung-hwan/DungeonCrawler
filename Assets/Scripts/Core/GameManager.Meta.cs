using UnityEngine;

// 영구 성장: 런이 끝나면 영혼석을 얻고, 직업 선택 화면(U 키)에서 영구 강화를 산다.
// PlayerPrefs에 저장한다. 자동 플레이(봇·시뮬레이션)에는 강화를 적용하지 않아 밸런스 통계가 흔들리지 않는다.
public partial class GameManager
{
    const string SoulStonesKey = "SoulStones";
    const string MetaLevelKeyPrefix = "Meta_";

    bool metaOpen;
    int metaCursor;
    int bossKills;
    int soulStonesEarned;
    string metaMessage;

    static int SoulStones => PlayerPrefs.GetInt(SoulStonesKey, 0);
    static string SoulStoneLabel => Ko ? "영혼석" : "Soul Stones";

    static int MetaLevel(MetaUpgradeDef def) =>
        Mathf.Clamp(PlayerPrefs.GetInt(MetaLevelKeyPrefix + def.Id, 0), 0, def.MaxLevel);

    // 이번 런에 실제로 적용되는 강화 수치.
    int MetaBonus(MetaUpgrade id)
    {
        if (autopilotActive) return 0;
        var def = GameData.MetaOf(id);
        return MetaLevel(def) * def.PerLevel;
    }

    // 런이 끝났을 때 한 번 부른다.
    void AwardSoulStones()
    {
        soulStonesEarned = autopilotActive ? 0 : GameData.SoulStonesFor(depth, kills, bossKills);
        if (soulStonesEarned <= 0) return;
        PlayerPrefs.SetInt(SoulStonesKey, SoulStones + soulStonesEarned);
        PlayerPrefs.Save();
    }

    void OpenMeta()
    {
        metaOpen = true;
        metaMessage = null;
        Play(Sfx.UiOpen);
    }

    void HandleMetaInput(PlayerCommand cmd)
    {
        int count = GameData.MetaUpgrades.Length;
        switch (cmd.Kind)
        {
            case CommandKind.Move when cmd.Dir.y != 0:
                metaCursor = (metaCursor - cmd.Dir.y + count) % count;
                metaMessage = null;
                Play(Sfx.UiMove);
                break;
            case CommandKind.Descend:
            case CommandKind.Wait:
                BuyMeta(GameData.MetaUpgrades[metaCursor]);
                break;
            case CommandKind.Cancel:
            case CommandKind.Meta:
                metaOpen = false;
                Play(Sfx.UiClose);
                break;
        }
    }

    void BuyMeta(MetaUpgradeDef def)
    {
        int current = MetaLevel(def);
        if (current >= def.MaxLevel)
        {
            metaMessage = Ko ? $"{def.Name}{Josa(def.Name, "은", "는")} 이미 최대 단계다." : $"{def.Name} is already at its maximum level.";
            Play(Sfx.Deny);
            return;
        }
        int cost = def.CostFor(current);
        if (SoulStones < cost)
        {
            metaMessage = Ko ? $"<color=#ff8888>영혼석이 부족하다. ({cost}개 필요)</color>"
                             : $"<color=#ff8888>Not enough Soul Stones. ({cost} needed)</color>";
            Play(Sfx.Deny);
            return;
        }
        PlayerPrefs.SetInt(SoulStonesKey, SoulStones - cost);
        PlayerPrefs.SetInt(MetaLevelKeyPrefix + def.Id, current + 1);
        PlayerPrefs.Save();
        Play(Sfx.Upgrade);
        metaMessage = Ko ? $"<color=#88ff88>{def.Name} {current + 1}단계 강화 완료!</color>"
                         : $"<color=#88ff88>{def.Name} upgraded to level {current + 1}!</color>";
    }

    void DrawMeta(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.8f));
        const float pw = 640, rowH = 64;
        var defs = GameData.MetaUpgrades;
        float ph = 150 + defs.Length * rowH;
        float x = (w - pw) / 2, y = (h - ph) / 2;

        DrawRect(new Rect(x, y, pw, ph), new Color(0.08f, 0.08f, 0.12f, 0.98f));
        DrawRect(new Rect(x, y, pw, 4), new Color(0.6f, 0.85f, 1f));
        GUI.Label(new Rect(x + 24, y + 16, 300, 34), Ko ? "<b>영구 강화</b>" : "<b>Permanent Upgrades</b>", new GUIStyle(textStyle) { fontSize = 26 });
        GUI.Label(new Rect(x + pw - 244, y + 20, 220, 30), $"<color=#99ddff>{SoulStoneLabel} {SoulStones}</color>",
            new GUIStyle(textStyle) { alignment = TextAnchor.MiddleRight });

        var small = new GUIStyle(textStyle) { fontSize = 15 };
        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            int current = MetaLevel(def);
            bool maxed = current >= def.MaxLevel;
            float ry = y + 66 + i * rowH;

            if (i == metaCursor) DrawRect(new Rect(x + 12, ry, pw - 24, rowH - 6), new Color(0.25f, 0.4f, 0.55f, 0.6f));

            string pips = new string('■', current) + new string('□', def.MaxLevel - current);
            GUI.Label(new Rect(x + 24, ry + 4, 380, 26), $"<b>{def.Name}</b>  <color=#99ddff>{pips}</color>", textStyle);
            GUI.Label(new Rect(x + 24, ry + 30, 440, 24),
                $"<color=#aaaaaa>{def.Describe(current)}</color>", small);

            string price = maxed ? $"<color=#888888>{(Ko ? "최대" : "MAX")}</color>"
                : SoulStones >= def.CostFor(current) ? $"<color=#99ddff>{SoulStoneLabel} {def.CostFor(current)}</color>"
                : $"<color=#886666>{SoulStoneLabel} {def.CostFor(current)}</color>";
            GUI.Label(new Rect(x + pw - 184, ry + 14, 160, 28), price, new GUIStyle(textStyle) { alignment = TextAnchor.MiddleRight });
        }

        float fy = y + 66 + defs.Length * rowH;
        if (metaMessage != null) GUI.Label(new Rect(x + 24, fy + 6, pw - 48, 26), metaMessage, textStyle);
        GUI.Label(new Rect(x + 24, fy + 38, pw - 48, 26),
            Ko ? "<color=#888888>W · S  선택     Enter · Space  강화     Esc · U  닫기</color>"
               : "<color=#888888>W · S  Select     Enter · Space  Upgrade     Esc · U  Close</color>", small);
    }
}
