using UnityEngine;

/// <summary>
/// Contrôleur PID (Proportionnel-Intégral-Dérivé)
/// Utilisé pour maintenir une vitesse constante en ajustant la puissance
/// </summary>
public class PIDController
{
    public double Kp { get; set; }  // Gain proportionnel
    public double Ki { get; set; }  // Gain intégral
    public double Kd { get; set; }  // Gain dérivé

    private double integral = 0.0;
    private double lastError = 0.0;

    public PIDController(double kp, double ki, double kd)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
    }

    /// <summary>
    /// Calcule la sortie du contrôleur PID
    /// </summary>
    /// <param name="setpoint">Valeur cible (ex: vitesse cible en m/s)</param>
    /// <param name="actual">Valeur actuelle (ex: vitesse actuelle en m/s)</param>
    /// <param name="dt">Delta time en secondes</param>
    /// <returns>Correction de puissance (Watts)</returns>
    public double Update(double setpoint, double actual, double dt)
    {
        if (dt <= 0) return 0;

        double error = setpoint - actual;

        // Terme proportionnel
        double proportional = Kp * error;

        // Terme intégral (accumulation de l'erreur)
        integral += error * dt;
        double integralTerm = Ki * integral;

        // Terme dérivé (taux de changement de l'erreur)
        double derivative = (error - lastError) / dt;
        double derivativeTerm = Kd * derivative;

        lastError = error;

        // Sortie PID
        double output = proportional + integralTerm + derivativeTerm;

        return output;
    }

    /// <summary>
    /// Réinitialise le contrôleur
    /// </summary>
    public void Reset()
    {
        integral = 0.0;
        lastError = 0.0;
    }
}
