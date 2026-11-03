# CAHIER DES CHARGES — « Vision vampirique »
### Le sceau du vampire · Partie 5 : « Résous les énigmes et progresse »

> Document destiné à Claude Code. À lire en entier avant toute action, puis à exécuter **une phase à la fois** (section 12).
> Langue de l'interface et des textes : **français**. Langue des commentaires de code : français, courts.

---

## 0. Mode d'emploi (pour la personne, pas pour Claude Code)

1. Copie ce fichier à la racine du projet Unity (même niveau que `Assets`).
2. Unity ouvert, **hors Play**, point **vert** dans Window > MCP for Unity. Lance `claude` dans le dossier du projet.
3. Envoie : `Lis VISION_SPEC.md en entier, puis exécute uniquement la Phase 0 (audit en lecture seule). Réponds en 15 lignes maximum, puis arrête-toi.`
4. Pour chaque phase suivante : `/clear`, puis `Exécute la Phase N de VISION_SPEC.md. Unity hors Play. Réponds en 4 lignes.`
5. Entre deux phases : Play dans l'éditeur (XR Simulation), test à la main, puis passe à la suivante.

---

## 1. Contexte et contraintes

### 1.1 État connu du projet (À VÉRIFIER par inspection, ne pas supposer)
- Unity 6 (6000.6), pipeline **URP-Performant**, AR Foundation + ARCore, XR Simulation dans l'éditeur, nouvel Input System, TextMeshPro, MCP for Unity.
- Scène : `Assets/QuestAR/Scenes/Mission5_Enigme.unity`.
- Scripts existants probables dans `Assets/QuestAR/Scripts/` : `ARPlacementManager`, `HoloPuzzlePanel`, `IndicesFlottants`, `CodePuzzle`, `CarteIndice`, `BandeauHistoire`, `TextesEnigme`, `PuzzleData`, et des outils Editor (`ThemeVampire`, `BuildHoloPanel`…).
- Données : `Assets/QuestAR/Data/Enigme_Musee.asset` (code **4729**, indice, **50 XP**). Prefab de caisse : `Assets/SlashAssetStash/SciFiCrateLowPolyFree[URP]/Prefabs/Crate.prefab`.
- Règles de jeu déjà en place à **conserver** : code 4729, **3 essais**, blocage **10 s** puis 3 nouveaux essais, récompense « plan », carte d'indice, bouton RÉCUPÉRER.
- Thème : rouge sang `#B00020` (lueurs `#FF1030`), noir `#0A0A0A`, blanc os `#EDE6DA`.
- Téléphone de test : **Huawei JKM-LX1 (Y8s), Android 9, ~3,7 Go de RAM, ARCore NON supporté** (« Device is not supported »). Le **mode de repli (section 9) est donc indispensable** pour montrer le jeu sur ce téléphone.
- Dans le build Android, les passes de post-traitement URP sont **retirées** (shaders « stripped »). **Ne pas dépendre du post-traitement.**
- L'ancien indice « lis dans le sens horaire » est **supprimé** : le code se déduit désormais de la **chronologie** (section 3).

### 1.2 Règles de travail (prioritaires)
- **Inspecter avant d'agir.** Si une information essentielle manque, la chercher dans le projet. Ne pas inventer d'architecture.
- **Réutiliser** l'existant. Ne rien recréer sans raison. Modifier le minimum de fichiers, les lister avant de commencer chaque phase.
- **Pas d'`execute_code`** (échoue sur ce projet). Pour agir sur la scène : outils MCP dédiés (objets, composants, matériaux), ou script Editor avec `[MenuItem("QuestAR/...")]` exécuté ensuite.
- **Ne jamais lancer le Play.** Unity doit être hors Play pour modifier ou compiler.
- **Une phase à la fois.** À la fin de chaque phase : sauvegarder la scène, vérifier la console, puis **s'arrêter** et résumer en 4 lignes (créé / modifié / à tester).
- Après 2 échecs sur la même erreur : s'arrêter et montrer l'erreur.
- **Aucun nouveau package** sans demander. Aucun réseau, aucune télémétrie.
- Pas d'emoji dans l'interface (la police TMP par défaut ne les affiche pas) : utiliser des glyphes simples ou des formes UI.
- Remplacer les `FindFirstObjectByType` (obsolète) par des références sérialisées ou `FindAnyObjectByType` mises en cache, **uniquement dans les fichiers modifiés**.

