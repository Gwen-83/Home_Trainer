using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moteur de simulation - Orchestre BLE, Physique, et Parcours
/// C'est LE module qui devient le cœur de Unity
/// 
/// Chaque tick :
/// - Lit la puissance BLE
/// - Appelle Physics.Update()
/// - Récupère la pente du parcours
/// - Envoie les commandes FTMS
/// - Enregistre haute fréquence (SessionRecorder)
/// - Émet l'event OnTick avec l'état courant
/// </summary>
public class SimulationEngine : MonoBehaviour
{
    public Config config;
    public PhysicsEngine physics;
    public BleService bleService;
    public GPXReader gpxReader;

    public event Action<string> OnStopped;
    public event Action<SimulationState> OnTick;
    public event Action OnStarted;
    public event Action<string> OnError;

    bool running = false;
    SimulationMode mode = SimulationMode.Free;
    double targetPower = 150.0;
    double targetSpeedKmh = 20.0;
    double elapsed = 0.0;

    // BLE-derived power
    double lastBlePower = 0.0;

    // Recording
    public SessionRecorder recorder;

    // PID Controller pour mode ConstantSpeed
    private PIDController pidSpeedController;

    // Mode Training
    private TrainingPlan trainingPlan;
    private int currentTrainingStep = 0;
    private double elapsedInStep = 0.0;

    // Cadence
    private double filteredCadence = 0.0;
    private const double CadenceFilterTau = 0.3; // seconds

    // Timing
    private System.Diagnostics.Stopwatch sessionStopwatch;

    // OfflineReplay
    private List<SimulationState> replayData = new List<SimulationState>();
    private int replayIndex = 0;
    private double replaySpeed = 1.0;

    void Awake()
    {
        if (config == null) config = new Config();
        if (physics == null) physics = new PhysicsEngine(config);
        if (pidSpeedController == null)
            pidSpeedController = new PIDController(config.PID_Kp, config.PID_Ki, config.PID_Kd);
        if (recorder == null)
            recorder = GetComponent<SessionRecorder>();
        if (sessionStopwatch == null)
            sessionStopwatch = new System.Diagnostics.Stopwatch();
    }

    void Start()
    {
        if (bleService == null)
            bleService = UnityEngine.Object.FindFirstObjectByType<BleService>();

        if (gpxReader == null)
            gpxReader = UnityEngine.Object.FindFirstObjectByType<GPXReader>();

        if (bleService != null)
        {
            bleService.OnPowerReceived += (p) => { lastBlePower = p; };
            bleService.OnConnected += () =>
            {
                // start free mode automatically when device connects
                if (!running)
                    StartSimulation(SimulationMode.Free);
            };
        }
    }

    public void StartSimulation(SimulationMode simMode, double targetPowerW = 150.0, double targetSpeed = 20.0)
    {
        if (running) return;

        this.mode = simMode;
        this.targetPower = targetPowerW;
        this.targetSpeedKmh = targetSpeed;
        running = true;
        elapsed = 0.0;
        currentTrainingStep = 0;
        elapsedInStep = 0.0;
        filteredCadence = 0.0;
        replayIndex = 0;

        physics.Reset();
        pidSpeedController.Reset();
        recorder?.StartRecording();

        sessionStopwatch.Restart();
        OnStarted?.Invoke();

        Debug.Log($"SimulationEngine démarré en mode {simMode}");
    }

    public void StartTraining(TrainingPlan plan)
    {
        if (running) return;

        if (plan == null || plan.Steps.Count == 0)
        {
            OnError?.Invoke("Plan d'entraînement vide");
            return;
        }

        trainingPlan = plan;
        currentTrainingStep = 0;
        elapsedInStep = 0.0;

        StartSimulation(SimulationMode.Training, plan.Steps[0].Power, 20.0);
        Debug.Log($"Entraînement démarré: {plan.Name}");
    }

    public void StartOfflineReplay(List<SimulationState> playbackData, double speed = 1.0)
    {
        if (running) return;

        if (playbackData == null || playbackData.Count == 0)
        {
            OnError?.Invoke("Aucune donnée de replay");
            return;
        }

        replayData = new List<SimulationState>(playbackData);
        replayIndex = 0;
        replaySpeed = Math.Max(0.1, Math.Min(4.0, speed));

        physics.Reset();
        mode = SimulationMode.OfflineReplay;
        running = true;
        elapsed = 0.0;

        sessionStopwatch.Restart();
        OnStarted?.Invoke();

        Debug.Log($"Replay offline démarré: {replayData.Count} points à {replaySpeed}x");
    }

    public void StopSimulation()
    {
        if (!running) return;

        running = false;
        sessionStopwatch.Stop();
        recorder?.StopAndSave();
        OnStopped?.Invoke("Simulation arrêtée");

        Debug.Log($"Simulation arrêtée après {elapsed:F1}s");
    }

