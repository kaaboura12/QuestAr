using UnityEngine;

public class HamaZombieAI : MonoBehaviour
{
    [Header("Movement & Attack")]
    public float moveSpeed = 0.95f;
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

    private Animator anim;
    private Transform playerCam;
    private string currentAnimState = "";
    private float lastAttackTime = -10f;
    private Collider myCollider;

    private void Awake()
    {
        currentHealth = maxHealth;
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
            CapsuleCollider cc = gameObject.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.radius = 0.35f;
            cc.height = 1.8f;
            myCollider = cc;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        try
        {
            gameObject.tag = "Blood";
        }
        catch (System.Exception) { }

        EnsureValidMaterial();
        FindPlayerCamera();
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
            return;
        }

        if (anim != null && anim.speed == 0f)
        {
            anim.speed = 1f;
        }

        Vector3 currentPos = transform.position;
        Vector3 targetPos = new Vector3(playerCam.position.x, currentPos.y, playerCam.position.z);

        Vector3 dirToPlayer = targetPos - currentPos;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer > 0.05f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Compute repulsion from other zombies so they never overlap
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

        if (distanceToPlayer > attackRange)
        {
            Vector3 moveDir = (dirToPlayer.normalized + separationOffset * 1.4f).normalized;
            moveDir.y = 0f;
            transform.position = currentPos + moveDir * (moveSpeed * Time.deltaTime);
            PlayState("Z_Walk_InPlace");
        }
        else
        {
            // Keep gentle separation even while in attack range
            if (separationOffset.sqrMagnitude > 0.01f)
            {
                transform.position = currentPos + separationOffset * (moveSpeed * 0.5f * Time.deltaTime);
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

        if (anim != null)
        {
            anim.speed = 1f;
            PlayState("Z_FallingBack");
        }

        if (myCollider != null)
        {
            myCollider.enabled = false;
        }

        Destroy(gameObject, 4.5f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead || collision == null || collision.gameObject == null) return;

        string objName = collision.gameObject.name;
        if (collision.gameObject.GetComponent("Projectile") != null ||
            objName.IndexOf("bullet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objName.IndexOf("projectile", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            TakeDamage(35f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead || other == null || other.gameObject == null) return;

        string objName = other.gameObject.name;
        if (other.gameObject.GetComponent("Projectile") != null ||
            objName.IndexOf("bullet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objName.IndexOf("projectile", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
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