---

## 2. Principes de design

1. **Chaque élément sert le gameplay** : orientation, information, feedback ou ambiance qui renforce la tension. Aucun décor gratuit.
2. **Déduction, pas devinette** : la solution est unique et se déduit uniquement des textes (section 3).
3. **Le joueur bouge pour jouer** : caméra et gyroscope sont nécessaires pour explorer (en repli : gyroscope seul ou glisser du doigt).
4. **Le sceau est le personnage central** : il réagit à tout (découvertes, leurres, erreurs, victoire).
5. **Lisibilité mobile d'abord** : une main, portrait, texte lisible en plein jour.

---

## 3. Contenu narratif (à utiliser tel quel, dans `TextesEnigme.cs` ou `RelicData`)

### 3.1 Reliques authentiques (4) — chiffre du code = chiffre de la relique, ordre = rang chronologique
| Rang | Chiffre | Titre | Fragment |
|---|---|---|---|
| 1 | **4** | Le Premier Sang | « Au commencement, le seigneur Aldric creuse la crypte sous la tour nord et y grave le sceau du clan. » |
| 2 | **7** | La Trahison | « Des hivers plus tard, son intendant le trahit. Le sang d'Aldric scelle la porte de la crypte. » |
| 3 | **2** | L'Héritier | « Après la trahison, l'héritier cache le plan du château derrière le sceau. » |
| 4 | **9** | Le Long Sommeil | « Le clan s'endort. Seul celui qui brise le sceau réveillera la dernière nuit. » |

Code = chiffres dans l'ordre chronologique = **4 7 2 9**.

### 3.2 Leurres (3) — chacun contredit explicitement des reliques authentiques
| Chiffre | Titre | Fragment | Contredit |
|---|---|---|---|
| 1 | Chronique falsifiée | « L'héritier cacha le plan avant même que la crypte ne soit creusée. » | rangs 1 et 3 |
| 5 | Chronique falsifiée | « L'intendant trahit Aldric avant la fondation du clan. » | rangs 1 et 2 |
| 8 | Chronique falsifiée | « Le clan dormait déjà quand Aldric grava le sceau. » | rangs 1 et 4 |

Un leurre ne doit **jamais** être identifiable par sa seule apparence : il l'est par la **contradiction** entre textes.

