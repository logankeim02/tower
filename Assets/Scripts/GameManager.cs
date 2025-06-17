using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using System.Collections; // Required for IEnumerator (coroutines)

public class GameManager : MonoBehaviour
{
    // Singleton pattern
    public static GameManager Instance { get; private set; }

    [Header("Player Settings")]
    [Tooltip("Starting player health. Game ends if this reaches 0.")]
    [SerializeField] private int playerHealth = 100;
    [Tooltip("Starting player money.")]
    [SerializeField] private int playerMoney = 500;

    [Header("UI Elements")]
    [Tooltip("Reference to the UI Text element displaying player health.")]
    [SerializeField] private TextMeshProUGUI healthText;
    [Tooltip("Reference to the UI Text element displaying player money.")]
    [SerializeField] private TextMeshProUGUI moneyText;
    [Tooltip("Reference to the UI Text element displaying the current round.")]
    [SerializeField] private TextMeshProUGUI roundText;
    [Tooltip("Reference to the UI Text element displaying the win/lose message.")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("Reference to the UI Text element inside the game over panel.")]
    [SerializeField] private TextMeshProUGUI gameOverMessageText;

    [Header("Round Settings")]
    [Tooltip("The delay in seconds before the first enemy spawns in a round.")]
    [SerializeField] private float startWaveDelay = 2f;
    [Tooltip("The time in seconds between each enemy spawn during a wave.")]
    [SerializeField] private float enemySpawnInterval = 0.5f;
    [Tooltip("Reference to the Enemy prefab to be spawned.")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Tower Placement")]
    [Tooltip("Reference to the Basic Tower prefab to be placed.")]
    [SerializeField] private GameObject basicTowerPrefab;
    [Tooltip("The cost to buy a basic tower.")]
    [SerializeField] private int basicTowerCost = 100;
    [Tooltip("The LayerMask for detecting towers during raycasting for hover.")]
    [SerializeField] private LayerMask towerLayer;
    [Tooltip("The LayerMask for detecting ground during tower placement.")]
    [SerializeField] private LayerMask groundLayer;
    // --- NEW: LayerMask for checking no-build zones ---
    [Tooltip("The LayerMask for areas where towers cannot be built (e.g., enemy path).")]
    [SerializeField] private LayerMask noBuildZoneLayer;
    [Tooltip("Material for the tower preview when placement is valid.")]
    [SerializeField] private Material previewValidMaterial;
    [Tooltip("Material for the tower preview when placement is invalid.")]
    [SerializeField] private Material previewInvalidMaterial;
    // ---------------------------------------------------

    [Header("Sell Settings")]
    [Tooltip("Percentage of original cost refunded when selling a tower (0.0 to 1.0).")]
    [SerializeField] private float sellRefundPercentage = 0.5f; // 50% refund

    private int currentRound = 0;
    private Transform[] pathWaypoints;
    private Coroutine currentWaveCoroutine;

    private bool isPlacingTower = false;
    private GameObject currentPlacingTowerPreview;
    private Tower currentlyHoveredTower;

    private bool isSellingTower = false;

    public int PlayerHealth => playerHealth;
    public int PlayerMoney => playerMoney;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        UpdateHealthUI();
        UpdateMoneyUI();
        UpdateRoundUI();
        gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        Time.timeScale = 1;

        GameObject waypointsParent = GameObject.Find("Waypoints");
        if (waypointsParent != null)
        {
            pathWaypoints = new Transform[waypointsParent.transform.childCount];
            for (int i = 0; i < waypointsParent.transform.childCount; i++)
            {
                pathWaypoints[i] = waypointsParent.transform.GetChild(i);
            }
            Debug.Log($"Found {pathWaypoints.Length} waypoints.");
        }
        else
        {
            Debug.LogError("Waypoints GameObject not found! Please create an empty GameObject named 'Waypoints' with child Transforms for the enemy path.");
        }

        UpdateHealthUI();
        UpdateMoneyUI();
        UpdateRoundUI();

        NextRound(); // This will start Round 1
    }

    private void Update()
    {
        HandleTowerPlacement();
        HandleTowerHover();

        if (Input.GetMouseButtonDown(0)) // Left click
        {
            if (isSellingTower)
            {
                Debug.Log("Update: Left click detected in sell mode. Calling TrySellHoveredTower()."); // DEBUG
                TrySellHoveredTower();
            }
        }
        else if (Input.GetMouseButtonDown(1)) // Right click (to cancel any active mode)
        {
            Debug.Log("Update: Right click detected. Cancelling modes."); // DEBUG
            CancelPlacement();
            ExitSellMode();
        }
    }

