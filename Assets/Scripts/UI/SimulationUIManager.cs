using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Gestionnaire d'interface utilisateur pour BLETrainerUnity
/// Affiche et met à jour les données de la simulation en temps réel
/// </summary>
public class SimulationUIManager : MonoBehaviour
{
    [SerializeField] private SimulationEngine simulationEngine;
    [SerializeField] private BleService bleService;

    // Affichage télémétrie
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI powerText;
    [SerializeField] private TextMeshProUGUI slopeText;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI cadenceText;
    [SerializeField] private TextMeshProUGUI modeText;

    // Affichage training
    [SerializeField] private TextMeshProUGUI trainingStepText;
    [SerializeField] private TextMeshProUGUI trainingProgressText;

    // Affichage BLE
    [SerializeField] private TextMeshProUGUI bleStatusText;
    [SerializeField] private Image bleStatusIndicator;

    // Boutons contrôle
    [SerializeField] private Button startFreeButton;
    [SerializeField] private Button startPowerButton;
    [SerializeField] private Button startSpeedButton;
    [SerializeField] private Button startTrainingButton;
    [SerializeField] private Button stopButton;

    // Sliders
    [SerializeField] private Slider powerSlider;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private TextMeshProUGUI powerValueText;
    [SerializeField] private TextMeshProUGUI speedValueText;



    // Slope bar
    [SerializeField] private Image slopeBarFill;
    private const float MaxAbsSlope = 0.06f; // 6%

    void Start()
    {
        if (simulationEngine == null)
            simulationEngine = FindFirstObjectByType<SimulationEngine>();

        if (bleService == null)
            bleService = FindFirstObjectByType<BleService>();

        if (simulationEngine != null)
        {
            simulationEngine.OnTick += HandleSimulationTick;
            simulationEngine.OnStarted += () => UpdateButtonStates(false);
            simulationEngine.OnStopped += (_) => UpdateButtonStates(true);
        }

        if (bleService != null)
        {
            bleService.OnConnected += UpdateBleStatus;
            bleService.OnDisconnected += (_) => UpdateBleStatus();
        }

        // Wire buttons
        if (startFreeButton != null)
            startFreeButton.onClick.AddListener(() => SimulateMode(SimulationMode.Free));

        if (startPowerButton != null)
            startPowerButton.onClick.AddListener(() => SimulateMode(SimulationMode.ConstantPower));

        if (startSpeedButton != null)
            startSpeedButton.onClick.AddListener(() => SimulateMode(SimulationMode.ConstantSpeed));

        if (startTrainingButton != null)
            startTrainingButton.onClick.AddListener(StartTrainingMode);

        if (stopButton != null)
            stopButton.onClick.AddListener(StopSimulation);

        // Wire sliders
        if (powerSlider != null)
            powerSlider.onValueChanged.AddListener((v) => { 
                if (powerValueText) powerValueText.text = $"{v:F0}W";
                if (simulationEngine != null && simulationEngine.IsRunning && (simulationEngine.CurrentState.Mode == SimulationMode.ConstantPower || simulationEngine.CurrentState.Mode == SimulationMode.Training))
                    simulationEngine.TargetPower = v;
            });

        if (speedSlider != null)
            speedSlider.onValueChanged.AddListener((v) => { 
                if (speedValueText) speedValueText.text = $"{v:F1} km/h";
                if (simulationEngine != null && simulationEngine.IsRunning && simulationEngine.CurrentState.Mode == SimulationMode.ConstantSpeed)
                    simulationEngine.TargetSpeedKmh = v;
            });

        UpdateButtonStates(true);
        UpdateBleStatus();

        // Initialize slider texts
        if (powerSlider != null && powerValueText != null)
            powerValueText.text = $"{powerSlider.value:F0}W";

        if (speedSlider != null && speedValueText != null)
            speedValueText.text = $"{speedSlider.value:F1} km/h";
    }

    void HandleSimulationTick(SimulationState state)
    {
        // Vitesse
        if (speedText != null)
            speedText.text = $"{state.VitesseKmh:F1} km/h";

        // Puissance
        if (powerText != null)
            powerText.text = $"{state.PuissanceWatts:F0} W";

        // Pente
        if (slopeText != null)
            slopeText.text = $"{state.Pente * 100:F1}%";

        // Distance
        if (distanceText != null)
            distanceText.text = $"{state.DistanceMetres / 1000:F2} km";

        // Temps
        if (timeText != null)
        {
            int minutes = (int)(state.SessionElapsedSeconds / 60);
            int seconds = (int)(state.SessionElapsedSeconds % 60);
            timeText.text = $"{minutes:D2}:{seconds:D2}";
        }

        // Cadence
        if (cadenceText != null)
            cadenceText.text = $"{state.Cadence:F0} RPM";

        // Mode
        if (modeText != null)
            modeText.text = $"Mode: {state.Mode}";

        // Training info
        if (state.Mode == SimulationMode.Training)
        {
            if (trainingStepText != null)
                trainingStepText.text = $"Step {state.CurrentTrainingStep + 1}/{state.TotalTrainingSteps}";

            if (trainingProgressText != null)
                trainingProgressText.text = $"{state.RemainingTimeInStep:F1}s remaining";
        }

        // Slope bar
        if (slopeBarFill != null)
        {
            float s = Mathf.Clamp((float)state.Pente, -MaxAbsSlope, MaxAbsSlope);
            float fill = (s + MaxAbsSlope) / (2f * MaxAbsSlope);
            slopeBarFill.fillAmount = fill;

            Color col = Color.green;
            if (fill > 0.5f)
                col = Color.Lerp(Color.yellow, Color.red, (fill - 0.5f) * 2f);
            else if (fill < 0.5f)
                col = Color.Lerp(Color.blue, Color.green, fill * 2f);

            slopeBarFill.color = col;
        }
    }

    void SimulateMode(SimulationMode mode)
    {
        if (simulationEngine == null) return;

        double power = powerSlider != null ? powerSlider.value : 150;
        double speed = speedSlider != null ? speedSlider.value : 20;

        simulationEngine.StartSimulation(mode, power, speed);
    }

    void StartTrainingMode()
    {
        if (simulationEngine == null) return;

        // Exemple plan d'entraînement
        var plan = new TrainingPlan("Quick Workout");
        plan.AddStep(60.0, 150.0);   // 1min warm-up
        plan.AddStep(30.0, 300.0);   // 30s hard
        plan.AddStep(30.0, 100.0);   // 30s recovery
        plan.AddStep(120.0, 200.0);  // 2min steady

        simulationEngine.StartTraining(plan);
    }

    void StopSimulation()
    {
        if (simulationEngine != null)
            simulationEngine.StopSimulation();
    }

    void UpdateButtonStates(bool simulationStopped)
    {
        if (startFreeButton) startFreeButton.interactable = simulationStopped;
        if (startPowerButton) startPowerButton.interactable = simulationStopped;
        if (startSpeedButton) startSpeedButton.interactable = simulationStopped;
        if (startTrainingButton) startTrainingButton.interactable = simulationStopped;
        if (stopButton) stopButton.interactable = !simulationStopped;
    }

    void UpdateBleStatus()
    {
        bool connected = bleService != null && bleService.IsConnected;

        if (bleStatusText != null)
            bleStatusText.text = connected ? "✓ BLE Connected" : "✗ BLE Disconnected";

        if (bleStatusIndicator != null)
            bleStatusIndicator.color = connected ? Color.green : Color.red;
    }
}