### 3.3 Placement
- 7 reliques (4 + 3) autour du joueur à 1,5–2,5 m, hauteur des yeux, **ordre spatial mélangé** (jamais dans l'ordre chronologique), réparti sur 360°. Positions déterminées par une graine par session (reproductible en test).
- Chaque relique = prefab `Crate` réduit (≈ 25 cm) + rune + chiffre TMP flottant au-dessus.

### 3.4 Textes du sceau et du bandeau
- Lancement : « La nuit tombe sur la crypte… quelque chose t'observe. »
- Sceau posé : « Un sceau ancien scelle le passage. Seules les reliques du clan peuvent l'ouvrir. »
- Relique authentique découverte : afficher son fragment dans le bandeau.
- Leurre inspecté : afficher son fragment + « Cette chronique sonne faux… »
- Erreur 1 : « Le sceau reste scellé… » · Erreur 2 : « Le sceau te brûle les doigts. Les reliques ont un ordre dans le temps : lis leurs chroniques. » · Erreur 3 : « Les ombres t'encerclent. Attends l'aube… »
- Victoire : « Le sceau se brise. Un cri retentit dans la crypte… »
- Tout le texte est centralisé dans `TextesEnigme.cs`.

---

## 4. Mécanique « Vision vampirique »

### 4.1 Utilisation
- Bouton rond **VISION** en bas à droite (≥ 140 px à 1080 de large), **maintenu** pour activer, relâché pour couper.
- **Énergie** : 6 s maximum, recharge en 8 s quand inactive. Barre fine autour du bouton. Impossible pendant la saisie du code et quand le sceau est verrouillé.
- Indicateur d'énergie lisible sans couleur seule (arc + pourcentage décroissant).

### 4.2 Effets visibles
1. **Runes cachées** : une rune gravée apparaît sur chaque relique dans un rayon de 3 m. Les runes des leurres sont **visuellement équivalentes** à celles des vraies (pas d'indice trivial).
2. **Traces spectrales** : pas de fantômes lumineux au sol qui mènent vers la relique **non découverte** la plus proche. Si aucune relique n'est à l'écran, un chevron sur le bord de l'écran indique la direction.
3. **Connexions** : lignes (`LineRenderer`) entre reliques **déjà découvertes** :
   - ligne **dorée continue** = les deux chroniques sont cohérentes entre elles ;
   - ligne **rouge pointillée + symbole ✕ (forme UI)** = les chroniques se contredisent.
   - Les lignes n'indiquent **pas de direction** : l'ordre doit être déduit des textes.
   - Distinction aussi par le motif (continu / pointillé), jamais par la couleur seule.
4. **Ambiance** : voile sombre rouge sur les bords (image UI plein écran, pas de post-traitement), chuchotements, légère distorsion du réticule.

### 4.3 Découverte d'une relique
- Visée au centre de l'écran **0,8 s** (anneau de progression autour du réticule) **ou** toucher direct sur la relique (alternative accessible).
- Une relique découverte reste marquée (rune pleine + glyphe ✓ pour une authentique **seulement après** résolution ; avant, marque neutre « lue »).
- Compteur « Chroniques lues : X/7 » (le joueur ne sait pas combien sont fausses).

---

## 5. Sceau holographique réactif (`HoloPuzzlePanel` à étendre)

États visuels, pilotés par un composant `SceauVisuel` réutilisant les matériaux existants (changer couleurs / émission via `MaterialPropertyBlock`) :
| Événement | Réaction visuelle | Son |
|---|---|---|
| Sceau posé | Anneau central + 4 segments éteints, légère pulsation | Bourdonnement grave |
| Relique lue | Un segment s'allume (progression), pulsation plus rapide | Note ascendante |
| Leurre lu | Scintillement rouge bref + fissure fine | Murmure dissonant |
| Vision active | Runes du sceau visibles, rotation lente de l'anneau | Souffle |
| Erreur de code | Secousse 0,3 s, une fissure de plus (1 par erreur) | Craquement |
| 3e erreur | Chaînes (formes simples) se referment, anneau verrouillé 10 s + compte à rebours | Chaînes |
| Victoire | Fissures qui s'élargissent, éclatement en particules (≤ 100), lumière, cadenas qui s'ouvre | Éclat + cri lointain |

Toutes les animations durent ≤ 1,5 s (sauf victoire ≤ 3 s), courbes `AnimationCurve`, sans plugin externe.

---

## 6. Logique, états et tentatives

- Créer une classe **C# pure** `EnigmeLogic` (sans `UnityEngine`) : validation du code, compteur d'essais, verrouillage (horloge injectable), ensemble des reliques lues, détection des contradictions entre deux reliques. Elle doit être **testable** en EditMode.
- États (`PuzzleStateMachine`) : `Intro → Exploration → SaisieCode → Verrouille → Resolue`, plus les sous-états `VisionActive` et `GrimoireOuvert` (non exclusifs).
- **3 essais** par série ; après le 3e échec : **10 s** de blocage avec compte à rebours, puis 3 nouveaux essais. « Essais : X/3 » affiché sur le clavier.
- **Un leurre lu ne coûte jamais d'essai.**
- La résolution est **idempotente** : XP (50) et grimoire attribués **une seule fois**, même si on relance.
- Données : `RelicData` (ScriptableObject : id, chiffre, rang, authentique, titre, fragment, sprite de rune). Les 7 assets dans `Assets/QuestAR/Data/Reliques/`. Réutiliser `PuzzleData` pour le code, l'indice et les XP.

---

## 7. Audio et haptique

- `AudioManager` léger (singleton ou référence sérialisée) avec identifiants : `ambiance`, `vision_on`, `vision_off`, `relique_ok`, `leurre`, `fissure`, `chaines`, `eclat`, `page`, `scellage_loop` (hauteur qui monte avec la progression).
- **Ne jamais planter si un clip manque** : si un `AudioClip` est absent, synthétiser un son simple (`AudioClip.Create`) ou ignorer sans erreur.
- Si des fichiers audio sont ajoutés : dossier `Assets/QuestAR/Audio/`, mono pour les effets, Vorbis, boucle d'ambiance < 2 Mo. Lister dans le rapport les noms de fichiers attendus pour que la personne puisse les fournir (sons libres de droits).
- Vibration courte (Android) sur : relique lue, erreur, victoire. Désactivable.
- Les chuchotements importants ont leur **sous-titre** dans le bandeau.

---

## 8. Grimoire interactif et suite de la mission

- Bouton **GRIMOIRE** (petit, haut-droite, ≥ 140 px). Ouvre un livre plein écran, tourne les pages au toucher/boutons (animation de page ≤ 0,4 s).
- Pages :
  1. **Chronique** : les 4 fragments authentiques par ordre chronologique (ceux lus), et après résolution les 3 leurres marqués « chronique falsifiée » (✕).
  2. **Plan du château** : schéma dessiné avec des éléments UI (rectangles et lignes), la crypte et la tour nord marquées ; débloqué à la victoire.
  3. **Indices** : « Indice mission 6 : la clé de la porte de fer est dans la salle du trône, derrière le portrait du seigneur Aldric. » (débloqué à la victoire).
- Fin d'énigme : la carte « Indice trouvé » existante propose « Ajouter au grimoire » ; la page « vole » vers le bouton GRIMOIRE (animation courte).
- **Persistance** : `JsonUtility` dans `Application.persistentDataPath` (ou `ProgressManager` / `PlayerPrefs` s'ils existent déjà : réutiliser) : reliques lues, énigme résolue, XP, pages débloquées, réglages. Bouton caché (triple tap en haut à gauche) pour tout réinitialiser en test.

---

## 9. Autorisations, AR et solutions de repli

Au démarrage, sélectionner automatiquement le mode, dans cet ordre :
| Mode | Condition | Comportement |
|---|---|---|
| A — AR complet | ARCore disponible (`ARSession.CheckAvailability`) | Plans AR, placement par raycast (comportement actuel) |
| B — Caméra + gyroscope | AR non supporté, caméra autorisée | Flux caméra arrière en plein écran (`WebCamTexture` sur `RawImage` derrière l'UI, rotation/miroir corrects en portrait), orientation par `AttitudeSensor` (Input System), sceau posé à 2 m devant au premier toucher, reliques à 1,5–2,5 m autour |
| C — Gyroscope seul | Caméra refusée ou indisponible | Fond sombre gothique, même orientation par gyroscope |
| D — Glisser du doigt | Pas de gyroscope (éditeur, tablette) | Regard au glisser |

- **Permission caméra** : demander `Permission.Camera` (UnityEngine.Android) avant de démarrer ; si refusée → panneau explicatif en français, boutons « Réessayer » et « Continuer sans caméra » (mode C).
- Si `NeedsInstall` : message clair + bouton d'installation (services Google Play pour la RA).
- Le mode A reste **identique** dans l'éditeur (XR Simulation) : ne pas le casser.
- Étiquette discrète du mode actif, visible **seulement** dans les builds de développement.

---

## 10. Accessibilité

- Police ≥ 36 px à la référence 1080×1920 pour le texte courant ; contraste ≥ 4,5:1 (blanc os sur noir).
- **Jamais la couleur seule** : états par forme, icône, motif ou texte (✓, ✕, pointillé, libellés).
- Cibles tactiles ≥ 140 px ; respect de la **zone sûre** (`Screen.safeArea`, encoche) ; jeu possible à une main.
- Réglages (panneau simple, sauvegardés) : **Effets réduits** (supprime secousses, scintillements et pulsations du voile), **Son**, **Vibrations**, **Taille du texte** (normale / grande).
- Photosensibilité : aucun flash > 3 par seconde, même en effets normaux.
- Toute information sonore importante a son équivalent texte.
- Alternative au regard prolongé : le toucher direct.

---

## 11. Performance mobile (cible : 30 FPS stables sur le Y8s)

- Aucune allocation par image dans les `Update` (pas de LINQ, pas de `new` dans les boucles, pas de concaténation de chaînes répétée).
- Pooling des particules et des lignes ; **≤ 4** systèmes de particules actifs, **≤ 100** particules chacun.
- Aucune ombre temps réel ; **1** seule lumière temps réel au plus ; pas de post-traitement.
- Effets de couleur/émission via `MaterialPropertyBlock` ou changements de couleur de matériau d'instance.
- Textures de la caisse (4K) : limiter `Max Size` à **1024** (ou 512) dans leurs réglages d'import, pour la mémoire et le temps de build.
- Instancier les 7 reliques **une fois** et les activer/désactiver.
- Raycasts de visée à fréquence limitée (≤ 20 Hz) et un seul par image.
- Désactiver `Raycast Target` sur tous les textes et images non interactifs (le bandeau a déjà bloqué des clics).
- Journaliser en développement seulement ; pas de `Debug.Log` dans les boucles.

---

## 12. Phases (une seule à la fois ; s'arrêter à la fin de chacune)

**Phase 0 — Audit (lecture seule, aucune modification).**
Inspecter : structure `Assets/QuestAR/`, version de Unity, packages (AR Foundation, ARCore, Input System, TMP, tests), scripts existants (rôle, dépendances, points d'extension), composants de la scène, `ProjectSettings` pertinents, présence de `ProgressManager`/sauvegarde, polices et audio disponibles. Livrer un rapport de **15 lignes max** : ce qui existe / à **réutiliser** / à **modifier** (liste de fichiers) / à **créer** / risques et questions bloquantes. Ne rien modifier.

**Phase 1 — Données, logique, états.**
`RelicData` + 7 assets (section 3), `EnigmeLogic` (pur C#), `PuzzleStateMachine`, mise à jour de `TextesEnigme`, adaptation de `IndicesFlottants` (7 reliques, placement mélangé, graine), retrait de l'indice « sens horaire ». *Validation : Play → 7 reliques autour de soi ; 4729 ouvre le sceau ; 3 essais + blocage 10 s inchangés.*

**Phase 2 — Découverte et Vision.**
Réticule + anneau de visée, toucher direct, bouton VISION et énergie, runes, traces spectrales + chevron, lignes de cohérence/contradiction, voile UI. *Validation : lire deux reliques → ligne dorée ou rouge ✕ conforme à la section 3 ; les runes ne trahissent pas les leurres.*

**Phase 3 — Sceau réactif.**
`SceauVisuel` et table de la section 5 (sans audio). *Validation : chaque événement produit la réaction prévue ; victoire ≤ 3 s.*

**Phase 4 — Audio et haptique.**
`AudioManager` null-safe, sons de la section 7 (synthétiques si absents), vibrations, sous-titres. *Validation : aucun plantage sans fichiers audio.*

**Phase 5 — Grimoire, persistance, fin.**
Grimoire (3 pages), animation d'ajout, sauvegarde, XP idempotent, indice mission 6, reset caché. *Validation : relancer le jeu conserve l'état ; XP donnés une seule fois.*

**Phase 6 — Repli AR et permissions.**
Sélecteur de mode A/B/C/D, permission caméra, `WebCamTexture`, `AttitudeSensor`, écrans d'erreur. *Validation : éditeur = mode A inchangé ; mode B jouable de bout en bout ; refus caméra → mode C.*

**Phase 7 — Accessibilité, réglages, performance.**
Panneau de réglages, zone sûre, tailles, optimisations de la section 11 (import des textures, `FindFirstObjectByType`, `Raycast Target`). *Validation : « Effets réduits » coupe bien secousses et scintillements.*

**Phase 8 — Tests et clôture.**
Si Unity Test Framework est présent : tests EditMode de `EnigmeLogic` (solution unique 4729, erreurs/essais, blocage, idempotence de la récompense, contradictions, aller-retour de sauvegarde). Sinon, liste de vérifications manuelles. Checklist de build Android. Rapport final.

---

## 13. Critères de validation globaux (la personne les vérifie)

- [ ] Le code **4729** est la **seule** solution déductible des textes (section 3), sans l'indice horaire.
- [ ] Les 3 leurres sont **réellement contradictoires** et n'ont pas d'apparence distincte.
- [ ] Les lignes de Vision ne révèlent que cohérence/contradiction, jamais l'ordre.
- [ ] Un leurre lu ne coûte **aucun essai**.
- [ ] 3 essais, blocage 10 s, puis 3 essais, comme avant.
- [ ] Le sceau réagit à toutes les lignes de la section 5.
- [ ] Le plan est ajouté au grimoire, l'indice de la mission 6 est visible, l'XP n'est donné qu'une fois.
- [ ] Mode B jouable sur le Y8s (caméra + gyroscope) ; mode A inchangé dans l'éditeur.
- [ ] Aucune ligne rouge dans la console ; aucun plantage si un son manque.
- [ ] Aucun texte ne dépend de la couleur seule ; « Effets réduits » fonctionne.
- [ ] Le jeu garde ≈ 30 FPS sur le téléphone de test.

---

## 14. Hors périmètre
Nouveaux niveaux, multijoueur, réseau, achats, analytique, refonte de l'architecture existante, nouveaux packages, post-traitement URP, modèles 3D lourds.
