using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and manages zombie enemies in the ARGameplay scene using Zombie3 and HamaZombieAI.
/// Spawns zombies in front of the player and maintains wave activity.
/// </summary>
[DefaultExecutionOrder(100)]
public class ARZombieSpawner : MonoBehaviour
{
    const string ZombiePrefabFallbackPath = "Assets/Zombie/Prefabs/Zombie3.prefab";

    [Header("Zombie Settings")]
    [SerializeField] GameObject zombiePrefab;
    [SerializeField] int initialZombieCount = 4;
    [SerializeField] int maxZombies = 5;
    [SerializeField] float spawnDistanceMin = 7.0f;
    [SerializeField] float spawnDistanceMax = 12.0f;
    [SerializeField] float respawnInterval = 9.0f;

    private readonly List<HamaZombieAI> activeZombies = new List<HamaZombieAI>();
    private float lastRespawnTime = 0f;
    private Transform playerCam;

    public int ActiveZombieCount => CleanAndCountZombies();

    private void Awake()
    {
        if (zombiePrefab == null)
        {
            var registry = ARCombatAssetRegistry.Instance;
            if (registry != null && registry.zombiePrefab != null)
            {
                zombiePrefab = registry.zombiePrefab;
            }
        }

#if UNITY_EDITOR
        if (zombiePrefab == null)
        {
            zombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabFallbackPath);
        }
#endif
    }

    private void Start()
    {
        StartCoroutine(InitialSpawnRoutine());
    }

    private IEnumerator InitialSpawnRoutine()
    {
        // Give 0.8s for AR tracking / Camera initialization
        yield return new WaitForSeconds(0.8f);

        FindPlayerCamera();

        float[] angleOffsets = { -48f, -20f, 20f, 48f };
        for (int i = 0; i < initialZombieCount; i++)
        {
            float angle = i < angleOffsets.Length ? angleOffsets[i] : Random.Range(-45f, 45f);
            float dist = Mathf.Lerp(spawnDistanceMin, spawnDistanceMax, (float)i / Mathf.Max(1, initialZombieCount - 1));
            SpawnSingleZombie(angle, dist);
            yield return new WaitForSeconds(0.15f);
        }

        lastRespawnTime = Time.time;
    }

    private void Update()
    {
        CleanAndCountZombies();

        if (activeZombies.Count < initialZombieCount && Time.time - lastRespawnTime >= respawnInterval)
        {
            lastRespawnTime = Time.time;
            float randomAngle = Random.Range(-45f, 45f);
            float randomDist = Random.Range(spawnDistanceMin, spawnDistanceMax);
            SpawnSingleZombie(randomAngle, randomDist);
        }
    }

    public GameObject SpawnSingleZombie(float angleOffsetDeg, float distance)
    {
        if (zombiePrefab == null)
        {
            var registry = ARCombatAssetRegistry.Instance;
            if (registry != null) zombiePrefab = registry.zombiePrefab;
        }

        if (zombiePrefab == null)
        {
            Debug.LogWarning("[ARZombieSpawner] No zombie prefab found!");
            return null;
        }

        FindPlayerCamera();
        if (playerCam == null) return null;

        Vector3 camForward = playerCam.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.001f)
            camForward = Vector3.forward;
        else
            camForward.Normalize();

        Vector3 spawnDir = Quaternion.Euler(0f, angleOffsetDeg, 0f) * camForward;
        Vector3 spawnPos = playerCam.position + spawnDir * distance;

        // Grounding: raycast downward, else assume floor at eye-height offset
        if (Physics.Raycast(spawnPos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawnPos.y = hit.point.y;
        }
        else
        {
            spawnPos.y = playerCam.position.y - 1.5f;
        }

        Quaternion lookAtPlayer = Quaternion.LookRotation((playerCam.position - spawnPos).normalized, Vector3.up);
        lookAtPlayer.x = 0f;
        lookAtPlayer.z = 0f;

        GameObject instance = Instantiate(zombiePrefab, spawnPos, lookAtPlayer);
        instance.name = "AR_Zombie_" + (activeZombies.Count + 1);
        instance.transform.localScale = Vector3.one * 0.9f;

        HamaZombieAI ai = instance.GetComponent<HamaZombieAI>();
        if (ai == null)
        {
            ai = instance.AddComponent<HamaZombieAI>();
        }

        ai.moveSpeed = Random.Range(0.88f, 1.05f);
        ai.rotationSpeed = 5.0f;
        ai.attackRange = 1.45f;
        ai.attackDamage = 15.0f;
        ai.attackInterval = 1.25f;
        ai.minSeparationFromOtherZombies = 1.3f;

        activeZombies.Add(ai);
        return instance;
    }

    private int CleanAndCountZombies()
    {
        for (int i = activeZombies.Count - 1; i >= 0; i--)
        {
            if (activeZombies[i] == null || activeZombies[i].isDead)
            {
                activeZombies.RemoveAt(i);
            }
        }
        return activeZombies.Count;
    }

    private void FindPlayerCamera()
    {
        if (playerCam != null) return;

        if (Camera.main != null)
        {
            playerCam = Camera.main.transform;
        }
        else
        {
            Camera cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null) playerCam = cam.transform;
        }
    }
}