    public void TakeDamage(int damage)
    {
        playerHealth -= damage;
        playerHealth = Mathf.Max(0, playerHealth);
        UpdateHealthUI();

        if (playerHealth <= 0)
        {
            GameOver(false);
        }
    }

    public void AddMoney(int amount)
    {
        playerMoney += amount;
        UpdateMoneyUI();
    }

    public bool TrySpendMoney(int amount)
    {
        if (playerMoney >= amount)
        {
            playerMoney -= amount;
            UpdateMoneyUI();
            return true;
        }
        Debug.Log("Not enough money!");
        return false;
    }

    public void NextRound()
    {
        currentRound++;
        UpdateRoundUI();
        Debug.Log($"Starting Round: {currentRound}");

        if (currentWaveCoroutine != null)
        {
            StopCoroutine(currentWaveCoroutine);
        }
        currentWaveCoroutine = StartCoroutine(SpawnWave(currentRound));
    }

    private IEnumerator SpawnWave(int roundNumber)
    {
        yield return new WaitForSeconds(startWaveDelay);

        int numberOfEnemiesToSpawn = 5 + (roundNumber * 2);
        Debug.Log($"Spawning {numberOfEnemiesToSpawn} enemies for Round {roundNumber}");

        for (int i = 0; i < numberOfEnemiesToSpawn; i++)
        {
            if (playerHealth <= 0)
            {
                yield break;
            }

            SpawnEnemy();
            yield return new WaitForSeconds(enemySpawnInterval);
        }

        Debug.Log($"Round {roundNumber} enemy spawning complete. Waiting for enemies to clear.");
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy Prefab not assigned in GameManager!");
            return;
        }
        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogError("Enemy path waypoints not set in GameManager!");
            return;
        }

        GameObject enemyGO = Instantiate(enemyPrefab, pathWaypoints[0].position, Quaternion.identity);
        Enemy enemy = enemyGO.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.SetPath(pathWaypoints);
        }
        else
        {
            Debug.LogError("Instantiated enemy does not have an Enemy script!");
            Destroy(enemyGO);
        }
    }

    public void StartPlacingBasicTower()
    {
        if (isSellingTower) ExitSellMode();
        if (isPlacingTower) return;

        if (TrySpendMoney(basicTowerCost))
        {
            isPlacingTower = true;
            currentPlacingTowerPreview = Instantiate(basicTowerPrefab);

            Tower previewTowerComponent = currentPlacingTowerPreview.GetComponent<Tower>();
            if (previewTowerComponent != null)
            {
                previewTowerComponent.SetRadiusVisible(true);
                previewTowerComponent.enabled = false; // Disable the Tower script
            }

            // --- NEW: Set initial preview material ---
            SetPreviewMaterial(currentPlacingTowerPreview, false); // Start with invalid visually
            // ----------------------------------------

            if (currentlyHoveredTower != null)
            {
                currentlyHoveredTower.SetRadiusVisible(false);
                currentlyHoveredTower.HideSellPrice();
                currentlyHoveredTower = null;
            }

            Debug.Log("StartPlacingBasicTower: Entering placement mode."); // DEBUG
        }
        else
        {
            Debug.Log("Cannot afford basic tower!");
        }
    }

    private void HandleTowerPlacement()
    {
        if (!isPlacingTower) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // --- NEW: Check for valid ground hit and no-build zone overlap ---
        bool hitGround = Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer);
        bool isValidPlacement = false;

        if (hitGround)
        {
            // Update preview position
            currentPlacingTowerPreview.transform.position = new Vector3(hit.point.x, basicTowerPrefab.transform.position.y, hit.point.z);

            // Check for overlap with no-build zone
            // We'll use the Tower prefab's Sphere Collider radius for this check
            // assuming it reflects the tower's base size for collision.
            // If the tower prefab has multiple colliders, you might need to adjust this.
            Collider towerCollider = basicTowerPrefab.GetComponent<Collider>();
            float checkRadius = (towerCollider is SphereCollider) ? (towerCollider as SphereCollider).radius * basicTowerPrefab.transform.localScale.x : 0.5f; // Fallback 0.5f

            // Adjust Y-position for the overlap check to be at the base of the tower
            Vector3 overlapCheckPosition = new Vector3(hit.point.x, basicTowerPrefab.transform.position.y + 0.1f, hit.point.z); // Slightly above ground
            
            // OverlapSphere returns true if any collider on noBuildZoneLayer is found within checkRadius
            bool overlapsNoBuildZone = Physics.OverlapSphere(overlapCheckPosition, checkRadius, noBuildZoneLayer).Length > 0;

            isValidPlacement = !overlapsNoBuildZone; // Placement is valid if it does NOT overlap no-build zone

            // Update preview material based on validity
            SetPreviewMaterial(currentPlacingTowerPreview, isValidPlacement);
        }
        else
        {
            // If not hitting ground, placement is invalid. Set preview material to invalid.
            SetPreviewMaterial(currentPlacingTowerPreview, false);
            // Optionally hide the preview or move it to a "holding" spot
            // currentPlacingTowerPreview.transform.position = new Vector3(1000,1000,1000); // Hide off-screen
        }

        // Only allow placement if it's hitting valid ground and not overlapping no-build zone
        if (Input.GetMouseButtonDown(0) && hitGround && isValidPlacement)
        // ---------------------------------------------------------------------------------
        {
            PlaceTower(hit.point);
            isPlacingTower = false;
            Destroy(currentPlacingTowerPreview);
            currentPlacingTowerPreview = null;
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    private void PlaceTower(Vector3 position)
    {
        position.y = basicTowerPrefab.transform.position.y;
        GameObject newTowerGO = Instantiate(basicTowerPrefab, position, Quaternion.identity);

        Tower newTowerComponent = newTowerGO.GetComponent<Tower>();
        if (newTowerComponent != null)
        {
            newTowerComponent.SetRadiusVisible(false);
            newTowerComponent.enabled = true; // Ensure script is enabled on placed tower
        }

        Debug.Log("Tower placed at: " + position);
    }

    public void CancelPlacement()
    {
        if (!isPlacingTower) return;

        isPlacingTower = false;
        if (currentPlacingTowerPreview != null)
        {
            Tower previewTowerComponent = currentPlacingTowerPreview.GetComponent<Tower>();
            if (previewTowerComponent != null)
            {
                previewTowerComponent.SetRadiusVisible(false);
            }
            Destroy(currentPlacingTowerPreview);
            currentPlacingTowerPreview = null;
        }
        Debug.Log("Tower placement cancelled.");
    }

    public void StartSellingTower()
    {
        if (isPlacingTower) CancelPlacement();

        if (isSellingTower)
        {
            Debug.Log("StartSellingTower: Toggling OFF sell mode."); // DEBUG
            ExitSellMode();
            return;
        }

        isSellingTower = true;
        Debug.Log("StartSellingTower: Entered sell mode. isSellingTower = " + isSellingTower); // DEBUG

        if (currentlyHoveredTower != null)
        {
            currentlyHoveredTower.SetRadiusVisible(false);
        }
    }

    public void ExitSellMode()
    {
        if (!isSellingTower) return;

        isSellingTower = false;
        Debug.Log("ExitSellMode: Exited sell mode. isSellingTower = " + isSellingTower); // DEBUG

        if (currentlyHoveredTower != null)
        {
            currentlyHoveredTower.HideSellPrice();
            currentlyHoveredTower.SetRadiusVisible(false);
            currentlyHoveredTower = null;
        }
    }

    private void TrySellHoveredTower()
    {
        Debug.Log("TrySellHoveredTower: Called. currentlyHoveredTower is null? " + (currentlyHoveredTower == null) + ", isSellingTower: " + isSellingTower); // DEBUG
        if (currentlyHoveredTower != null && isSellingTower)
        {
            SellTower(currentlyHoveredTower);
        }
        else if (isSellingTower)
        {
            Debug.Log("TrySellHoveredTower: No tower hovered to sell when clicked."); // DEBUG
        }
        ExitSellMode();
    }

    private void SellTower(Tower towerToSell)
    {
        Debug.Log("SellTower: Called to sell " + (towerToSell != null ? towerToSell.name : "NULL") + "."); // DEBUG
        if (towerToSell == null) return;

        int refundAmount = Mathf.FloorToInt(towerToSell.TowerCost * sellRefundPercentage);
        AddMoney(refundAmount);
        Debug.Log("SellTower: Refunding $" + refundAmount + ". New money: $" + PlayerMoney); // DEBUG

        if (currentlyHoveredTower == towerToSell)
        {
            currentlyHoveredTower.HideSellPrice();
            currentlyHoveredTower = null;
        }

        Destroy(towerToSell.gameObject);
        Debug.Log("SellTower: Tower destroyed."); // DEBUG
    }

    /// <summary>
    /// Sets the material of the preview tower to indicate valid/invalid placement.
    /// </summary>
    /// <param name="previewGO">The preview tower GameObject.</param>
    /// <param name="isValid">True for valid, false for invalid.</param>
    private void SetPreviewMaterial(GameObject previewGO, bool isValid)
    {
        if (previewGO == null) return;

        Renderer previewRenderer = previewGO.GetComponent<Renderer>();
        if (previewRenderer != null)
        {
            if (isValid && previewValidMaterial != null)
            {
                previewRenderer.material = previewValidMaterial;
            }
            else if (!isValid && previewInvalidMaterial != null)
            {
                previewRenderer.material = previewInvalidMaterial;
            }
            else
            {
                Debug.LogWarning("Missing valid/invalid preview materials in GameManager, or preview renderer.", this);
            }
        }
    }

    /// <summary>
    /// Handles detecting if the mouse is hovering over a tower and displays its radius/sell price.
    /// </summary>
    private void HandleTowerHover()
    {
        if (isPlacingTower)
        {
            if (currentlyHoveredTower != null)
            {
                currentlyHoveredTower.SetRadiusVisible(false);
                currentlyHoveredTower.HideSellPrice();
                currentlyHoveredTower = null;
            }
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, towerLayer))
        {
            Tower hitTower = hit.collider.GetComponent<Tower>();

            if (hitTower != null)
            {
                if (currentlyHoveredTower != hitTower)
                {
                    if (currentlyHoveredTower != null)
                    {
                        currentlyHoveredTower.SetRadiusVisible(false);
                        currentlyHoveredTower.HideSellPrice();
                    }
                    currentlyHoveredTower = hitTower;
                    Debug.Log("HandleTowerHover: Hovering new tower: " + currentlyHoveredTower.name); // DEBUG

                    if (isSellingTower)
                    {
                        int refund = Mathf.FloorToInt(currentlyHoveredTower.TowerCost * sellRefundPercentage);
                        currentlyHoveredTower.ShowSellPrice(refund);
                        currentlyHoveredTower.SetRadiusVisible(false);
                        Debug.Log("HandleTowerHover: Displaying sell price for " + currentlyHoveredTower.name + ": $" + refund); // DEBUG
                    }
                    else
                    {
                        currentlyHoveredTower.SetRadiusVisible(true);
                        currentlyHoveredTower.HideSellPrice();
                        Debug.Log("HandleTowerHover: Displaying radius for " + currentlyHoveredTower.name); // DEBUG
                    }
                }
            }
            else
            {
                if (currentlyHoveredTower != null)
                {
                    currentlyHoveredTower.SetRadiusVisible(false);
                    currentlyHoveredTower.HideSellPrice();
                    currentlyHoveredTower = null;
                    Debug.Log("HandleTowerHover: Exited tower hover (hit non-Tower script object)."); // DEBUG
                }
            }
        }
        else
        {
            if (currentlyHoveredTower != null)
            {
                currentlyHoveredTower.SetRadiusVisible(false);
                currentlyHoveredTower.HideSellPrice();
                currentlyHoveredTower = null;
                Debug.Log("HandleTowerHover: Exited tower hover (no hit)."); // DEBUG
            }
        }
    }

    /// <summary>
    /// Updates the health display on the UI.
    /// </summary>
    private void UpdateHealthUI()
    {
        if (healthText != null)
        {
            healthText.text = $"Health: {playerHealth}";
        }
    }

    /// <summary>
    /// Updates the money display on the UI.
    /// </summary>
    private void UpdateMoneyUI()
    {
        if (moneyText != null)
        {
            moneyText.text = $"Money: ${playerMoney}";
        }
    }

    /// <summary>
    /// Updates the round display on the UI.
    /// </summary>
    private void UpdateRoundUI()
    {
        if (roundText != null)
        {
            roundText.text = $"Round: {currentRound}";
        }
    }

    /// <summary>
    /// Handles the end of the game (win or lose).
    /// </summary>
    /// <param name="won">True if the player won, false if they lost.</param>
    public void GameOver(bool won)
    {
        Time.timeScale = 0; // Pause the game
        gameOverPanel.SetActive(true);

        if (won)
        {
            gameOverMessageText.text = "YOU WIN!";
        }
        else
        {
            gameOverMessageText.text = "GAME OVER!";
        }
        Debug.Log(won ? "Game Won!" : "Game Over!");
    }
}