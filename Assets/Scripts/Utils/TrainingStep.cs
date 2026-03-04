using System;
using System.Collections.Generic;

/// <summary>
/// Représente une étape d'un plan d'entraînement
/// Chaque étape a une durée et une puissance cible
/// </summary>
[Serializable]
public class TrainingStep
{
    public double Duration { get; set; }  // Durée en secondes
    public double Power { get; set; }     // Puissance cible en Watts

    public TrainingStep() { }

    public TrainingStep(double duration, double power)
    {
        Duration = duration;
        Power = power;
    }

    public override string ToString()
    {
        return $"TrainingStep: {Duration:F0}s @ {Power:F0}W";
    }
}

/// <summary>
/// Gestionnaire de plans d'entraînement
/// </summary>
public class TrainingPlan
{
    public string Name { get; set; } = "Training Plan";
    public List<TrainingStep> Steps { get; set; } = new List<TrainingStep>();

    public double TotalDuration
    {
        get
        {
            double total = 0;
            foreach (var step in Steps)
                total += step.Duration;
            return total;
        }
    }

    public TrainingPlan() { }

    public TrainingPlan(string name)
    {
        Name = name;
    }

    public void AddStep(double durationSeconds, double powerWatts)
    {
        Steps.Add(new TrainingStep(durationSeconds, powerWatts));
    }

    public void AddStep(TrainingStep step)
    {
        if (step != null)
            Steps.Add(step);
    }

    public void Clear()
    {
        Steps.Clear();
    }

    public override string ToString()
    {
        return $"TrainingPlan '{Name}': {Steps.Count} steps, {TotalDuration:F0}s total";
    }
}
