using System;

public enum SimulationMode
{
    Free,
    ConstantPower,
    ConstantSpeed,
    Training,
    OfflineReplay
}

[Serializable]
public class SimulationState
{
    public SimulationMode Mode;
    public double DeltaTime;
    public double SessionElapsedSeconds;

    public double VitesseMs;
    public double VitesseKmh => VitesseMs * 3.6;
    public double DistanceMetres;
    public double Pente;
    public double PuissanceWatts;
    public double Cadence;
    public int GearIndex;

    // targets
    public double TargetPower;
    public double TargetSpeedKmh;

    // training
    public int CurrentTrainingStep;
    public int TotalTrainingSteps;
    public double RemainingTimeInStep;
}
