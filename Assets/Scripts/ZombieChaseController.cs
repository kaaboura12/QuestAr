using System.Collections;
using UnityEngine;

namespace ZombieAR
{
    public class ZombieChaseController : MonoBehaviour
    {
        public enum State
        {
            Idle,
            AlertHolding,
            Running,
            Attacking,
            Dead
        }

        [Header("Target & Movement")]
        [Tooltip("Cible à poursuivre (la caméra principale sera utilisée si vide)")]
        public Transform targetPlayer;

        [Tooltip("Vitesse de course vers le joueur")]
        public float runSpeed = 2.8f;

        [Tooltip("Vitesse de rotation vers le joueur")]
        public float rotationSpeed = 7.0f;

        [Tooltip("Distance minimale pour déclencher l'attaque")]
        public float attackDistance = 1.3f;

        [Header("Health & Combat")]
        public int maxHealth = 3;
        [SerializeField] private int currentHealth;

        [Header("Delay Settings")]
        [Tooltip("Temps d'arrêt/alerte avant de courir (en secondes)")]
        public float alertHoldDuration = 5.0f;

        [Header("Animation States")]
        public string idleAnimState = "Z_Idle";
        public string runAnimState = "Z_Run_InPlace";
        public string attackAnimState = "Z_Attack";
        public string deathAnimState = "Z_FallingBack";

        [Header("Audio Settings")]
        public AudioSource audioSource;
        public AudioClip runGrowlClip;

        [Header("Debug Status")]
        [SerializeField] private State currentState = State.Idle;
        [SerializeField] private float countdownRemaining = 0f;

        public State CurrentState => currentState;
        public float CountdownRemaining => countdownRemaining;
        public bool IsDead => currentState == State.Dead;

        private Animator animator;
        private Collider col;
        private Coroutine growlRoutine;

        private void Awake()
        {
            currentHealth = maxHealth;
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            col = GetComponent<Collider>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.9f, 0);
                box.size = new Vector3(0.8f, 1.8f, 0.8f);
                col = box;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.spatialBlend = 1.0f; // Audio 3D spatialisé
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 20.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;

            if (runGrowlClip == null)
            {
                runGrowlClip = GenerateZombieGrowlClip();
            }
        }

        private void Start()
        {
            FindPlayerTarget();
            PlayAnimation(idleAnimState);

            if (MicLoudnessDetector.Instance != null)
            {
                MicLoudnessDetector.Instance.OnLoudSound += OnLoudSoundDetected;
            }
        }

        private void FindPlayerTarget()
        {
            if (targetPlayer == null)
            {
                if (Camera.main != null)
                {
                    targetPlayer = Camera.main.transform;
                }
                else
                {
                    Camera cam = FindAnyObjectByType<Camera>();
                    if (cam != null) targetPlayer = cam.transform;
                }
            }
        }

        private void OnEnable()
        {
            if (MicLoudnessDetector.Instance != null)
            {
                MicLoudnessDetector.Instance.OnLoudSound -= OnLoudSoundDetected;
                MicLoudnessDetector.Instance.OnLoudSound += OnLoudSoundDetected;
            }
        }

        private void OnDisable()
        {
            if (MicLoudnessDetector.Instance != null)
            {
                MicLoudnessDetector.Instance.OnLoudSound -= OnLoudSoundDetected;
            }
        }

        private void OnLoudSoundDetected(float db)
        {
            if (currentState == State.Idle)
            {
                TriggerAlert();
            }
        }

        [ContextMenu("Trigger Alert (5s hold then run)")]
        public void TriggerAlert()
        {
            if (currentState == State.Running || currentState == State.AlertHolding || currentState == State.Dead) return;

            FindPlayerTarget();
            StopAllCoroutines();
            StartCoroutine(AlertHoldRoutine());
        }

        private IEnumerator AlertHoldRoutine()
        {
            currentState = State.AlertHolding;
            countdownRemaining = alertHoldDuration;

            while (countdownRemaining > 0f)
            {
                LookAtTarget();
                countdownRemaining -= Time.deltaTime;
                yield return null;
            }

            // Début de la course
            currentState = State.Running;
            PlayAnimation(runAnimState);

            // Démarrer les grognements en boucle pendant la course
            if (growlRoutine != null) StopCoroutine(growlRoutine);
            growlRoutine = StartCoroutine(GrowlLoopRoutine());
        }

        private IEnumerator GrowlLoopRoutine()
        {
            while (currentState == State.Running)
            {
                if (audioSource != null && runGrowlClip != null && !audioSource.isPlaying)
                {
                    audioSource.pitch = Random.Range(0.85f, 1.15f);
                    audioSource.PlayOneShot(runGrowlClip, 0.85f);
                }
                yield return new WaitForSeconds(Random.Range(1.8f, 3.2f));
            }
        }

        private void Update()
        {
            if (currentState == State.Dead) return;

            FindPlayerTarget();

            if (currentState == State.Running)
            {
                ChasePlayer();
            }
        }

        private void LookAtTarget()
        {
            if (targetPlayer == null) return;

            Vector3 lookDirection = targetPlayer.position - transform.position;
            lookDirection.y = 0;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private void ChasePlayer()
        {
            if (targetPlayer == null) return;

            Vector3 targetGroundPos = targetPlayer.position;
            targetGroundPos.y = transform.position.y;

            float distance = Vector3.Distance(transform.position, targetGroundPos);

            if (distance <= attackDistance)
            {
                currentState = State.Attacking;
                PlayAnimation(attackAnimState);
                if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
                return;
            }

            LookAtTarget();
            transform.position = Vector3.MoveTowards(transform.position, targetGroundPos, runSpeed * Time.deltaTime);
        }

        public void TakeDamage(int damage)
        {
            if (currentState == State.Dead) return;

            currentHealth -= damage;
            Debug.Log($"<color=red>[ZombieChaseController] {gameObject.name} touché ! PV restants: {currentHealth}</color>");

            // Si le zombie était encore au repos, un coup de feu l'alerte immédiatement !
            if (currentState == State.Idle)
            {
                TriggerAlert();
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            currentState = State.Dead;
            StopAllCoroutines();

            if (col != null) col.enabled = false;
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

            PlayAnimation(deathAnimState);
            Debug.Log($"<color=green>[ZombieChaseController] {gameObject.name} est éliminé !</color>");

            // Destruction après 4 secondes pour libérer la mémoire
            Destroy(gameObject, 4.0f);
        }

        private void PlayAnimation(string stateName)
        {
            if (animator != null && !string.IsNullOrEmpty(stateName))
            {
                animator.CrossFade(stateName, 0.15f);
            }
        }

        public void ResetToIdle()
        {
            if (currentState == State.Dead) return;

            StopAllCoroutines();
            currentState = State.Idle;
            countdownRemaining = 0f;
            currentHealth = maxHealth;
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            PlayAnimation(idleAnimState);
        }

        private AudioClip GenerateZombieGrowlClip()
        {
            int sampleRate = 22050;
            float duration = 1.4f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];
            var rand = new System.Random(42 + name.GetHashCode());

            float phase = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((t / duration) * Mathf.PI);

                float freq = 85f + 35f * Mathf.Sin(t * 12f) + (float)(rand.NextDouble() * 20f);
                phase += 2f * Mathf.PI * freq / sampleRate;

                float wave = Mathf.Sin(phase) * 0.6f;
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.35f;

                data[i] = Mathf.Clamp((wave + noise) * env * 0.8f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ZombieGrowl_" + gameObject.name, totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
