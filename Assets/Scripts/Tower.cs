using UnityEngine;
using System.Collections;
using TMPro;

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

    // Tower Cost, Sell Price Text variables
    [Tooltip("The original purchase cost of this tower.")]
    [SerializeField] private int towerCost;
    public int TowerCost => towerCost;
    [Tooltip("TextMeshPro text element to display sell price when hovered in sell mode.")]
    [SerializeField] private TextMeshProUGUI sellPriceText;

    // Animator variable (not used for Fatty Poly Turret, but kept if you swap assets)
    [Tooltip("Reference to the Animator component on the turret model.")]
    [SerializeField] private Animator turretAnimator;

    // --- NEW: Fire Effect GameObject ---
    [Tooltip("Reference to the Fire Effect GameObject (e.g., muzzle flash particle system).")]
    [SerializeField] private GameObject fireEffectPrefab; // Drag the FireFx object here
    private ParticleSystem fireEffectParticleSystem; // Cached reference to its particle system
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

        SetRadiusVisible(false);

        if (sellPriceText != null)
        {
            sellPriceText.gameObject.SetActive(false);
            Debug.Log(name + ": SellPriceText initialized and hidden."); // DEBUG
        }
        else
        {
            Debug.LogError(name + ": SellPriceText is NULL in Tower.cs! Check prefab assignment.", this); // DEBUG
        }

        if (turretAnimator == null)
        {
            turretAnimator = GetComponentInChildren<Animator>();
        }

        // --- NEW: Get Particle System component from Fire Effect ---
        if (fireEffectPrefab != null)
        {
            fireEffectParticleSystem = fireEffectPrefab.GetComponent<ParticleSystem>();
            if (fireEffectParticleSystem == null)
            {
                Debug.LogWarning(name + ": FireEffectPrefab does not have a ParticleSystem component.", this);
            }
        }
        else
        {
            Debug.LogWarning(name + ": FireEffectPrefab is NULL in Tower.cs! Shot effect will not play.", this);
        }
        // -------------------------------------------------------------
    }

    private void Update()
    {
        FindTarget();

        if (targetEnemy != null)
        {
            Vector3 lookDirection = targetEnemy.position - transform.position;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }

            if (Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + 1f / fireRate;
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
                if (enemyCollider == null) continue;

                Enemy enemyComponent = enemyCollider.GetComponent<Enemy>();
                if (enemyComponent == null) continue;

                float distance = Vector3.Distance(transform.position, enemyCollider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    potentialTarget = enemyCollider.transform;
                }
            }
            targetEnemy = potentialTarget;
        }
        else
        {
            targetEnemy = null;
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

        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        Bullet bullet = bulletGO.GetComponent<Bullet>();

        if (bullet != null)
        {
            Vector3 direction = (targetEnemy.position - firePoint.position).normalized;
            bullet.SetDirection(direction);
        }

        // --- NEW: Play the fire effect ---
        if (fireEffectParticleSystem != null)
        {
            fireEffectParticleSystem.Play(); // Play the particle system
        }
        else if (fireEffectPrefab != null) // If it's not a particle system but just a GameObject we want to activate
        {
             // This assumes the FireFx is meant to be activated and then perhaps deactivates itself
             // or you'd need a coroutine to deactivate it after a short time.
             // For a simple single shot, ParticleSystem.Play() is ideal.
             fireEffectPrefab.gameObject.SetActive(true); // Activate the GameObject
             // If it's a transient effect, you might want to call SetActive(false) after a delay:
             // StartCoroutine(DeactivateFireEffectAfterDelay(0.5f)); // Example delay
        }
        // ------------------------------------
    }

    // --- NEW: Optional Coroutine for deactivating non-particle FX ---
    // IEnumerator DeactivateFireEffectAfterDelay(float delay)
    // {
    //     yield return new WaitForSeconds(delay);
    //     if (fireEffectPrefab != null)
    //     {
    //         fireEffectPrefab.gameObject.SetActive(false);
    //     }
    // }
    // -----------------------------------------------------------------

    /// <summary>
    /// Draws the attack radius circle using the LineRenderer.
    /// </summary>
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

    /// <summary>
    /// Displays the calculated sell price above the tower.
    /// </summary>
    public void ShowSellPrice(int price)
    {
        if (sellPriceText != null)
        {
            sellPriceText.text = $"+${price}";
            sellPriceText.gameObject.SetActive(true);
            Debug.Log(name + ": Showing sell price: $" + price); // DEBUG
        }
        else
        {
            Debug.LogError(name + ": Attempted to show sell price but sellPriceText is NULL.", this); // DEBUG
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
            Debug.Log(name + ": Hiding sell price."); // DEBUG
        }
    }

    /// <summary>
    /// Gizmos for visualizing attack range in the editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}