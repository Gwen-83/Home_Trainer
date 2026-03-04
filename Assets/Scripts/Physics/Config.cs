using UnityEngine;

[System.Serializable]
public class Config
{
    // Paramètres physiques du cycliste
    public double MasseCycliste = 70.0; // kg - masse du cycliste
    public double MasseVelo = 10.0;     // kg - masse du vélo
    public double Masse => MasseCycliste + MasseVelo; // masse totale
    public double CdA = 0.32;        // coefficient aérodynamique
    public double Crr = 0.004;       // coefficient de roulement
    public double Rho = 1.225;       // densité air (kg/m³)
    public double Ieq = 0.15;        // inertie équivalente (kg·m²)
    public double Rw = 0.34;         // rayon roue (m)

    // Limites d'accélération/décélération
    public double MaxAcceleration = 5.0;  // m/s² - accélération maximale réaliste
    public double MaxDeceleration = -9.0; // m/s² - décélération maximale réaliste (freinage)

    // Paramètres PID pour le mode Constant Speed
    public double PID_Kp = 0.5;      // Gain proportionnel
    public double PID_Ki = 0.05;     // Gain intégral
    public double PID_Kd = 0.1;      // Gain dérivé

    // Paramètres de simulation
    public double TargetTickRateMs = 20.0;  // ~50 Hz
    public double MinDeltaTime = 0.001;     // 1ms minimum
    public double MaxDeltaTime = 0.1;       // 100ms maximum
    public double SoftMinDeltaTime = 0.002; // 2ms (anti-jitter)
}