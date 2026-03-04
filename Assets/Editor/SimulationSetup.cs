using UnityEditor;
using UnityEngine;

public static class SimulationSetup
{
    [MenuItem("Tools/Setup Simulation Manager")] 
    public static void CreateManager()
    {
        var existing = GameObject.Find("SimulationManager");
        if (existing != null)
        {
            Debug.Log("SimulationManager already exists");
            Selection.activeGameObject = existing;
            return;
        }

        var go = new GameObject("SimulationManager");
        go.AddComponent<SimulationEngine>();
        go.AddComponent<BleService>();
        go.AddComponent<SessionRecorder>();
        go.AddComponent<SessionReplay>();
        Selection.activeGameObject = go;
        Debug.Log("Created SimulationManager with required components.");
    }
}
