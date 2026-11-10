using UnityEngine;

public enum CommandKind { None, Move, Wait, UseItem, Descend, Restart, Cancel, Skill, ToggleHelp, Meta, Language, Mute, VolumeDown, VolumeUp }

public struct PlayerCommand
{
    public CommandKind Kind;
    public Vector2Int Dir;
    public int Slot;
    public bool FromPotionKey; // H 키로 들어온 물약 사용(숫자 키 1과 구분하기 위해)

    public PlayerCommand(CommandKind kind, Vector2Int dir = default, int slot = 0)
    {
        Kind = kind;
        Dir = dir;
        Slot = slot;
        FromPotionKey = false;
    }
}

// 키 입력을 게임 명령으로 바꾼다. 이동 키를 꾹 누르면 일정 간격으로 반복 이동한다.
public class InputReader
{
    const float RepeatDelay = 0.25f;
    const float RepeatInterval = 0.09f;

    static readonly (KeyCode key, Vector2Int dir)[] MoveKeys =
    {
        (KeyCode.W, Vector2Int.up), (KeyCode.UpArrow, Vector2Int.up), (KeyCode.Keypad8, Vector2Int.up),
        (KeyCode.S, Vector2Int.down), (KeyCode.DownArrow, Vector2Int.down), (KeyCode.Keypad2, Vector2Int.down),
        (KeyCode.A, Vector2Int.left), (KeyCode.LeftArrow, Vector2Int.left), (KeyCode.Keypad4, Vector2Int.left),
        (KeyCode.D, Vector2Int.right), (KeyCode.RightArrow, Vector2Int.right), (KeyCode.Keypad6, Vector2Int.right),
        (KeyCode.Q, new Vector2Int(-1, 1)), (KeyCode.Keypad7, new Vector2Int(-1, 1)),
        (KeyCode.E, new Vector2Int(1, 1)), (KeyCode.Keypad9, new Vector2Int(1, 1)),
        (KeyCode.Z, new Vector2Int(-1, -1)), (KeyCode.Keypad1, new Vector2Int(-1, -1)),
        (KeyCode.C, new Vector2Int(1, -1)), (KeyCode.Keypad3, new Vector2Int(1, -1)),
    };

    static readonly KeyCode[] SlotKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    KeyCode heldKey = KeyCode.None;
    float nextRepeatTime;

    public PlayerCommand Read()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) return new PlayerCommand(CommandKind.Cancel);
        if (Input.GetKeyDown(KeyCode.R)) return new PlayerCommand(CommandKind.Restart);
        if (Input.GetKeyDown(KeyCode.Tab)) return new PlayerCommand(CommandKind.ToggleHelp);
        if (Input.GetKeyDown(KeyCode.U)) return new PlayerCommand(CommandKind.Meta);
        if (Input.GetKeyDown(KeyCode.L)) return new PlayerCommand(CommandKind.Language);
        if (Input.GetKeyDown(KeyCode.M)) return new PlayerCommand(CommandKind.Mute);
        if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Minus)) return new PlayerCommand(CommandKind.VolumeDown);
        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Equals)) return new PlayerCommand(CommandKind.VolumeUp);
        if (Input.GetKeyDown(KeyCode.X)) return new PlayerCommand(CommandKind.Skill);
        if (Input.GetKeyDown(KeyCode.H)) return new PlayerCommand(CommandKind.UseItem, slot: 0) { FromPotionKey = true };
        for (int i = 0; i < SlotKeys.Length; i++)
            if (Input.GetKeyDown(SlotKeys[i])) return new PlayerCommand(CommandKind.UseItem, slot: i);
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            return new PlayerCommand(CommandKind.Descend);
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.Keypad5))
            return new PlayerCommand(CommandKind.Wait);

        foreach (var (key, dir) in MoveKeys)
        {
            if (Input.GetKeyDown(key))
            {
                heldKey = key;
                nextRepeatTime = Time.time + RepeatDelay;
                return new PlayerCommand(CommandKind.Move, dir);
            }
        }

        if (heldKey != KeyCode.None)
        {
            if (!Input.GetKey(heldKey))
            {
                heldKey = KeyCode.None;
            }
            else if (Time.time >= nextRepeatTime)
            {
                nextRepeatTime = Time.time + RepeatInterval;
                foreach (var (key, dir) in MoveKeys)
                    if (key == heldKey) return new PlayerCommand(CommandKind.Move, dir);
            }
        }

        return new PlayerCommand(CommandKind.None);
    }

    // 몬스터를 발견하는 등 위험할 때 꾹 누르기 이동을 멈춘다.
    public void CancelRepeat() => heldKey = KeyCode.None;
}
