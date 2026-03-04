using System;

/// <summary>
/// Moteur physique pur - Calcule l'accélération, la vitesse et la distance
/// Zéro dépendance BLE, zéro dépendance UI
/// Testable avec des unit tests
/// </summary>
public class PhysicsEngine
{
    private double vitesse = 0.0;
    private double distanceCumuleeMetres = 0.0;
    private readonly Config config;

    // Gravity
    private const double g = 9.81;

    // Wheel circumference à partir du ratio
    private double Rw => config.Rw;
    private double Rapport { get; set; } = 0.335; // À initialiser depuis l'extérieur

    public double Vitesse => vitesse;
    public double DistanceCumuleeMetres => distanceCumuleeMetres;

    public PhysicsEngine(Config cfg)
    {
        config = cfg;
    }

    /// <summary>
    /// Met à jour la représentation physique (vitesse, distance) basée sur la puissance et pente
    /// </summary>
    public double Update(double puissanceWatts, double pente, double deltaTime)
    {
        if (deltaTime <= 0) return vitesse;

        // Clamp pente
        pente = Math.Clamp(pente, -0.06, 0.06);

        // Calcul accélération
        double acceleration = CalculerAcceleration(puissanceWatts, pente);

        // Intégration vitesse
        vitesse += acceleration * deltaTime;
        if (vitesse < 0) vitesse = 0;

        // Intégration distance
        distanceCumuleeMetres += vitesse * deltaTime;

        return vitesse;
    }

    /// <summary>
    /// Calcule l'accélération instantanée basée sur la puissance fournie et les forces résistantes
    /// </summary>
    public double CalculerAcceleration(double puissanceWatts, double pente)
    {
        // Forces résistantes
        double Fg = config.Masse * g * pente;                           // Gravité
        double Frr = config.Masse * g * config.Crr;                     // Roulement
        double Fa = 0.5 * config.Rho * config.CdA * vitesse * vitesse; // Aérodynamique
        double Fres = Fg + Frr + Fa;

        // Vitesse effective pour éviter division par zéro
        double vEff = Math.Max(vitesse, 0.5);

        // Force propulsive
        double Fprop = puissanceWatts / vEff;

        // Moment d'inertie équivalent ramenée à l'axe arrière
        double meqProj = config.Masse + config.Ieq / Math.Pow(Rw * Rapport, 2);

        // Accélération
        return (Fprop - Fres) / meqProj;
    }

    /// <summary>
    /// Calcule la puissance résistive pour une vitesse donnée (ignorant la puissance motrice)
    /// Utile pour le mode Constant Speed
    /// </summary>
    public double CalculerPuissanceResistive(double vitesseMs, double pente)
    {
        double Fg = config.Masse * g * pente;
        double Frr = config.Masse * g * config.Crr;
        double Fa = 0.5 * config.Rho * config.CdA * vitesseMs * vitesseMs;
        double Fres = Fg + Frr + Fa;
        return Fres * vitesseMs;
    }

    /// <summary>
    /// Réinitialise l'état physique
    /// </summary>
    public void Reset()
    {
        vitesse = 0.0;
        distanceCumuleeMetres = 0.0;
    }

    /// <summary>
    /// Fixe manuellement le ratio de pignon (index dans la couronne)
    /// </summary>
    public void SetGearRatio(double ratio)
    {
        Rapport = ratio;
    }
}
