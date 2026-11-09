using UnityEngine;

public class HamaZombieAI : MonoBehaviour
{
    public enum ZombieState
    {
        Idle,
        Walking,
        AlertHolding,
        Running,
        Attacking,
        Dead
    }

    [Header("State Machine")]
    public ZombieState currentState = ZombieState.Walking;
    public float walkSpeed = 0.95f;
    public float runSpeed = 4.2f;
    public float alertDuration = 5.0f;
    public float rotationSpeed = 5.0f;
    public float attackRange = 1.45f;
    public float minSeparationFromOtherZombies = 1.3f;
    public float attackInterval = 1.25f;
    public float attackDamage = 15.0f;

    [Header("Health & State")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public bool isStunned = false;
    public bool isDead = false;

    [Header("Audio 3D")]
    public AudioClip roarClip;
    private AudioSource roarAudio;

    private Animator anim;
    private Transform playerCam;
    private string currentAnimState = "";
    private float lastAttackTime = -10f;
    private float lastBulletHitTime = -10f;
    private float alertTimer = 0f;
    private Collider myCollider;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        ARMicDetectionManager.OnLoudSound += HandleLoudSound;
    }

    private void OnDisable()
    {
        ARMicDetectionManager.OnLoudSound -= HandleLoudSound;
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
        }