    void FixedUpdate()
    {
        if (!running) return;

        // Skip if BLE required but not connected and not simulating
        if (bleService != null && !bleService.IsConnected && !bleService.simulateInEditor)
            return;

        double dt = Time.fixedDeltaTime;
        dt = System.Math.Clamp(dt, config.MinDeltaTime, config.MaxDeltaTime);
        if (dt < config.SoftMinDeltaTime) dt = config.SoftMinDeltaTime;

        elapsed += dt;

        double slope = 0.0;
        if (gpxReader != null && gpxReader.distances.Count > 0)
            slope = gpxReader.GetSlopeAtDistance((float)physics.DistanceCumuleeMetres);

        // Clamp slope
        slope = System.Math.Clamp(slope, -0.06, 0.06);

        var state = new SimulationState
        {
            Mode = mode,
            DeltaTime = dt,
            SessionElapsedSeconds = elapsed,
            Pente = slope
        };

        if (mode == SimulationMode.OfflineReplay)
        {
            // Mode replay: charge les données depuis l'enregistrement
            if (replayIndex >= replayData.Count)
            {
                running = false;
                OnStopped?.Invoke("Replay terminé");
                return;
            }

            var replayState = replayData[replayIndex];
            state.VitesseMs = replayState.VitesseMs;
            state.DistanceMetres = replayState.DistanceMetres;
            state.PuissanceWatts = replayState.PuissanceWatts;
            state.Cadence = replayState.Cadence;

            replayIndex++;
        }
        else
        {
            // Mode simulation normal: utilise la physique
            double appliedPower = ComputePowerForMode(dt, slope);
            state.PuissanceWatts = appliedPower;

            // Met à jour la physique
            physics.Update(appliedPower, slope, dt);

            state.VitesseMs = physics.Vitesse;
            state.DistanceMetres = physics.DistanceCumuleeMetres;

            // Calcul cadence filtrée
            double rawCadence = CalculateRawCadence();
            filteredCadence = FilterCadence(rawCadence, dt);
            state.Cadence = filteredCadence;

            // Update training state
            if (mode == SimulationMode.Training && trainingPlan != null && trainingPlan.Steps.Count > 0)
            {
                elapsedInStep += dt;
                var step = trainingPlan.Steps[currentTrainingStep];

                if (elapsedInStep >= step.Duration)
                {
                    elapsedInStep -= step.Duration;
                    if (currentTrainingStep < trainingPlan.Steps.Count - 1)
                    {
                        currentTrainingStep++;
                        step = trainingPlan.Steps[currentTrainingStep];
                    }
                }

                state.CurrentTrainingStep = currentTrainingStep;
                state.TotalTrainingSteps = trainingPlan.Steps.Count;
                state.RemainingTimeInStep = System.Math.Max(0, step.Duration - elapsedInStep);
            }

            // Envoie commandes BLE
                if (bleService != null && bleService.IsConnected)
            {
                try
                {
                    // Send simulation parameters
                    bleService.SendSimulationSet(slope, config.Crr, config.CdA);

                    // Send target power if not in Free mode
                    if (mode != SimulationMode.Free)
                        bleService.SendTargetPower((short)state.PuissanceWatts);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Erreur envoi BLE: {ex.Message}");
                }
            }

            // Enregistrement
            recorder?.RecordState(state);
        }

        // update accessible property
        CurrentState = state;

        // Émet l'event
        OnTick?.Invoke(state);
    }

    private double ComputePowerForMode(double dt, double slope)
    {
        switch (mode)
        {
            case SimulationMode.Free:
                return lastBlePower;

            case SimulationMode.ConstantPower:
                return targetPower;

            case SimulationMode.ConstantSpeed:
                {
                    double targetSpeedMs = targetSpeedKmh / 3.6;
                    double resistivePower = physics.CalculerPuissanceResistive(targetSpeedMs, slope);
                    double correction = pidSpeedController.Update(targetSpeedMs, physics.Vitesse, dt);
                    return System.Math.Max(0, resistivePower + correction);
                }

            case SimulationMode.Training:
                {
                    if (trainingPlan == null || trainingPlan.Steps.Count == 0)
                        return targetPower;

                    var step = trainingPlan.Steps[currentTrainingStep];
                    return step.Power;
                }

            default:
                return 0;
        }
    }

    private double CalculateRawCadence()
    {
        if (physics.Vitesse < 0.1)
            return 0;

        double circumference = 2 * System.Math.PI * config.Rw;
        double wheelRpm = (physics.Vitesse / circumference) * 60.0;

        return wheelRpm;
    }

    private double FilterCadence(double rawCadence, double dt)
    {
        if (dt <= 0) return filteredCadence;

        double alpha = 1.0 - System.Math.Exp(-dt / CadenceFilterTau);
        filteredCadence += alpha * (rawCadence - filteredCadence);

        return filteredCadence;
    }

    public bool IsRunning => running;
    public SimulationState CurrentState { get; private set; }
}
