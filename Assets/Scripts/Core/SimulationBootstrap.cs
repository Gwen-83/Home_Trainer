using UnityEngine;
using UnityEngine.UI; // for Image
using TMPro;

public class SimulationBootstrap : MonoBehaviour
{
    public Transform rider;
    
    // plusieurs textes pour l'UI
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI slopeText;
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI timeText;

    // optional slope bar graphic (fill image)
    public Image slopeBarFill;

    // debug logging from bootstrap itself
    public bool debugLog = false;

    public GPXReader gpxReader;

    private PhysicsEngine physics;
    private BleService bleService;
    public SimulationEngine simulationEngine;
    private double puissanceConstante = 200.0;

    // télémétrie publique
    public double CurrentSpeedMs { get; private set; } = 0.0;
    public double CurrentSpeedKmh { get; private set; } = 0.0;
    public double CurrentSlope { get; private set; } = 0.0;
    public double CurrentDistance { get; private set; } = 0.0;
    public double CurrentPowerWatts { get; private set; } = 0.0;
    public double ElapsedTimeSeconds { get; private set; } = 0.0;

    // slope bar constants
    private const float MaxAbsSlope = 0.06f; // 6% maximum displayed
    private double lastLoggedTime = 0.0;

    void Start()
    {
        var config = new Config();
        physics = new PhysicsEngine(config);

        // try to wire to a SimulationEngine if present
        if (simulationEngine == null)
            simulationEngine = Object.FindFirstObjectByType<SimulationEngine>();

        if (simulationEngine != null)
        {
            simulationEngine.OnTick += HandleEngineTick;
            Debug.Log("SimulationBootstrap bound to SimulationEngine");
        }

        // cache BLE service if available
        bleService = Object.FindFirstObjectByType<BleService>();

        // try to resolve GPX reader if not assigned from inspector
        if (gpxReader == null)
        {
            gpxReader = Object.FindFirstObjectByType<GPXReader>();
            if (gpxReader != null)
                Debug.Log("GPXReader trouvé automatiquement via FindFirstObjectByType");
            else
            {
                // create one automatically so the user can configure it later
                var go = new GameObject("GPXReader");
                gpxReader = go.AddComponent<GPXReader>();
                Debug.LogWarning("Aucun GPXReader trouvé : création automatique d'un GameObject. N'oubliez pas de définir fileName ou d'assigner un vrai lecteur.");
            }
        }

        if (gpxReader != null)
        {
            gpxReader.LoadGPX();
            Debug.Log($"GPX chargé : {gpxReader.distances.Count} points, {gpxReader.elevations.Count} élévations");
        }
    }

    void FixedUpdate()
    {
        // If a SimulationEngine is present, it will call back via HandleEngineTick.
        if (simulationEngine != null && simulationEngine.IsRunning)
            return;

        // if BLE exists and is not connected, and we're not simulating in editor, update UI with current values then bail
        if (bleService != null && !bleService.IsConnected && !bleService.simulateInEditor)
        {
            UpdateUI();
            return;
        }
 
        double dt = Time.fixedDeltaTime;

        float currentDistance = (float)physics.DistanceCumuleeMetres;

        // pente
        float currentSlope = 0f;
        if (gpxReader != null && gpxReader.distances.Count > 1)
            currentSlope = gpxReader.GetSlopeAtDistance(currentDistance);

        // calcul vitesse
        double vitesseMs = physics.Update(puissanceConstante, currentSlope, dt);
        double vitesseKmh = vitesseMs * 3.6;

        // update télémétrie
        CurrentSpeedMs = vitesseMs;
        CurrentSpeedKmh = vitesseKmh;
        CurrentSlope = currentSlope;
        CurrentDistance = physics.DistanceCumuleeMetres;
        CurrentPowerWatts = puissanceConstante;
        ElapsedTimeSeconds += dt;

        rider.Translate(Vector3.forward * (float)vitesseMs * (float)dt);

        UpdateUI();
    }

    void HandleEngineTick(SimulationState state)
    {
        // apply to rider transform
        if (rider != null)
            rider.Translate(Vector3.forward * (float)state.VitesseMs * (float)state.DeltaTime);

        CurrentSpeedMs = state.VitesseMs;
        CurrentSpeedKmh = state.VitesseKmh;
        CurrentSlope = state.Pente;
        CurrentDistance = state.DistanceMetres;
        CurrentPowerWatts = state.PuissanceWatts;
        ElapsedTimeSeconds = state.SessionElapsedSeconds;

        UpdateUI();
    }

    void UpdateUI()
    {
        if (speedText != null)
            speedText.text = $"Vitesse: {CurrentSpeedKmh:F1} km/h";

        if (distanceText != null)
            distanceText.text = $"Distance: {CurrentDistance / 1000f:F2} km";

        if (slopeText != null)
            slopeText.text = $"Pente: {CurrentSlope * 100:F2}%";

        if (powerText != null)
            powerText.text = $"Puissance: {CurrentPowerWatts:F0} W";

        if (timeText != null)
        {
            int minutes = (int)(ElapsedTimeSeconds / 60);
            int seconds = (int)(ElapsedTimeSeconds % 60);
            timeText.text = $"Temps: {minutes:D2}:{seconds:D2}";
        }

        // slope bar
        if (slopeBarFill != null)
        {
            float s = Mathf.Clamp((float)CurrentSlope, -MaxAbsSlope, MaxAbsSlope);
            float fill = (s + MaxAbsSlope) / (2f * MaxAbsSlope);
            slopeBarFill.fillAmount = fill;
            Color col = Color.Lerp(Color.green, Color.red, fill);
            slopeBarFill.color = col;
        }

        // logs
        if (debugLog && ElapsedTimeSeconds - lastLoggedTime >= 1.0)
        {
            lastLoggedTime = ElapsedTimeSeconds;
            Debug.Log($"Sim t={ElapsedTimeSeconds:F1}s v={CurrentSpeedMs:F2}m/s ({CurrentSpeedKmh:F1}km/h) dist={CurrentDistance:F1}m slope={CurrentSlope*100.0:F2}% P={CurrentPowerWatts:F0}W");
        }
    }
}