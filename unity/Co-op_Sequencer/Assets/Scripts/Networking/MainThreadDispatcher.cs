using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Minimal main-thread dispatcher. Add to any persistent GameObject,
/// or call Enqueue() — it auto-creates itself if not present.
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private static readonly ConcurrentQueue<Action> _queue = new();

    public static void Enqueue(Action action) => _queue.Enqueue(action);

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        while (_queue.TryDequeue(out var action))
            action?.Invoke();
    }

    // Auto-create if someone calls Enqueue before a scene has one
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("MainThreadDispatcher");
        go.AddComponent<MainThreadDispatcher>();
    }
}
