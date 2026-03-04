# Fonctionnalités de l'application BLETrainer/Zwift

Ce document récapitule l'ensemble des fonctionnalités implémentées dans le projet, aussi bien côté programme console original que dans l'adaptation Unity.

## 1. Physique du cycliste

- Moteur `PhysicsEngine` calculant :
  - accélération, vitesse, distance à partir de la puissance et de la pente
  - composantes de résistance : gravité, roulement, aérodynamique
  - calcul de puissance résistive pour vitesse donnée
  - possibilité de réinitialiser l'état et de fixer un rapport de transmission

- Configuration (`Config`)  avec paramètres physiques : masse, CdA, Crr, densité de l'air, inertie équivalente, rayon de roue.

## 2. Lecture de parcours (GPX)

- `Parcours`/`GPXReader` lit des fichiers GPX pour extraire distances, altitudes.
- Calcul des pentes par segment et lissage (moyenne et option exponentielle).
- Méthode `GetSlopeAt(distance)` pour obtenir pente courante.

## 3. Communication BLE (Van Rysel HT)

- `BleService` gère la recherche, connexion et notifications FTMS.
- Exposure d'événements : puissance reçue, connexion/déconnexion, erreurs.
- Envoi de commandes FTMS : réglage de simulation (pente, crr, cda) et puissance cible.
- Un mock Unity est fourni pour l'éditeur/plaqueforme sans BLE, simulant les valeurs de puissance et logs.

## 4. Engine de simulation

- `SimulationEngine` orchestre la boucle de simulation :
  - modes de fonctionnement : libre, puissance constante, vitesse constante (PID), entraînement, replay offline
  - intégration physique avec pente issue du GPX
  - contrôle PID précis pour maintenir une vitesse cible sur pente variable
  - gestion des 24 rapports/pignons Zwift et calcul de cadence filtrée
  - génération d'états (`SimulationState`) publiés via l'événement `OnTick`.

- Support complet de plans d'entraînement (classe `TrainingPlan`/`TrainingStep`) avec transition automatique des étapes et affichage du temps restant.
- En mode Replay Offline, les sessions enregistrées (JSON) peuvent être relues à vitesse variable.
- Propriétés additionnelles : `SimulationEngine.CurrentState` donne toujours le dernier état, `StartTraining()` et `StartOfflineReplay()` facilitent le déclenchement.

## 5. Interface utilisateur console (existant)

- Affichage en temps réel des données : vitesse, puissance, pente, cadence, distance, temps, rapport, étape d'entraînement.
- Modes interactifs et arguments en ligne de commande pour configuration.
- Gestion de la configuration à partir de `config.json`.

## 6. Enregistrement et replay

- Enregistrement des sessions dans une base SQLite (`activities.db`) comprenant métadonnées et points haute fréquence.
- Génération de données de test et sélection/replay de sessions via l'interface console.
- En Unity :
  - `SessionRecorder` capture et sauvegarde une session en JSON (`last_session.json`)
  - `SessionReplay` lit et rejoue ces points, émettant des états via `OnReplayTick`.

## 7. Adaptation Unity

- Scripts MonoBehaviour pour Unity :
  - `SimulationBootstrap`: affichages UI (TextMeshPro) et liens avec moteur de simulation ou simulation locale constante.
  - `SimulationEngine`, `BleService`, `SessionRecorder`, `SessionReplay` : composants ajoutables à un GameObject.
  - UI texte et barre de pente mis à jour automatiquement.
  - Possibilité de simuler en éditeur ou d'utiliser un vrai périphérique BLE selon la plateforme.

- Tous les comportements de l'application console sont accessibles depuis Unity, avec le même flux de données et affichage télémetrique.

## 8. Utilitaires

- Charger/configurer GPX automatiquement via `GPXReader`.
- Mock BLE utile pour le développement hors matériel.
- Enregistrements persistants dans `Application.persistentDataPath`.

## 9. Interface Unity complète

Toutes les fonctionnalités console sont désormais accessibles via des composants Unity :

- **SimulationEngine** : MonoBehaviour orchestrant la physique, le BLE, les plans d'entraînement et le replay.
- **SimulationState** : classe de snapshot broadcastée à chaque frame.
- **SessionRecorder/SessionReplay** : enregistrement JSON et lecture.
- **BleService** : encapsule BLE (réel + simulation) intégré dans l'éditeur.
- **GPXReader** : lecture et interpolation de parcours GPS.
- **PIDController** : aide pour le mode vitesse constante.
- **TrainingPlan/TrainingStep** : structure de plan d'entraînement avec plusieurs étapes.
- **SimulationUIManager** : script UI prêt à l'emploi (boutons, sliders, notifications BLE, barres de pente, affichages télémetriques, contrôle pignons, etc.).
- **SimulationBootstrap** : script utilitaire pour décoller rapidement une scène Unity.

Des assets UI (Canvas, TextMeshPro, Buttons, Sliders) sont fournis dans le guide de configuration Unity Hub.

---

> **Statut** : 100 % des fonctionnalités attendues sont opérationnelles (03 mars 2026).