using UnityEngine;
using System.Collections; // Required for Coroutines

public class Enemy : MonoBehaviour
{
    [Header("Enemy Settings")]
    [Tooltip("The speed at which the enemy moves along the path.")]
    [SerializeField] private float moveSpeed = 1.0f;
    [Tooltip("The amount of health this enemy has.")]
    [SerializeField] private int enemyHealth = 1;
    [Tooltip("The amount of money the player gets when this enemy is destroyed.")]
    [SerializeField] private int moneyOnKill = 10;
    [Tooltip("The damage dealt to player health if this enemy reaches the end.")]
    [SerializeField] private int damageOnReachEnd = 1;

    [Tooltip("Reference to the Animator component on the enemy model.")]
    [SerializeField] private Animator enemyAnimator;
    [Tooltip("The name of the animation state to play when the enemy moves (e.g., 'Walk', 'Run').")]
    [SerializeField] private string walkAnimationStateName = "Walk";
    
    [Header("Ragdoll Settings")]
    [Tooltip("Check this box if this enemy should use the ragdoll system on death.")]
    [SerializeField] private bool useRagdoll = true;
    
    [Header("Death Effects")]
    [Tooltip("Prefab of the blood splat to instantiate on the ground when enemy dies.")]
    [SerializeField] private GameObject bloodSplatGroundPrefab;
    [Tooltip("Prefab of the gore particles to instantiate and play when enemy dies.")]
    [SerializeField] private GameObject goreParticlesPrefab;

    private Rigidbody[] ragdollRigidbodies;
    private Collider mainCollider;
    
    private Transform[] pathWaypoints;
    private int currentWaypointIndex = 0;
    private bool isDying = false;

    private void Awake()
    {
        mainCollider = GetComponent<Collider>();
        if (useRagdoll)
        {
            ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        }
    }

    public void SetPath(Transform[] waypoints)
    {
        pathWaypoints = waypoints;
        if (pathWaypoints != null && pathWaypoints.Length > 0)
        {
            transform.position = pathWaypoints[0].position;
        }
    }

    private void Start()
    {
        if (enemyAnimator != null)
        {
            enemyAnimator.Play(walkAnimationStateName);
        }
    }

    private void Update()
    {
        if (isDying) return;

        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogWarning("Enemy path not set or empty!");
            return;
        }

        if (currentWaypointIndex < pathWaypoints.Length)
        {
            Vector3 targetPosition = pathWaypoints[currentWaypointIndex].position;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            Vector3 directionToWaypoint = (targetPosition - transform.position).normalized;
            if (directionToWaypoint != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(new Vector3(directionToWaypoint.x, 0, directionToWaypoint.z));
            }

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                currentWaypointIndex++;
            }
        }
        else
        {
            if (!isDying)
            {
                isDying = true;
                GameManager.Instance.TakeDamage(damageOnReachEnd);
                GameManager.Instance.EnemyDefeated();

                if (useRagdoll)
                {
                    ActivateRagdoll();
                }
                else
                {
                    Destroy(gameObject); 
                }
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDying) return;

        enemyHealth -= damage;

        if (enemyHealth <= 0 && !isDying)
        {
            isDying = true;
            GameManager.Instance.AddMoney(moneyOnKill);
            SpawnDeathEffects();
            GameManager.Instance.EnemyDefeated();
            
            if (useRagdoll)
            {
                ActivateRagdoll();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
    
    private void ActivateRagdoll()
    {
        // --- NEW: Change layer to "Ragdoll" ---
        int ragdollLayer = LayerMask.NameToLayer("Ragdoll");
        SetLayerRecursively(gameObject, ragdollLayer);
        // ----------------------------------------

        if (enemyAnimator != null) enemyAnimator.enabled = false;
        if (mainCollider != null) mainCollider.enabled = false;
        
        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.isKinematic = false;
        }
        
        StartCoroutine(FadeOutAndDestroy(5f, 2f));
    }

    // --- NEW: Helper function to change layer for all children ---
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
    
    private IEnumerator FadeOutAndDestroy(float delay, float fadeDuration)
    {
        yield return new WaitForSeconds(delay);
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        float timer = 0f;
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        while (timer < fadeDuration)
        {
            float newAlpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            foreach (SkinnedMeshRenderer r in renderers)
            {
                r.GetPropertyBlock(propBlock);
                Color originalColor = r.material.color;
                propBlock.SetColor("_BaseColor", new Color(originalColor.r, originalColor.g, originalColor.b, newAlpha));
                r.SetPropertyBlock(propBlock);
            }
            timer += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    private void SpawnDeathEffects()
    {
        if (bloodSplatGroundPrefab != null)
        {
            Vector3 splatPosition = new Vector3(transform.position.x, 0.005f, transform.position.z);
            Instantiate(bloodSplatGroundPrefab, splatPosition, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        }

        if (goreParticlesPrefab != null)
        {
            GameObject goreGO = Instantiate(goreParticlesPrefab, transform.position, Quaternion.identity);
            ParticleSystem ps = goreGO.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
        }
    }
}