        myCollider = GetComponent<Collider>();
        if (myCollider == null)
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.95f, 0f);
            bc.size = new Vector3(0.7f, 1.9f, 0.6f);
            myCollider = bc;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Setup3DAudio();

        try
        {
            gameObject.tag = "Blood";
        }
        catch (System.Exception) { }

        EnsureValidMaterial();
        FindPlayerCamera();
    }

    private void Setup3DAudio()
    {
        roarAudio = GetComponent<AudioSource>();
        if (roarAudio == null)
        {
            roarAudio = gameObject.AddComponent<AudioSource>();
        }
        roarAudio.spatialBlend = 1.0f; // 3D Spatial Audio
        roarAudio.minDistance = 2.0f;
        roarAudio.maxDistance = 25.0f;
        roarAudio.rolloffMode = AudioRolloffMode.Linear;
        roarAudio.playOnAwake = false;
        roarAudio.loop = true;
        roarAudio.volume = 0.9f;

        if (roarClip != null)
        {
            roarAudio.clip = roarClip;
        }
        else
        {
            var reg = ARCombatAssetRegistry.Instance;
            if (reg != null && reg.hurtVoice != null)
            {
                roarAudio.clip = reg.hurtVoice;
                roarAudio.pitch = Random.Range(0.6f, 0.72f); // Pitch bas terrifiant (grognement zombie)
            }
        }
    }

    private void HandleLoudSound()
    {
        if (isDead || isStunned)
            return;

        // Quand le joueur crie ou fait du bruit : les zombies NE s'arrêtent PAS !
        // Ils passent immédiatement de la marche normale au sprint direct vers Camera.main
        if (currentState != ZombieState.Running)
        {
            currentState = ZombieState.Running;

            if (playerCam == null)
                FindPlayerCamera();

            // S'orienter immédiatement vers la caméra du joueur sur l'axe horizontal
            if (playerCam != null)
            {
                Vector3 currentPos = transform.position;
                Vector3 targetPos = new Vector3(playerCam.position.x, currentPos.y, playerCam.position.z);
                Vector3 dir = targetPos - currentPos;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }

            if (anim != null)
            {
                anim.applyRootMotion = false;
            }

            PlayState("Z_Run_InPlace");

            if (roarAudio != null && !roarAudio.isPlaying)
            {
                roarAudio.Play();
            }

            Debug.Log($"[HamaZombieAI] {gameObject.name} a entendu le bruit ! Sprint immédiat vers le joueur à {runSpeed} m/s !");
        }
    }

    private void EnsureValidMaterial()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                bool needsFallback = m.shader == null ||
                                     !m.shader.isSupported ||
                                     m.shader.name == "Hidden/InternalErrorShader" ||
                                     m.shader.name.IndexOf("Error", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (!needsFallback && m.shader.name.IndexOf("Universal Render Pipeline", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                    {
                        needsFallback = true;
                    }
                }

                if (needsFallback)
                {
                    Shader targetShader = Shader.Find("Standard");
                    if (targetShader == null || !targetShader.isSupported)
                    {
                        targetShader = Shader.Find("Mobile/Diffuse");
                    }
                    if (targetShader != null)
                    {
                        m.shader = targetShader;
                    }
                }
            }
        }
    }

    private void FindPlayerCamera()
    {
        if (Camera.main != null)
        {
            playerCam = Camera.main.transform;
        }
        else
        {
            Camera c = Object.FindAnyObjectByType<Camera>();
            if (c != null) playerCam = c.transform;
        }
    }

    private void Update()
    {
        if (isDead) return;

        if (playerCam == null)
        {
            FindPlayerCamera();
            if (playerCam == null) return;
        }

        // If Flash is active (5 seconds stun), freeze zombie completely
        if (isStunned)
        {
            if (anim != null)
            {
                if (currentAnimState != "Z_Idle")
                {
                    anim.Play("Z_Idle", 0, 0f);
                    currentAnimState = "Z_Idle";
                }
                anim.speed = 0f;
            }
            if (roarAudio != null && roarAudio.isPlaying)
                roarAudio.Pause();
            return;
        }

        if (anim != null && anim.speed == 0f)
        {
            anim.speed = 1f;
        }

        if (QuestARLevelManager.IsTransitioning || QuestARLevelManager.IsGameWon || QuestARLevelManager.IsPlayerDead())
        {
            if (roarAudio != null && roarAudio.isPlaying)
                roarAudio.Pause();
            PlayState("Z_Idle");
            return;
        }

        // Maintenir le zombie strictement posé sur le vrai sol sous la caméra (sans jamais flotter à hauteur des yeux)
        Vector3 p = transform.position;
        float rawFloorY = ARZombieSpawner.IsFloorCalibrated
            ? ARZombieSpawner.DetectedFloorY
            : (playerCam.position.y - 1.45f);
        p.y = Mathf.Clamp(rawFloorY, playerCam.position.y - 1.85f, playerCam.position.y - 1.20f);
        transform.position = p;

        Vector3 currentPos = transform.position;
        Vector3 targetPos = new Vector3(playerCam.position.x, currentPos.y, playerCam.position.z);

        Vector3 dirToPlayer = targetPos - currentPos;
        float distanceToPlayer = dirToPlayer.magnitude;

        // 1. ÉTAT: AlertHolding (Converti immédiatement en Running sans immobilisation)
        if (currentState == ZombieState.AlertHolding)
        {
            currentState = ZombieState.Running;
            if (anim != null)
            {
                anim.applyRootMotion = false;
            }
            PlayState("Z_Run_InPlace");
            if (roarAudio != null && !roarAudio.isPlaying)
            {
                roarAudio.Play();
            }
        }

        // Orientation vers la caméra
        if (distanceToPlayer > 0.05f)
        {
            float rotSpeed = (currentState == ZombieState.Running) ? 9.0f : rotationSpeed;
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
        }

        // Répulsion entre zombies pour éviter la superposition
        Vector3 separationOffset = Vector3.zero;
        HamaZombieAI[] allZombies = Object.FindObjectsByType<HamaZombieAI>(FindObjectsInactive.Exclude);
        foreach (HamaZombieAI other in allZombies)
        {
            if (other == null || other == this || other.isDead) continue;
            Vector3 diff = currentPos - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist > 0.001f && dist < minSeparationFromOtherZombies)
            {
                separationOffset += diff.normalized * ((minSeparationFromOtherZombies - dist) / minSeparationFromOtherZombies);
            }
        }

        // 2. Déplacement ou Attaque
        if (distanceToPlayer > attackRange)
        {
            float activeSpeed = (currentState == ZombieState.Running) ? runSpeed : walkSpeed;
            Vector3 moveDir = dirToPlayer.normalized;
            if (separationOffset.sqrMagnitude > 0.001f)
            {
                moveDir = (dirToPlayer.normalized + separationOffset * 0.7f).normalized;
            }
            moveDir.y = 0f;

            // Déplacement vers Camera.main
            transform.position += moveDir * (activeSpeed * Time.deltaTime);

            if (currentState == ZombieState.Running)
            {
                PlayState("Z_Run_InPlace");
                if (roarAudio != null && !roarAudio.isPlaying)
                {
                    roarAudio.Play();
                }
            }
            else
            {
                PlayState("Z_Walk_InPlace");
            }
        }
        else
        {
            // Portée d'attaque atteinte
            currentState = ZombieState.Attacking;
            if (separationOffset.sqrMagnitude > 0.01f)
            {
                transform.position = currentPos + separationOffset * (walkSpeed * 0.5f * Time.deltaTime);
            }
            PlayState("Z_Attack");

            if (Time.time - lastAttackTime >= attackInterval)
            {
                lastAttackTime = Time.time;
                if (HamaFlashStunManager.Instance != null)
                {
                    HamaFlashStunManager.Instance.DamagePlayer(attackDamage);
                }
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        isStunned = false;
        currentState = ZombieState.Dead;

        if (roarAudio != null && roarAudio.isPlaying)
        {
            roarAudio.Stop();
        }

        if (anim != null)
        {
            anim.speed = 1f;
            PlayState("Z_FallingBack");
        }

        if (myCollider != null)
        {
            myCollider.enabled = false;
        }

        QuestARLevelManager.NotifyZombieKilled();

        Destroy(gameObject, 4.5f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead || collision == null || collision.gameObject == null) return;
        TryProcessProjectileHit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead || other == null || other.gameObject == null) return;
        TryProcessProjectileHit(other.gameObject);
    }

    private void TryProcessProjectileHit(GameObject hitObj)
    {
        if (isDead || hitObj == null || QuestARLevelManager.IsPlayerDead()) return;

        string objName = hitObj.name;
        if (hitObj.GetComponent("Projectile") != null ||
            objName.IndexOf("bullet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objName.IndexOf("projectile", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Empêcher une même balle de déclencher OnCollisionEnter + OnTriggerEnter plusieurs fois
            if (Time.time - lastBulletHitTime < 0.06f)
            {
                Destroy(hitObj);
                return;
            }

            lastBulletHitTime = Time.time;
            Destroy(hitObj);
            TakeDamage(35f);
        }
    }

    public void SetStunned(bool stunned)
    {
        if (isDead) return;
        isStunned = stunned;
        if (anim != null)
        {
            if (stunned)
            {
                anim.Play("Z_Idle", 0, 0f);
                anim.speed = 0f;
                currentAnimState = "Z_Idle";
            }
            else
            {
                anim.speed = 1f;
                currentAnimState = "";
            }
        }
    }

    private void PlayState(string stateName)
    {
        if (anim == null || currentAnimState == stateName) return;
        anim.CrossFadeInFixedTime(stateName, 0.2f);
        currentAnimState = stateName;
    }
}
