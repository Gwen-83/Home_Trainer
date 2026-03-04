using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SessionReplay : MonoBehaviour
{
    public string fileName = "last_session.json";
    [Serializable]
    class StateContainer { public List<SimulationState> items = new List<SimulationState>(); }

    List<SimulationState> points = new List<SimulationState>();
    int index = 0;
    bool playing = false;

    public event Action<SimulationState> OnReplayTick;

    public void LoadFromDisk()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Replay file not found: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            var container = JsonUtility.FromJson<StateContainer>(json);
            points = container != null ? container.items : new List<SimulationState>();
            Debug.Log($"Loaded {points.Count} replay points");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load replay: {ex.Message}");
        }
    }

    public void StartReplay()
    {
        if (points == null || points.Count == 0) LoadFromDisk();
        index = 0;
        playing = points.Count > 0;
    }

    public void StopReplay()
    {
        playing = false;
    }

    void FixedUpdate()
    {
        if (!playing || points == null || index >= points.Count) return;

        // advance by the delta time of the next point
        var next = points[index];
        OnReplayTick?.Invoke(next);
        index++;
        if (index >= points.Count) playing = false;
    }
}
