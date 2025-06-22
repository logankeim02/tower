using UnityEngine;

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

    [Header("Death Effects")]
    [Tooltip("Prefab of the blood splat to instantiate on the ground when enemy dies.")]
    [SerializeField] private GameObject bloodSplatGroundPrefab;
    [Tooltip("Prefab of the gore particles to instantiate and play when enemy dies.")]
    [SerializeField] private GameObject goreParticlesPrefab;

    private Transform[] pathWaypoints;
    private int currentWaypointIndex = 0;
    
    // --- NEW: This flag prevents the death code from running multiple times ---
    private bool isDying = false;

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
            // --- MODIFIED: Check the isDying flag before running leak logic ---
            if (!isDying)
            {
                isDying = true; // Set the flag
                GameManager.Instance.TakeDamage(damageOnReachEnd);
                GameManager.Instance.EnemyDefeated(); // Notify the GameManager
                Destroy(gameObject);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        // Don't process damage if the enemy is already dying
        if (isDying) return;

        enemyHealth -= damage;

        // --- MODIFIED: Check the isDying flag before running death logic ---
        if (enemyHealth <= 0 && !isDying)
        {
            isDying = true; // Set the flag immediately to prevent other calls
            GameManager.Instance.AddMoney(moneyOnKill);
            SpawnDeathEffects();
            GameManager.Instance.EnemyDefeated(); // Notify the GameManager
            Destroy(gameObject);
        }
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