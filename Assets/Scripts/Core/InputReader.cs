using System.Collections.Generic;
using UnityEngine;

public enum CommandKind { None, Move, Wait, UseItem, Descend, Restart, Cancel, Skill, ToggleHelp, Meta, Language, Mute, VolumeDown, VolumeUp, Records }

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

    // 키 상태와 시계를 바꿔 끼울 수 있게 해 둔다(봇 모드의 입력 자체 검사가 가짜 키보드로 같은 코드를 돌린다).
    public System.Func<KeyCode, bool> KeyHeld = Input.GetKey;
    public System.Func<KeyCode, bool> KeyPressed = Input.GetKeyDown;
    public System.Func<float> Now = () => Time.time;
    static readonly KeyCode[] SlotKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    // 누르고 있는 이동 키(MoveKeys의 번호)를 누른 순서대로 쌓는다. 맨 뒤가 지금 반복되는 키.
    // 나중에 누른 키를 떼면 먼저 누르고 있던 키로 돌아가 끊기지 않고 이어서 움직인다.
    readonly List<int> held = new();
    // CancelRepeat로 멈춘 뒤 아직 떼지 않은 키. 뗐다가 다시 눌러야 다시 움직인다.
    readonly HashSet<int> stopped = new();
    float nextRepeatTime;
    bool repeated;        // 마지막으로 누른 뒤 반복 이동이 한 번이라도 나갔는지
    float stopCueTime = -1f; // 멈춘 키를 계속 누르고 있으면 이 시각에 "멈췄다"는 표시를 띄운다. 없으면 음수.
    bool cueGiven;        // 이번 멈춤을 이미 알렸는지(멈춘 키를 모두 뗄 때까지 유지)

    public PlayerCommand Read()
    {
        if (KeyPressed(KeyCode.Escape)) return new PlayerCommand(CommandKind.Cancel);
        if (KeyPressed(KeyCode.R)) return new PlayerCommand(CommandKind.Restart);
        if (KeyPressed(KeyCode.Tab)) return new PlayerCommand(CommandKind.ToggleHelp);
        if (KeyPressed(KeyCode.U)) return new PlayerCommand(CommandKind.Meta);
        if (KeyPressed(KeyCode.T)) return new PlayerCommand(CommandKind.Records);
        if (KeyPressed(KeyCode.L)) return new PlayerCommand(CommandKind.Language);
        if (KeyPressed(KeyCode.M)) return new PlayerCommand(CommandKind.Mute);
        if (KeyPressed(KeyCode.LeftBracket) || KeyPressed(KeyCode.Minus)) return new PlayerCommand(CommandKind.VolumeDown);
        if (KeyPressed(KeyCode.RightBracket) || KeyPressed(KeyCode.Equals)) return new PlayerCommand(CommandKind.VolumeUp);
        if (KeyPressed(KeyCode.X)) return new PlayerCommand(CommandKind.Skill);
        if (KeyPressed(KeyCode.H)) return new PlayerCommand(CommandKind.UseItem, slot: 0) { FromPotionKey = true };
        for (int i = 0; i < SlotKeys.Length; i++)
            if (KeyPressed(SlotKeys[i])) return new PlayerCommand(CommandKind.UseItem, slot: i);
        if (KeyPressed(KeyCode.F) || KeyPressed(KeyCode.Return) || KeyPressed(KeyCode.KeypadEnter))
            return new PlayerCommand(CommandKind.Descend);
        if (KeyPressed(KeyCode.Space) || KeyPressed(KeyCode.Period) || KeyPressed(KeyCode.Keypad5))
            return new PlayerCommand(CommandKind.Wait);

        return ReadMove();
    }


    PlayerCommand ReadMove()
    {
        int pressed = -1;
        for (int i = 0; i < MoveKeys.Length; i++)
        {
            var key = MoveKeys[i].key;
            bool justDown = KeyPressed(key);
            if (!justDown && !KeyHeld(key))
            {
                held.Remove(i);
                stopped.Remove(i);
                continue;
            }
            // 새로 누른 키. 다른 명령 키와 같은 프레임에 눌려 GetKeyDown을 놓친 키도 여기서 잡는다.
            if (justDown) { stopped.Remove(i); held.Remove(i); }
            else if (stopped.Contains(i) || held.Contains(i)) continue;
            held.Add(i);
            pressed = i;
        }
        if (stopped.Count == 0)
        {
            stopCueTime = -1f;
            cueGiven = false;
        }

        if (pressed >= 0)
        {
            repeated = false;
            nextRepeatTime = Now() + RepeatDelay;
            return new PlayerCommand(CommandKind.Move, MoveKeys[pressed].dir);
        }

        // 반복하던 키를 뗐어도 아직 누르고 있는 키가 있으면 그 키가 박자를 이어받는다.
        if (held.Count == 0 || Now() < nextRepeatTime) return new PlayerCommand(CommandKind.None);
        repeated = true;
        nextRepeatTime = Now() + RepeatInterval;
        return new PlayerCommand(CommandKind.Move, MoveKeys[held[^1]].dir);
    }

    // 새 몬스터를 발견하거나 맞는 등 눈여겨볼 일이 생기면 꾹 누르기 이동을 멈춘다.
    // 누르고 있던 키는 뗐다가 다시 눌러야 다시 움직인다.
    // 반복 이동이 실제로 끊겼으면 true. 막 누른 키였다면, 계속 누르고 있을 때 ConsumeStopCue가 알려준다.
    public bool CancelRepeat()
    {
        bool wasRepeating = held.Count > 0 && repeated;
        held.Clear();
        repeated = false;
        bool anyDown = false;
        for (int i = 0; i < MoveKeys.Length; i++)
        {
            if (!KeyHeld(MoveKeys[i].key)) continue;
            stopped.Add(i);
            anyDown = true;
        }
        // 한 번의 멈춤에는 표시도 한 번만: 같은 턴에 멈출 일이 겹쳐도(제단을 밟자 몬스터가 나타남 등) 다시 알리지 않는다.
        if (wasRepeating)
        {
            cueGiven = true;
            stopCueTime = -1f;
        }
        else if (anyDown && !cueGiven && stopCueTime < 0f)
        {
            stopCueTime = Now() + RepeatDelay;
        }
        return wasRepeating;
    }

    // 멈춘 뒤에도 이동 키를 계속 누르고 있어 "키가 안 먹는" 것처럼 보일 때 한 번 true를 돌려준다.
    public bool ConsumeStopCue()
    {
        if (stopCueTime < 0f || Now() < stopCueTime) return false;
        stopCueTime = -1f;
        cueGiven = true;
        return stopped.Count > 0;
    }
}
