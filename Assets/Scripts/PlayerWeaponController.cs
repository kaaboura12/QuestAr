using System.Collections;
using UnityEngine;

namespace ZombieAR
{
    public class PlayerWeaponController : MonoBehaviour
    {
        public static PlayerWeaponController Instance { get; private set; }

        [Header("Weapon Configuration")]
        [Tooltip("Transform de l'arme affichée à l'écran")]
        public Transform gunTransform;

        [Tooltip("Portée maximale du tir en mètres")]
        public float range = 50f;

        [Tooltip("Dégâts par balle")]
        public int damagePerShot = 1;

        [Tooltip("Temps de recharge entre deux tirs (secondes)")]
        public float fireRate = 0.22f;

        [Header("Audio & Effects")]
        public AudioSource audioSource;
        public AudioClip gunShotClip;

        private Camera playerCamera;
        private float nextFireTime = 0f;
        private Vector3 defaultLocalPos = new Vector3(0.22f, -0.20f, 0.48f);
        private Quaternion defaultLocalRot = Quaternion.Euler(0f, -5f, 0f);
        private Coroutine recoilCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            playerCamera = GetComponent<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.spatialBlend = 0f; // 2D (le joueur tire, son direct aux oreilles)

            if (gunShotClip == null)
            {
                gunShotClip = GenerateGunShotClip();
            }
        }

        private void Start()
        {
            if (gunTransform != null)
            {
                gunTransform.localPosition = defaultLocalPos;
                gunTransform.localRotation = defaultLocalRot;
            }
        }

        private void Update()
        {
            // Tir avec la souris / clic sur PC
            if (Input.GetMouseButtonDown(0))
            {
                // Vérifier qu'on ne clique pas sur le haut de l'écran si UI
                Shoot();
            }
        }

        public void Shoot()
        {
            if (Time.time < nextFireTime) return;
            nextFireTime = Time.time + fireRate;

            // 1. Jouer le son de coup de feu
            if (audioSource != null && gunShotClip != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(gunShotClip, 1.0f);
            }

            // 2. Animation de recul physique de l'arme
            if (gunTransform != null)
            {
                if (recoilCoroutine != null) StopCoroutine(recoilCoroutine);
                recoilCoroutine = StartCoroutine(RecoilRoutine());
            }

            // 3. Raycast depuis le centre de la caméra (réticule de visée)
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera != null)
            {
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, range))
                {
                    Debug.Log($"<color=yellow>[PlayerWeaponController] Tir a touché : {hit.collider.name}</color>");

                    var zombie = hit.collider.GetComponentInParent<ZombieChaseController>();
                    if (zombie != null)
                    {
                        zombie.TakeDamage(damagePerShot);
                    }
                }
            }
        }

        private IEnumerator RecoilRoutine()
        {
            Vector3 kickPos = defaultLocalPos + new Vector3(0f, 0.04f, -0.09f);
            Quaternion kickRot = defaultLocalRot * Quaternion.Euler(-14f, 0f, 0f);

            float t = 0f;
            while (t < 0.05f)
            {
                t += Time.deltaTime;
                gunTransform.localPosition = Vector3.Lerp(defaultLocalPos, kickPos, t / 0.05f);
                gunTransform.localRotation = Quaternion.Slerp(defaultLocalRot, kickRot, t / 0.05f);
                yield return null;
            }

            t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                gunTransform.localPosition = Vector3.Lerp(kickPos, defaultLocalPos, t / 0.16f);
                gunTransform.localRotation = Quaternion.Slerp(kickRot, defaultLocalRot, t / 0.16f);
                yield return null;
            }

            gunTransform.localPosition = defaultLocalPos;
            gunTransform.localRotation = defaultLocalRot;
        }

        private AudioClip GenerateGunShotClip()
        {
            int sampleRate = 44100;
            float duration = 0.38f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];
            var rand = new System.Random(99);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f); // Décroissance très rapide (impact sec)

                // Fréquence basse de détonation + bruit blanc de claquement
                float bang = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 15f);
                float crack = ((float)rand.NextDouble() * 2f - 1f) * 0.9f;

                data[i] = Mathf.Clamp((bang * 0.7f + crack * 0.5f) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("GunShot_Synthesized", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
