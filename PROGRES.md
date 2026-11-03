# PROGRES — Vision vampirique (reprise possible depuis ce fichier)

Phases 0 à 3 : faites (voir VISION_SPEC.md). Ordre de cette session : 5, 6, 7, 4, 8 — **toutes faites**.
Aucune scène modifiée : tout est créé par le code au lancement (`QuestARSysteme`, démarre seul dans `Mission5_Enigme`).
Compilation : 0 erreur (reste seulement d'anciens avertissements « obsolète » dans des fichiers Editor non touchés).

## Phase 5 — Grimoire, persistance, fin : FAIT
- Créés : `ProgressManager.cs` (JSON `questar_save.json`), `UIOutils.cs`, `GrimoireUI.cs`, `QuestARSysteme.cs`.
- Modifiés : `IndicesFlottants.cs` (reliques lues sauvegardées, `RestaurerLues`), `HoloPuzzlePanel.cs`, `CarteIndice.cs` (« XP déjà obtenus »), `TextesEnigme.cs`.
- Carte finale : indice « La crypte du château renferme le plan » conservé + bouton « AJOUTER AU GRIMOIRE » (la page vole vers GRIMOIRE).
- Indice mission 6 : page 3 du grimoire (débloquée à la victoire). XP : une seule fois, même après redémarrage.
- Reset caché : triple tap en haut à gauche (efface la progression, garde les réglages, recharge la scène).
- Limite : après un redémarrage, le panneau n'est PAS restauré « déjà résolu » ; on peut ressaisir 4729 (sans nouveaux XP).
- Les reliques lues sont sauvegardées d'un Play à l'autre : pour repartir de zéro, triple tap en haut à gauche.

## Phase 6 — Modes A/B/C/D : FAIT
- Créé : `SelecteurMode.cs`. Éditeur : ne fait rien (mode A inchangé). Téléphone : `ARSession.CheckAvailability` ; non supporté → permission caméra (écran « Réessayer / Continuer sans caméra ») → flux `WebCamTexture` (B) ou fond gothique (C) ; sans gyroscope → glisser du doigt (D). Étiquette du mode : builds de développement seulement.
- Sceau posé à 2 m devant au premier toucher (modes B/C/D).
- Non testable sans téléphone : sens du gyroscope (formule `Euler(90,0,0) * (x,y,-z,-w)`), orientation/miroir du flux caméra.

## Phase 7 — Réglages, zone sûre, performance : FAIT
- Créé : `ReglagesUI.cs` (bouton RÉGLAGES sous GRIMOIRE : Effets réduits, Son, Vibrations, Taille du texte ; sauvegardés).
- Effets réduits : coupe secousse du sceau, scintillements (anneau, flash de victoire, tremblement des leurres), pulsation du voile et du réticule.
- Zone sûre (`Screen.safeArea`) : appliquée à mon interface (Vision, Grimoire, Réglages, écrans de mode). Les anciens canvas (clavier, carte, bandeau) ne sont pas modifiés.
- Raycast Target désactivé sur tous les textes non interactifs. Les images décoratives de l'ancienne interface sont laissées telles quelles (elles protègent le placement AR des touchers).
- Textures de la caisse : `maxTextureSize` = 1024 (5 fichiers .meta).
- `FindFirstObjectByType` remplacé dans les fichiers modifiés (`IndicesFlottants`, `HoloPuzzlePanel`, `BandeauHistoire`). Restent seulement dans des scripts Editor.

## Phase 4 — Audio, haptique, sous-titres : FAIT
- `AudioManager.cs` : si un fichier manque, le son est fabriqué par le code (aucun plantage).
- Fichiers audio attendus (facultatifs), dossier **`Assets/QuestAR/Resources/Audio/`** (le nom « Resources » est obligatoire pour que Unity les charge) :
  `ambiance` (boucle < 2 Mo), `scellage_loop` (boucle), `vision_on`, `vision_off`, `relique_ok`, `leurre`, `fissure`, `chaines`, `eclat`, `page`. Format .ogg, mono pour les effets, sons libres de droits.
- Vibrations : relique lue (40 ms), erreur (60 ms), victoire (≈ 0,5 s). Désactivables dans RÉGLAGES.
- Sous-titres : fragments et « Cette chronique sonne faux… » dans le bandeau ; chuchotements de la Vision (affichés une fois).

## Phase 8 — Vérifications manuelles (pas de package de tests)

### Dans l'éditeur (XR Simulation), dans cet ordre
1. Play : la console n'a aucune ligne rouge. Triple tap en haut à gauche pour repartir de zéro.
2. Poser le sceau : 7 reliques tout autour, jamais dans l'ordre du temps ; compteur « Chroniques lues : 0/7 ».
3. Lire (regard 0,8 s ou clic) les 4 vraies : chaque fragment s'affiche ; un segment du sceau s'allume.
4. Lire un leurre : « Cette chronique sonne faux… », scintillement, fissure fine ; **le compteur d'essais ne bouge pas**.
5. Maintenir VISION (ou touche V) : runes, traces au sol, chevron hors écran ; énergie 6 s puis recharge 8 s.
6. Lire deux reliques avec la Vision : ligne dorée continue (cohérentes) ou rouge pointillée + croix (contradiction). Aucune direction indiquée.
7. Ouvrir le clavier : la Vision est impossible. Saisir un mauvais code : secousse + fissure, « Essais : 2/3 ».
8. 3 mauvais codes : chaînes, compte à rebours 10 s, puis 3 nouveaux essais.
9. Saisir 4729 : victoire en moins de 3 s, carte avec « La crypte du château renferme le plan » et « +50 XP ».
10. « AJOUTER AU GRIMOIRE » : la page vole vers GRIMOIRE. Ouvrir le grimoire : 3 pages (chronique, plan, indice mission 6).
11. Stopper puis relancer : les reliques lues sont conservées ; ressaisir 4729 affiche « XP déjà obtenus » (XP = 50, pas 100).
12. RÉGLAGES : Effets réduits (plus de secousse ni scintillement), Son, Vibrations, Taille du texte (bandeau plus grand).
13. Sans aucun fichier audio : des sons synthétiques jouent, aucune erreur.

### Sur le Huawei JKM-LX1 (build)
14. Au lancement : demande de permission caméra ; refuser → écran « Réessayer / Continuer sans caméra ».
15. Caméra autorisée : image de la caméra arrière à l'endroit, qui bouge avec le téléphone (mode B). Si l'image est tournée ou retournée, ou si la vue tourne à l'envers : me le dire.
16. Toucher l'écran : le sceau apparaît à 2 m devant ; jouer de bout en bout.
17. Environ 30 FPS stables ; pas de gel au premier son.

### Checklist de build Android (ne rien changer à la configuration obligatoire)
- Plateforme Android, ARCore coché ; Graphics API OpenGLES3 seul (pas de Vulkan) ; API minimum 26 ; IL2CPP ; ARM64 uniquement.
- Scène `Mission5_Enigme` cochée dans Build Settings (déjà le cas).
- Permissions du manifeste : CAMERA (ajoutée par ARCore) ; VIBRATE (ajoutée automatiquement par `Handheld.Vibrate`).
- Build de développement coché pour voir l'étiquette « Mode X » ; le décocher pour la version finale.
- Vérifier que les shaders « Sprites/Default » sont bien dans le build (utilisés par les hologrammes) ; sinon les ajouter à Always Included Shaders.
- Tester sur le téléphone avec ARCore non supporté (mode de repli) ET, si possible, sur un téléphone compatible ARCore (mode A).
