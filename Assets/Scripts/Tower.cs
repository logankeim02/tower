using UnityEngine;
using System.Collections;
using TMPro; // NEW: For the sell price text

public class Tower : MonoBehaviour
{
    [Header("Tower Settings")]
    [Tooltip("The range of the tower (radius of its detection sphere).")]
    [SerializeField] private float attackRange = 3.0f; // Customizable tower attack range
    [Tooltip("The time in seconds between each shot.")]
    [SerializeField] private float fireRate = 1.0f; // Customizable tower fire rate
    [Tooltip("Reference to the Bullet prefab to be fired.")]
    [SerializeField] private GameObject bulletPrefab; // Assign the Bullet prefab here
    [Tooltip("The transform where bullets will be spawned from (e.g., the barrel of the tower).")]
    [SerializeField] private Transform firePoint; // Assign an empty GameObject as fire point

    // Variables for Radius Visual
    [Tooltip("Reference to the Line Renderer used for the attack radius.")]
    [SerializeField] private LineRenderer radiusLineRenderer;
    [Tooltip("Number of segments to use when drawing the radius circle.")]
    [SerializeField] private int segments = 50; // More segments = smoother circle

    // --- NEW: Tower Cost Variable and Public Accessor ---
    [Tooltip("The original purchase cost of this tower.")]
    [SerializeField] private int towerCost; // Set this on the prefab based on GameManager's basicTowerCost
    public int TowerCost => towerCost;
    // ----------------------------------------------------

    // --- NEW: Sell Price Display Text ---
    [Tooltip("TextMeshPro text element to display sell price when hovered in sell mode.")]
    [SerializeField] private TextMeshProUGUI sellPriceText;
    // ------------------------------------

    private Transform targetEnemy;
    private float nextFireTime;

    private Collider[] enemiesInRange;
    private LayerMask enemyLayer;

    private void Awake()
    {
        enemyLayer = LayerMask.GetMask("Enemy");
        if (firePoint == null)
        {
            firePoint = transform;
        }
        if (radiusLineRenderer == null)
        {
            radiusLineRenderer = GetComponentInChildren<LineRenderer>();
        }

        SetRadiusVisible(false); // Initially hide radius

        // --- NEW: Initially hide the sell price text ---
        if (sellPriceText != null)
        {
            sellPriceText.gameObject.SetActive(false);
        }
        // ---------------------------------------------
    }

    private void Update()
    {
        FindTarget(); // Continuously look for a target

        if (targetEnemy != null)
        {
            // Make the tower face the target (optional, for visual feedback)
            Vector3 lookDirection = targetEnemy.position - transform.position;
            lookDirection.y = 0; // Keep tower upright
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }

            // Check if it's time to fire
            if (Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + 1f / fireRate; // Calculate next fire time
            }
        }
    }

    /// <summary>
    /// Finds the closest enemy within range and sets it as the target.
    /// </summary>
    private void FindTarget()
    {
        enemiesInRange = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);

        if (enemiesInRange.Length > 0)
        {
            float closestDistance = Mathf.Infinity;
            Transform potentialTarget = null;

            foreach (Collider enemyCollider in enemiesInRange)
            {
                if (enemyCollider == null) continue; // Skip if collider is null (enemy destroyed)

                // Check if the enemy still exists (has an Enemy component)
                Enemy enemyComponent = enemyCollider.GetComponent<Enemy>();
                if (enemyComponent == null) continue; // Skip if it's not a valid enemy

                float distance = Vector3.Distance(transform.position, enemyCollider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    potentialTarget = enemyCollider.transform;
                }
            }
            targetEnemy = potentialTarget; // Set the closest valid enemy as target
        }
        else
        {
            targetEnemy = null; // No enemies in range
        }
    }

    /// <summary>
    /// Spawns a bullet and sets its direction towards the target.
    /// </summary>
    private void Shoot()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("Bullet Prefab not assigned to Tower!");
            return;
        }

        // Instantiate bullet at fire point position
        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bullet = bulletGO.GetComponent<Bullet>();

        if (bullet != null)
        {
            // Calculate direction to target
            Vector3 direction = (targetEnemy.position - firePoint.position).normalized;
            bullet.SetDirection(direction);
        }
    }

    /// <summary>
    /// Draws the attack radius circle using the LineRenderer.
    /// Call SetRadiusVisible(true) to show, SetRadiusVisible(false) to hide.
    /// </summary>
    /// <param name="isVisible">True to show the radius, false to hide.</param>
    public void SetRadiusVisible(bool isVisible)
    {
        if (radiusLineRenderer == null)
        {
            Debug.LogWarning("Radius Line Renderer not assigned or found on Tower!", this);
            return;
        }

        radiusLineRenderer.gameObject.SetActive(isVisible);

        if (isVisible)
        {
            float towerOverallScale = transform.localScale.x;
            float effectiveLineRadius = attackRange / towerOverallScale;

            if (towerOverallScale == 0)
            {
                Debug.LogError("Tower scale is zero, cannot draw radius correctly!");
                effectiveLineRadius = attackRange;
            }

            radiusLineRenderer.positionCount = segments + 1;
            float x, z;
            float angle = 0f;
            for (int i = 0; i < (segments + 1); i++)
            {
                x = Mathf.Sin(Mathf.Deg2Rad * angle) * effectiveLineRadius;
                z = Mathf.Cos(Mathf.Deg2Rad * angle) * effectiveLineRadius;
                radiusLineRenderer.SetPosition(i, new Vector3(x, 0f, z));
                angle += (360f / segments);
            }
        }
    }

    // --- NEW: Methods to show/hide sell price ---
    /// <summary>
    /// Displays the calculated sell price above the tower.
    /// </summary>
    /// <param name="price">The money amount the player will get for selling.</param>
    public void ShowSellPrice(int price)
    {
        if (sellPriceText != null)
        {
            sellPriceText.text = $"+${price}";
            sellPriceText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the sell price display.
    /// </summary>
    public void HideSellPrice()
    {
        if (sellPriceText != null)
        {
            sellPriceText.gameObject.SetActive(false);
        }
    }
    // ------------------------------------------

    /// <summary>
    /// Gizmos for visualizing attack range in the editor.
    /// (This is for editor-only selection, SetRadiusVisible is for runtime player view)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}