using System.Collections.Generic;
using UnityEngine;

// Sons du jeu. NE PLANTE JAMAIS si un fichier audio manque : le son est alors fabriqué par le code.
// Pour mettre tes propres sons : place des fichiers dans Assets/QuestAR/Resources/Audio/ avec ces noms :
//   ambiance, vision_on, vision_off, relique_ok, leurre, fissure, chaines, eclat, page, scellage_loop
// (.ogg, mono pour les effets ; la boucle d'ambiance < 2 Mo).
public class AudioManager : MonoBehaviour
{
    const int Freq = 22050;
    static AudioManager instance;

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioSource sfx, ambiance, scellage;
    System.Random hasard = new System.Random(1234);

    void Awake()
    {
        instance = this;
        sfx = NouvelleSource(false);
        ambiance = NouvelleSource(true);
        scellage = NouvelleSource(true);
        ambiance.volume = 0.35f;
        scellage.volume = 0.3f;
    }

    void Start()
    {
        ambiance.clip = Obtenir("ambiance");
        if (ambiance.clip != null) ambiance.Play();
    }

    void OnDestroy() { if (instance == this) instance = null; }

    AudioSource NouvelleSource(bool boucle)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = boucle;
        s.spatialBlend = 0f;   // son 2D
        return s;
    }

    void Update()
    {
        bool muet = !ProgressManager.Data.son;
        sfx.mute = ambiance.mute = scellage.mute = muet;
    }

    // ---------- Utilisation depuis les autres scripts ----------

    public static void Jouer(string id)
    {
        if (instance == null || !ProgressManager.Data.son) return;
        AudioClip c = instance.Obtenir(id);
        if (c != null) instance.sfx.PlayOneShot(c);
    }

    public static void DemarrerScellage()
    {
        if (instance == null) return;
        instance.scellage.clip = instance.Obtenir("scellage_loop");
        if (instance.scellage.clip != null && !instance.scellage.isPlaying) instance.scellage.Play();
    }

    // Plus on lit de reliques, plus la boucle du sceau monte (0 à 1)
    public static void ProgressionScellage(float p)
    {
        if (instance != null) instance.scellage.pitch = 1f + 0.6f * Mathf.Clamp01(p);
    }

    public static void ArreterScellage()
    {
        if (instance != null) instance.scellage.Stop();
    }

    // Vibration courte (Android). Désactivable dans les réglages.
    public static void Vibrer(int millisecondes)
    {
        if (!ProgressManager.Data.vibrations) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (millisecondes >= 500) { Handheld.Vibrate(); return; }   // ajoute aussi la permission VIBRATE au build
            using (AndroidJavaClass u = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject a = u.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject v = a.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                v.Call("vibrate", (long)millisecondes);
        }
        catch (System.Exception) { /* pas de vibreur : on ignore */ }
#endif
    }

    // ---------- Clips : fichier si présent, sinon son synthétique ----------

    AudioClip Obtenir(string id)
    {
        AudioClip c;
        if (clips.TryGetValue(id, out c)) return c;
        try { c = Resources.Load<AudioClip>("Audio/" + id); } catch (System.Exception) { c = null; }
        if (c == null) c = Synthese(id);
        clips[id] = c;   // peut rester null : on ne plantera pas
        return c;
    }

    AudioClip Synthese(string id)
    {
        switch (id)
        {
            case "ambiance": return Boucle(id, 4f, (t, i) => 0.25f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * 82.5f * t)
                                                      + 0.1f * Mathf.Sin(2f * Mathf.PI * 110.25f * t)) ;
            case "scellage_loop": return Boucle(id, 2f, (t, i) => 0.3f * Mathf.Sin(2f * Mathf.PI * 70f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 140f * t));
            case "vision_on": return Son(id, 0.5f, (t, d) => Balayage(200f, 800f, t, d) * Enveloppe(t, d, 0.05f) * 0.5f);
            case "vision_off": return Son(id, 0.4f, (t, d) => Balayage(700f, 180f, t, d) * Enveloppe(t, d, 0.03f) * 0.5f);
            case "relique_ok": return Son(id, 0.4f, (t, d) => Balayage(440f, 660f, t, d) * Enveloppe(t, d, 0.02f) * 0.5f);
            case "leurre": return Son(id, 0.6f, (t, d) => (Mathf.Sin(2f * Mathf.PI * 311f * t) + Mathf.Sin(2f * Mathf.PI * 330f * t)) * 0.3f
                                                     * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * Enveloppe(t, d, 0.03f));
            case "fissure": return Son(id, 0.35f, (t, d) => (Bruit() * 0.6f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.5f) * Mathf.Exp(-t * 14f));
            case "chaines": return Son(id, 0.9f, (t, d) => Chaine(t));
            case "eclat": return Son(id, 1.6f, (t, d) => (Bruit() * 0.5f * Mathf.Exp(-t * 4f))
                                                    + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 300f, t / d) * t) * 0.15f * Enveloppe(t, d, 0.2f)
                                                    * (1f + 0.4f * Mathf.Sin(2f * Mathf.PI * 6f * t)));
            case "page": return Son(id, 0.25f, (t, d) => Bruit() * 0.35f * Mathf.Sin(Mathf.PI * t / d));
            default: return null;
        }
    }

    float Bruit() { return (float)(hasard.NextDouble() * 2.0 - 1.0); }

    // Fréquence qui glisse de f0 à f1 pendant la durée d (phase intégrée = pas de craquement)
    static float Balayage(float f0, float f1, float t, float d)
    {
        float phase = 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2f * d));
        return Mathf.Sin(phase);
    }

    // Montée rapide puis descente douce
    static float Enveloppe(float t, float d, float attaque)
    {
        return Mathf.Clamp01(t / attaque) * Mathf.Clamp01((d - t) / (d * 0.5f));
    }

    // Maillons de chaîne : petits tintements métalliques décalés
    static float Chaine(float t)
    {
        float s = 0f;
        for (int k = 0; k < 5; k++)
        {
            float t0 = k * 0.14f;
            if (t < t0) continue;
            float f = 900f + 260f * k;
            s += Mathf.Sin(2f * Mathf.PI * f * (t - t0)) * Mathf.Exp(-(t - t0) * 18f) * 0.25f;
        }
        return s;
    }

    delegate float Forme(float t, float duree);

    AudioClip Son(string nom, float duree, Forme f)
    {
        int n = Mathf.CeilToInt(duree * Freq);
        float[] d = new float[n];
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Freq, duree), -1f, 1f);
        AudioClip c = AudioClip.Create(nom, n, 1, Freq, false);
        c.SetData(d, 0);
        return c;
    }

    // Boucle : seulement des sinus à nombre entier de périodes, donc aucun "clic" au raccord
    AudioClip Boucle(string nom, float duree, System.Func<float, int, float> f)
    {
        int n = Mathf.CeilToInt(duree * Freq);
        float[] d = new float[n];
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Freq, i), -1f, 1f);
        AudioClip c = AudioClip.Create(nom, n, 1, Freq, false);
        c.SetData(d, 0);
        return c;
    }
}
