using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SessionRecorder : MonoBehaviour
{
    [Serializable]
    class StateContainer { public List<SimulationState> items = new List<SimulationState>(); }

    List<SimulationState> buffer = new List<SimulationState>();
    bool recording = false;

    public void StartRecording()
    {
        buffer.Clear();
        recording = true;
    }

    public void RecordState(SimulationState s)
    {
        if (!recording) return;
        // shallow copy minimal data to keep things serializable
        buffer.Add(new SimulationState {
            DeltaTime = s.DeltaTime,
            SessionElapsedSeconds = s.SessionElapsedSeconds,
            VitesseMs = s.VitesseMs,
            DistanceMetres = s.DistanceMetres,
            Pente = s.Pente,
            PuissanceWatts = s.PuissanceWatts
        });
    }

    public void StopAndSave()
    {
        recording = false;
        SaveToDisk();
    }

    void SaveToDisk()
    {
        try
        {
            var path = Path.Combine(Application.persistentDataPath, "last_session.json");
            var container = new StateContainer { items = buffer };
            var json = JsonUtility.ToJson(container);
            File.WriteAllText(path, json);
            Debug.Log($"Session saved: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save session: {ex.Message}");
        }
    }
}
