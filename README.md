# 🧟‍♂️ QuestAR - Jeu Mobile en Réalité Augmentée (AR)

[![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black?logo=unity&logoColor=white)](https://unity.com/)
[![ARCore](https://img.shields.io/badge/AR-ARFoundation%20%2F%20ARCore-blue)](https://developers.google.com/ar)
[![Platform](https://img.shields.io/badge/Platform-Android-green?logo=android&logoColor=white)](https://www.android.com/)
[![Branch](https://img.shields.io/badge/Branch-Negzaoui-orange?logo=git)](https://github.com/kaaboura12/QuestAr/tree/Negzaoui)

**QuestAR** est une expérience mobile immersive en **Réalité Augmentée (AR)** développée sur **Unity** pour les appareils Android supportant **ARCore**. Le projet fusionne le frisson de la survie en tir à la première personne (**FPS AR**) et la réflexion d'un **Escape Game en Réalité Augmentée**.

---

## 🎮 Déroulement du Jeu (Gameplay Flow)

Le joueur progresse à travers plusieurs étapes interactives :

```mermaid
flowchart LR
    A["🔐 Authentification<br/>(VisageID / Profil)"] --> B["📋 Menu Principal<br/>(Session & Choix)"]
    B --> C["🔫 Niveaux 1 à 4 : Survie AR<br/>(Vagues de Zombies)"]
    C --> D["🧩 Mission 5 : Énigme Finale<br/>(Chroniques & Keypad)"]
    D --> E["🏆 Victoire Totale<br/>(Dashboard RPG & Stats)"]
```

1. **Authentification (`VisageID_Auth_Scene`)** :
   - Connexion via reconnaissance faciale ou profil joueur invité.
2. **Menu Principal (`MainMenu`)** :
   - Affichage de l'état du joueur, options audio et lancement des missions.
3. **Missions 1 à 4 : Survie & Combat AR (`ARGameplay`)** :
   - **Détection Stricte du Sol** : Détection des surfaces horizontales réelles (`ARPlaneManager`) pour un ancrage réaliste au sol (aucun zombie flottant ou collé aux murs).
   - **Système d'Armes & Visée** : Armes personnalisées avec visée précise (AIM), tir (FIRE) et gestion des munitions (RELOAD).
   - **Capteur de Luminosité Ambiante** : Mesure en temps réel des Lux de la pièce via `ARCameraManager` pour adapter l'ambiance et les mécaniques d'obscurité.
   - **Flash Tactique (Stun)** : Activation de la torche pour paralyser les créatures avec un temps de recharge stratégique (Cooldown de 10 secondes affiché sur le HUD).
   - **Microphone & Son** : Détection de l'intensité sonore pour réagir aux bruits ambiants.
4. **Mission 5 : L'Énigme Finale (`Mission5_Enigme`)** :
   - Exploration de l'environnement réel pour repérer les chroniques et reliques flottantes.
   - Vision vampirique et déchiffrage du lore ancien.
   - Pavé tactile (Keypad) optimisé en mode paysage pour saisir le code secret et briser le sceau.
5. **Écran de Victoire Totale** :
   - Célébration finale avec lauriers d'or, musique de triomphe, statistiques de la partie et boutons d'action (`REJOUER` / `DÉCONNEXION`).

---

## 🛠️ Architecture & Technologies

- **Moteur** : Unity 2022.3 (C#)
- **AR Framework** : Unity AR Foundation, ARCore XR Plugin (Tracking 6DoF, AR Raycast, Plane Tracking, Light Estimation)
- **Pipeline Graphique** : Built-in Render Pipeline (Shaders optimisés pour mobile)
- **Combat Rig** : Intégration Infima Games Low Poly Shooter Pack
- **Capteurs Matériels Exploités** :
  - Caméra AR & Gyroscope
  - Capteur de lumière ambiante (Lux continu)
  - Torche LED / Flash Android
  - Microphone
  - Moteur haptique (vibrations Android)

---

## 🚀 Installation & Build

### Prérequis :
- **Unity 2022.3 LTS** avec le module **Android Build Support** (OpenJDK, Android SDK & NDK).
- Un smartphone Android compatible **Google Play Services for AR (ARCore)**.

### Étapes :
1. Cloner le projet et basculer sur la branche :
   ```bash
   git clone https://github.com/kaaboura12/QuestAr.git
   cd QuestAr
   git checkout Negzaoui
   ```
2. Ouvrir le projet dans Unity Hub.
3. Aller dans **File > Build Settings...**, basculer la plateforme sur **Android**.
4. Vérifier l'ordre des scènes actives dans le build :
   - `0` : `Assets/VisageID_Auth/Scenes/VisageID_Auth_Scene.unity`
   - `1` : `Assets/_Project/Scenes/MainMenu.unity`
   - `2` : `Assets/_Project/Scenes/ARGameplay.unity`
   - `3` : `Assets/QuestAR/Scenes/Mission5_Enigme.unity`
5. Connecter le smartphone en mode Débogage USB et cliquer sur **Build and Run**.

---

## 📌 Spécificités de la Branche `Negzaoui`

Cette branche regroupe l'ensemble des développements et corrections majeurs :
- ✅ **Intégration complète de la scène `Mission5_Enigme`** et liaison automatique après la victoire du niveau 4.
- ✅ **Fix du Spawner AR** : Détection stricte des plans horizontaux inférieurs pour un spawn propre des zombies sur le sol réel.
- ✅ **Correction des Shaders** : Remplacement des shaders URP incompatibles (rose/magenta) par des shaders Standard optimisés.
- ✅ **Capteur de Lumière & Flash** : Activation continue du `requestedLightEstimation` et ajout d'un cooldown de 10 secondes sur le bouton Flash.
- ✅ **Keypad Landscape** : Repositionnement et adaptation tactile du pavé numérique pour écran horizontal.
- ✅ **Écran de Victoire Totale** : Interface de victoire finale avec reprise de session et retour au menu.
