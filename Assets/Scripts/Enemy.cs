using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Settings")]
    [Tooltip("The speed at which the enemy moves along the path.")]
    [SerializeField] private float moveSpeed = 1.0f; // Customizable enemy speed
    [Tooltip("The amount of health this enemy has.")]
    [SerializeField] private int enemyHealth = 1; // Customizable enemy health
    [Tooltip("The amount of money the player gets when this enemy is destroyed.")]
    [SerializeField] private int moneyOnKill = 10; // Money gained when enemy is killed
    [Tooltip("The damage dealt to player health if this enemy reaches the end.")]
    [SerializeField] private int damageOnReachEnd = 1; // Damage dealt if enemy reaches end

    private Transform[] pathWaypoints; // Array to hold the path waypoints
    private int currentWaypointIndex = 0; // Current waypoint the enemy is moving towards

    /// <summary>
    /// Initializes the enemy with its path waypoints.
    /// This should be called immediately after instantiating the enemy.
    /// </summary>
    /// <param name="waypoints">An array of Transform points defining the path.</param>
    public void SetPath(Transform[] waypoints)
    {
        pathWaypoints = waypoints;
        if (pathWaypoints != null && pathWaypoints.Length > 0)
        {
            // Set initial position to the first waypoint
            transform.position = pathWaypoints[0].position;
        }
    }

    private void Update()
    {
        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogWarning("Enemy path not set or empty!");
            return;
        }

        // Check if there are more waypoints to move to
        if (currentWaypointIndex < pathWaypoints.Length)
        {
            Vector3 targetPosition = pathWaypoints[currentWaypointIndex].position;

            // Move towards the current waypoint
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            // Check if the enemy has reached the current waypoint
            if (Vector3.Distance(transform.position, targetPosition) < 0.1f) // Small threshold
            {
                currentWaypointIndex++; // Move to the next waypoint
            }
        }
        else
        {
            // Enemy has reached the end of the path
            GameManager.Instance.TakeDamage(damageOnReachEnd);
            Destroy(gameObject); // Destroy the enemy
        }
    }

    /// <summary>
    /// Call this method when a bullet hits the enemy.
    /// </summary>
    /// <param name="damage">Amount of damage to apply.</param>
    public void TakeDamage(int damage)
    {
        enemyHealth -= damage;
        if (enemyHealth <= 0)
        {
            GameManager.Instance.AddMoney(moneyOnKill); // Give money to player
            Destroy(gameObject); // Destroy the enemy
        }
    }
}