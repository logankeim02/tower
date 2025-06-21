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

    [Tooltip("Reference to the Fire Effect GameObject (e.g., muzzle flash particle system).")]
    [SerializeField] private GameObject fireEffectPrefab;
    private ParticleSystem fireEffectParticleSystem;

    // Audio Variables
    [Header("Audio Settings")]
    [Tooltip("Reference to the AudioSource component on this tower.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("The minimum pitch for the gun sound.")]
    [SerializeField] private float minPitch = 0.9f; // Slightly flatter
    [Tooltip("The maximum pitch for the gun sound.")]
    [SerializeField] private float maxPitch = 1.1f; // Slightly sharper
    // --- REMOVED: pitchStep ---
    // [Tooltip("How much to change pitch per shot.")]
    // [SerializeField] private float pitchStep = 0.05f;
    // --------------------------

    // --- REMOVED: currentPitch and pitchIncreasing as they're no longer needed for random pitch ---
    // private float currentPitch;
    // private bool pitchIncreasing = true;
    // ------------------------------------------------------------------------------------------------

    private Transform targetEnemy; // The current enemy this tower is targeting
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
        }
        else
        {
            Debug.LogError(name + ": SellPriceText is NULL in Tower.cs! Check prefab assignment.", this);
        }

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

        // Initialize AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError(name + ": AudioSource component not found on Tower! Sound will not play.", this);
            }
        }
        // --- REMOVED: currentPitch initialization ---
        // currentPitch = 1.0f; // Start at normal pitch
        // ------------------------------------------
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

        if (fireEffectParticleSystem != null)
        {
            fireEffectParticleSystem.Play();
        }
        else if (fireEffectPrefab != null)
        {
            fireEffectPrefab.gameObject.SetActive(true);
        }

        // --- NEW: Play sound with RANDOM pitch ---
        if (audioSource != null)
        {
            // Set a random pitch within the defined range
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.Play(); // Play the assigned AudioClip
        }
        // ------------------------------------------
    }

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
        }
        else
        {
            Debug.LogError(name + ": Attempted to show sell price but sellPriceText is NULL.", this);
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

    /// <summary>
    /// Gizmos for visualizing attack range in the editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}