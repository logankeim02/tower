using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using System.Collections; // Required for IEnumerator (coroutines)
using UnityEngine.UI; // REQUIRED for ScrollRect component!

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
    [Tooltip("The LayerMask for areas where towers cannot be built (e.g., enemy path).")]
    [SerializeField] private LayerMask noBuildZoneLayer;
    // --- REMOVED: previewValidMaterial, as we'll use the original ---
    // [Tooltip("Material for the tower preview when placement is valid.")]
    // [SerializeField] private Material previewValidMaterial;
    // -------------------------------------------------------------
    [Tooltip("Material for the tower preview when placement is invalid.")]
    [SerializeField] private Material previewInvalidMaterial;

    [Header("Sell Settings")]
    [Tooltip("Percentage of original cost refunded when selling a tower (0.0 to 1.0).")]
    [SerializeField] private float sellRefundPercentage = 0.5f; // 50% refund

    // Tower Purchase Options (for specific buttons/slots)
    [Header("Tower Purchase Options")]
    [Tooltip("The ID of the basic tower type that the Light Turret slot buys.")]
    [SerializeField] private string lightTurretID = "BasicTower"; // Identifier for the type of tower

    // Scrolling UI Variables
    [Header("Scroll Menu Settings")]
    [Tooltip("Reference to the Scroll Rect component of the turret buy menu.")]
    [SerializeField] private ScrollRect turretBuyScrollRect;
    [Tooltip("How much to scroll up/down per button click (0.0 to 1.0, 1.0 is full height).")]
    [SerializeField] private float scrollAmountPerClick = 0.2f; // Scrolls 20% of the view height per click

    // --- NEW: Store original tower material ---
    private Material originalTowerMaterial;
    // ------------------------------------------

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
        // Singleton pattern enforcement
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Debug logs for UI elements
        if (healthText == null) Debug.LogError("Awake: healthText is NULL in GameManager Inspector!", this);
        else Debug.Log("Awake: healthText in GameManager references: " + healthText.name + " (Instance ID: " + healthText.GetInstanceID() + ")");
        if (moneyText == null) Debug.LogError("Awake: moneyText is NULL in GameManager Inspector!", this);
        else Debug.Log("Awake: moneyText in GameManager references: " + moneyText.name + " (Instance ID: " + moneyText.GetInstanceID() + ")");
        if (roundText == null) Debug.LogError("Awake: roundText is NULL in GameManager Inspector!", this);
        else Debug.Log("Awake: roundText in GameManager references: " + roundText.name + " (Instance ID: " + roundText.GetInstanceID() + ")");
        
        // Debug logs for ScrollRect
        if (turretBuyScrollRect == null) Debug.LogError("Awake: turretBuyScrollRect is NULL in GameManager Inspector! Scroll buttons will not work.", this);
        else Debug.Log("Awake: turretBuyScrollRect in GameManager references: " + turretBuyScrollRect.name + " (Instance ID: " + turretBuyScrollRect.GetInstanceID() + ")");

        // --- NEW: Get original material from basicTowerPrefab ---
        if (basicTowerPrefab != null)
        {
            Renderer prefabRenderer = basicTowerPrefab.GetComponent<Renderer>();
            if (prefabRenderer != null)
            {
                originalTowerMaterial = prefabRenderer.sharedMaterial;
                Debug.Log($"Awake: Stored originalTowerMaterial: {originalTowerMaterial?.name}");
            }
            else
            {
                Debug.LogWarning("Awake: basicTowerPrefab has no Renderer component to get original material.", this);
            }
        }
        else
        {
            Debug.LogError("Awake: basicTowerPrefab is NULL! Cannot store original material.", this);
        }
        // --------------------------------------------------------

        // Initial UI updates
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

        Debug.Log("Start: Calling UpdateHealthUI with playerHealth: " + playerHealth);
        Debug.Log("Start: Calling UpdateMoneyUI with playerMoney: " + playerMoney);
        Debug.Log("Start: Calling UpdateRoundUI with currentRound: " + currentRound);

        UpdateHealthUI();
        UpdateMoneyUI();
        UpdateRoundUI();

        NextRound(); // This will start Round 1
    }

    private void Update()
    {
        HandleTowerPlacement();
        HandleTowerHover();

        if (Input.GetMouseButtonDown(0))
        {
            if (isSellingTower)
            {
                Debug.Log("Update: Left click detected in sell mode. Calling TrySellHoveredTower().");
                TrySellHoveredTower();
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("Update: Right click detected. Cancelling modes.");
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

    /// <summary>
    /// Called when the Light Turret buy slot is clicked.
    /// </summary>
    public void BuyLightTurret()
    {
        Debug.Log("BuyLightTurret button clicked. Attempting to place Light Turret.");
        StartPlacingBasicTower();
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
                previewTowerComponent.enabled = false;
            }

            SetPreviewMaterial(currentPlacingTowerPreview, false); // Start with invalid visually

            if (currentlyHoveredTower != null)
            {
                currentlyHoveredTower.SetRadiusVisible(false);
                currentlyHoveredTower.HideSellPrice();
                currentlyHoveredTower = null;
            }

            Debug.Log("StartPlacingBasicTower: Entering placement mode.");
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

        bool hitGround = Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayer);
        Debug.DrawRay(ray.origin, ray.direction * 100f, hitGround ? Color.blue : Color.yellow);
        Debug.Log($"HandleTowerPlacement: Raycast hit ground: {hitGround}. Hit point: {hit.point}");

        bool isValidPlacement = false;

        if (hitGround)
        {
            currentPlacingTowerPreview.transform.position = new Vector3(hit.point.x, basicTowerPrefab.transform.position.y, hit.point.z);

            Collider towerCollider = basicTowerPrefab.GetComponent<Collider>();
            float checkRadius = 0.5f;
            if (towerCollider != null)
            {
                if (towerCollider is SphereCollider sphereCol)
                {
                    checkRadius = sphereCol.radius * basicTowerPrefab.transform.lossyScale.x;
                }
                else if (towerCollider is CapsuleCollider capsuleCol)
                {
                    CapsuleCollider capCol = towerCollider as CapsuleCollider;
                    checkRadius = Mathf.Max(capCol.radius, capCol.height / 2f) * basicTowerPrefab.transform.lossyScale.x;
                }
                else if (towerCollider is BoxCollider boxCol)
                {
                    checkRadius = Mathf.Max(boxCol.size.x, boxCol.size.z) / 2f * basicTowerPrefab.transform.lossyScale.x;
                }
            }
            Debug.Log($"HandleTowerPlacement: Calculated tower checkRadius: {checkRadius}. Tower Prefab Scale: {basicTowerPrefab.transform.localScale}");

            Vector3 overlapCheckPosition = new Vector3(hit.point.x, basicTowerPrefab.transform.position.y + 0.1f, hit.point.z);
            
            Collider[] collidersInNoBuildZone = Physics.OverlapSphere(overlapCheckPosition, checkRadius, noBuildZoneLayer);
            bool overlapsNoBuildZone = collidersInNoBuildZone.Length > 0;
            Debug.Log($"HandleTowerPlacement: Overlaps NoBuildZone: {overlapsNoBuildZone}. Colliders found: {collidersInNoBuildZone.Length}");

            isValidPlacement = !overlapsNoBuildZone;

            SetPreviewMaterial(currentPlacingTowerPreview, isValidPlacement);
        }
        else
        {
            SetPreviewMaterial(currentPlacingTowerPreview, false);
            Debug.Log("HandleTowerPlacement: Raycast did not hit ground layer, placement invalid.");
        }

        if (Input.GetMouseButtonDown(0) && hitGround && isValidPlacement)
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
            newTowerComponent.enabled = true;
        }

        Debug.Log("Tower placed at: " + newTowerGO.transform.position);
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
            Debug.Log("StartSellingTower: Toggling OFF sell mode.");
            ExitSellMode();
            return;
        }

        isSellingTower = true;
        Debug.Log("StartSellingTower: Entered sell mode. isSellingTower = " + isSellingTower);

        if (currentlyHoveredTower != null)
        {
            currentlyHoveredTower.SetRadiusVisible(false);
        }
    }

    public void ExitSellMode()
    {
        if (!isSellingTower) return;

        isSellingTower = false;
        Debug.Log("ExitSellMode: Exited sell mode. isSellingTower = " + isSellingTower);

        if (currentlyHoveredTower != null)
        {
            currentlyHoveredTower.HideSellPrice();
            currentlyHoveredTower.SetRadiusVisible(false);
            currentlyHoveredTower = null;
        }
    }

    private void TrySellHoveredTower()
    {
        Debug.Log("TrySellHoveredTower: Called. currentlyHoveredTower is null? " + (currentlyHoveredTower == null) + ", isSellingTower: " + isSellingTower);
        if (currentlyHoveredTower != null && isSellingTower)
        {
            SellTower(currentlyHoveredTower);
        }
        else if (isSellingTower)
        {
            Debug.Log("TrySellHoveredTower: No tower hovered to sell when clicked.");
        }
        ExitSellMode();
    }

    private void SellTower(Tower towerToSell)
    {
        Debug.Log("SellTower: Called to sell " + (towerToSell != null ? towerToSell.name : "NULL") + ".");
        if (towerToSell == null) return;

        int refundAmount = Mathf.FloorToInt(towerToSell.TowerCost * sellRefundPercentage);
        Debug.Log($"SellTower: TowerCost={towerToSell.TowerCost}, SellRefundPercentage={sellRefundPercentage}, RefundAmount={refundAmount}");

        AddMoney(refundAmount);
        Debug.Log("SellTower: Refunding $" + refundAmount + ". New money: $" + PlayerMoney);

        if (currentlyHoveredTower == towerToSell)
        {
            currentlyHoveredTower.HideSellPrice();
            currentlyHoveredTower = null;
        }

        Destroy(towerToSell.gameObject);
        Debug.Log("SellTower: Tower destroyed.");
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
            if (isValid) // Use original material for valid placement
            {
                if (originalTowerMaterial != null)
                {
                    previewRenderer.material = originalTowerMaterial;
                }
                else
                {
                    // Fallback if original material wasn't set, use a default white/grey
                    previewRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")); // Or "Standard" if not URP
                    Debug.LogWarning("SetPreviewMaterial: Original tower material is NULL, using default URP Lit material.", this);
                }
            }
            else // Use invalid material for invalid placement
            {
                if (previewInvalidMaterial != null)
                {
                    previewRenderer.material = previewInvalidMaterial;
                }
                else
                {
                    // Fallback if invalid material wasn't set, use a default red
                    previewRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.red }; // Or "Standard"
                    Debug.LogWarning("SetPreviewMaterial: Invalid preview material is NULL, using default red material.", this);
                }
            }
        }
        else
        {
            Debug.LogWarning("Preview tower has no Renderer component to set material.", this);
        }
    }

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
                    Debug.Log("HandleTowerHover: Hovering new tower: " + currentlyHoveredTower.name);

                    if (isSellingTower)
                    {
                        int refund = Mathf.FloorToInt(currentlyHoveredTower.TowerCost * sellRefundPercentage);
                        currentlyHoveredTower.ShowSellPrice(refund);
                        currentlyHoveredTower.SetRadiusVisible(false);
                        Debug.Log("HandleTowerHover: Displaying sell price for " + currentlyHoveredTower.name + ": $" + refund);
                    }
                    else
                    {
                        currentlyHoveredTower.SetRadiusVisible(true);
                        currentlyHoveredTower.HideSellPrice();
                        Debug.Log("HandleTowerHover: Displaying radius for " + currentlyHoveredTower.name);
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
                    Debug.Log("HandleTowerHover: Exited tower hover (hit non-Tower script object).");
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
                    Debug.Log("HandleTowerHover: Exited tower hover (no hit).");
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
            healthText.text = $"{playerHealth}";
            Debug.Log($"UpdateHealthUI: Setting HealthText to '{healthText.text}'");
        }
    }

    /// <summary>
    /// Updates the money display on the UI.
    /// </summary>
    private void UpdateMoneyUI()
    {
        if (moneyText != null)
        {
            moneyText.text = $"{playerMoney}";
            Debug.Log($"UpdateMoneyUI: Setting MoneyText to '{moneyText.text}'");
        }
    }

    /// <summary>
    /// Updates the round display on the UI.
    /// </summary>
    private void UpdateRoundUI()
    {
        if (roundText != null)
        {
            roundText.text = $"{currentRound}";
            Debug.Log($"UpdateRoundUI: Setting RoundText to '{roundText.text}'");
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

    // --- Scrolling Methods ---
    /// <summary>
    /// Scrolls the turret buy menu up by a defined amount.
    /// </summary>
    public void ScrollUpTurretMenu()
    {
        if (turretBuyScrollRect != null)
        {
            Debug.Log($"ScrollUpTurretMenu: Current normalized position: {turretBuyScrollRect.verticalNormalizedPosition}"); // DEBUG
            turretBuyScrollRect.verticalNormalizedPosition = Mathf.Min(1f, turretBuyScrollRect.verticalNormalizedPosition + scrollAmountPerClick);
            Debug.Log($"ScrollUpTurretMenu: New normalized position: {turretBuyScrollRect.verticalNormalizedPosition}"); // DEBUG
        }
        else
        {
            Debug.LogWarning("ScrollUpTurretMenu: turretBuyScrollRect is not assigned!", this);
        }
    }

    /// <summary>
    /// Scrolls the turret buy menu down by a defined amount.
    /// </summary>
    public void ScrollDownTurretMenu()
    {
        if (turretBuyScrollRect != null)
        {
            Debug.Log($"ScrollDownTurretMenu: Current normalized position: {turretBuyScrollRect.verticalNormalizedPosition}"); // DEBUG
            turretBuyScrollRect.verticalNormalizedPosition = Mathf.Max(0f, turretBuyScrollRect.verticalNormalizedPosition - scrollAmountPerClick);
            Debug.Log($"ScrollDownTurretMenu: New normalized position: {turretBuyScrollRect.verticalNormalizedPosition}"); // DEBUG
        }
        else
        {
            Debug.LogWarning("ScrollDownTurretMenu: turretBuyScrollRect is not assigned!", this);
        }
    }
}