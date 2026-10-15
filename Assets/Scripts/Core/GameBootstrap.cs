using UnityEngine;

// 씬에 아무것도 배치하지 않아도 Play를 누르면 게임이 시작되도록 GameManager를 자동 생성한다.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Object.FindFirstObjectByType<GameManager>() != null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }
}
