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

    // Wheel circumference
    private double Rw => config.Rw;

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

        // Clamp deltaTime pour stabilité numérique à 50 Hz
        deltaTime = Math.Clamp(deltaTime, config.MinDeltaTime, config.MaxDeltaTime);

        // Clamp pente
        pente = Math.Clamp(pente, -0.06, 0.06);

        // Calcul accélération
        double acceleration = CalculerAcceleration(puissanceWatts, pente);

        // Limitation accélération/décélération irréaliste
        acceleration = Math.Clamp(acceleration, config.MaxDeceleration, config.MaxAcceleration);

        // Gestion roue libre : si puissance nulle et vitesse > 0, permettre décélération naturelle
        if (puissanceWatts <= 0 && vitesse > 0)
        {
            // Décélération due aux résistances seulement
            acceleration = Math.Max(acceleration, config.MaxDeceleration);
        }

        // Sécurité NaN
        if (double.IsNaN(acceleration) || double.IsInfinity(acceleration))
        {
            acceleration = 0.0;
        }

        // Intégration vitesse
        vitesse += acceleration * deltaTime;
        if (vitesse < 0) vitesse = 0;

        // Sécurité NaN pour vitesse
        if (double.IsNaN(vitesse) || double.IsInfinity(vitesse))
        {
            vitesse = 0.0;
        }

        // Intégration distance
        distanceCumuleeMetres += vitesse * deltaTime;

        // Sécurité NaN pour distance
        if (double.IsNaN(distanceCumuleeMetres) || double.IsInfinity(distanceCumuleeMetres))
        {
            distanceCumuleeMetres = 0.0;
        }

        return vitesse;
    }

    /// <summary>
    /// Calcule l'accélération instantanée basée sur la puissance fournie et les forces résistantes
    /// </summary>
    public double CalculerAcceleration(double puissanceWatts, double pente)
    {
        // Calcul force gravité
        double Fg = config.Masse * g * pente;

        // Calcul résistance roulement
        double Frr = config.Masse * g * config.Crr;

        // Calcul résistance aérodynamique
        double Fa = 0.5 * config.Rho * config.CdA * vitesse * vitesse;

        // Forces résistantes totales
        double Fres = Fg + Frr + Fa;

        // Sécurité NaN pour forces
        if (double.IsNaN(Fres) || double.IsInfinity(Fres))
        {
            Fres = 0.0;
        }

        // Vitesse effective pour éviter division par zéro
        double vEff = Math.Max(vitesse, 0.01);

        // Force propulsive
        double Fprop = puissanceWatts / vEff;

        // Gestion inertie équivalente
        double meqProj = config.Masse + config.Ieq;

        // Protection contre division par zéro pour masse équivalente
        if (meqProj <= 0 || double.IsNaN(meqProj) || double.IsInfinity(meqProj))
        {
            meqProj = config.Masse;
        }

        // Accélération
        double acceleration = (Fprop - Fres) / meqProj;

        // Sécurité NaN finale
        if (double.IsNaN(acceleration) || double.IsInfinity(acceleration))
        {
            acceleration = 0.0;
        }

        return acceleration;
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

        // Sécurité NaN
        if (double.IsNaN(Fres) || double.IsInfinity(Fres))
        {
            Fres = 0.0;
        }

        double puissanceResistive = Fres * vitesseMs;

        // Sécurité NaN
        if (double.IsNaN(puissanceResistive) || double.IsInfinity(puissanceResistive))
        {
            puissanceResistive = 0.0;
        }

        return puissanceResistive;
    }

    /// <summary>
    /// Réinitialise l'état physique
    /// </summary>
    public void Reset()
    {
        vitesse = 0.0;
        distanceCumuleeMetres = 0.0;
    }

}
