using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using System.Collections; // Required for IEnumerator (coroutines)
using System.Collections.Generic; // REQUIRED for Lists
using UnityEngine.UI; // REQUIRED for UI components like Button

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
    [Tooltip("Reference to the UI Text element displaying the enemy counter.")]
    [SerializeField] private TextMeshProUGUI enemyCounterText;
    [Tooltip("Reference to the UI Text element displaying the win/lose message.")]
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("Reference to the UI Text element inside the game over panel.")]
    [SerializeField] private TextMeshProUGUI gameOverMessageText;
    // --- NEW: UI Button Reference ---
    [Tooltip("Reference to the button that starts the next round.")]
    [SerializeField] private Button startRoundButton;
    // ---------------------------------

    // --- NEW: Audio Source Reference ---
    [Header("Audio")]
    [Tooltip("The AudioSource that plays a sound when the round starts.")]
    [SerializeField] private AudioSource startRoundAudioSource;
    // -----------------------------------

    [Header("Round & Wave Settings")]
    [Tooltip("Assign all the Wave ScriptableObjects here in the order they should appear.")]
    [SerializeField] private List<Wave> waves;
    [Tooltip("The delay in seconds after starting a round before the first enemy spawns.")]
    [SerializeField] private float startWaveDelay = 1f;
    [Tooltip("The time in seconds between each enemy spawn during a wave.")]
    [SerializeField] private float enemySpawnInterval = 0.5f;

    private int enemiesSpawnedInWave = 0;
    private int enemiesRemainingInWave = 0;

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
    [Tooltip("Material for the tower preview when placement is invalid.")]
    [SerializeField] private Material previewInvalidMaterial;

    [Header("Sell Settings")]
    [Tooltip("Percentage of original cost refunded when selling a tower (0.0 to 1.0).")]
    [SerializeField] private float sellRefundPercentage = 0.5f; // 50% refund

    [Header("Tower Purchase Options")]
    [Tooltip("The ID of the basic tower type that the Light Turret slot buys.")]
    [SerializeField] private string lightTurretID = "BasicTower"; // Identifier for the type of tower

    [Header("Scroll Menu Settings")]
    [Tooltip("Reference to the Scroll Rect component of the turret buy menu.")]
    [SerializeField] private ScrollRect turretBuyScrollRect;
    [Tooltip("How much to scroll up/down per button click (0.0 to 1.0, 1.0 is full height).")]
    [SerializeField] private float scrollAmountPerClick = 0.2f; // Scrolls 20% of the view height per click

    private Material originalTowerMaterial;

    private int currentRound = 0;
    private Transform[] pathWaypoints;
    private Coroutine currentWaveCoroutine;

    private bool isPlacingTower = false;
    private GameObject currentPlacingTowerPreview;
    private Tower currentlyHoveredTower;

    private bool isSellingTower = false;
    // --- NEW: Game State Flag ---
    private bool isWaveInProgress = false;

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

        // --- NEW: Add a check for the button ---
        if (startRoundButton == null) Debug.LogError("Awake: startRoundButton is NULL in GameManager Inspector!", this);
        // -----------------------------------------

        if (healthText == null) Debug.LogError("Awake: healthText is NULL in GameManager Inspector!", this);
        if (moneyText == null) Debug.LogError("Awake: moneyText is NULL in GameManager Inspector!", this);
        if (roundText == null) Debug.LogError("Awake: roundText is NULL in GameManager Inspector!", this);
        if (enemyCounterText == null) Debug.LogError("Awake: enemyCounterText is NULL in GameManager Inspector! Enemy counter will not update.", this);
        if (turretBuyScrollRect == null) Debug.LogWarning("Awake: turretBuyScrollRect is NULL in GameManager Inspector! Scroll buttons will not work.", this);

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

        // --- MODIFIED: Prepare the very first round instead of auto-starting it ---
        PrepareNextRound();
    }

    private void Update()
    {
        HandleTowerPlacement();
        HandleTowerHover();

        if (Input.GetMouseButtonDown(0))
        {
            if (isSellingTower)
            {
                TrySellHoveredTower();
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
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

    // --- RENAMED & MODIFIED: Was 'NextRound', now prepares the round and shows the button ---
    private void PrepareNextRound()
    {
        // Check if player has beaten all defined waves
        if (currentRound >= waves.Count)
        {
            GameOver(true); // Player wins!
            return;
        }

        currentRound++;
        UpdateRoundUI();
        Debug.Log($"Preparing Round: {currentRound}");

        // Get info for the upcoming wave to display on the UI
        Wave nextWave = waves[currentRound - 1];
        enemiesRemainingInWave = nextWave.TotalEnemies;
        UpdateEnemyCounterUI();

        startRoundButton.gameObject.SetActive(true); // Show the start button
    }

    // --- NEW: Public method for the START ROUND button to call ---
    public void StartRound()
    {
        // Prevent starting a round if one is already active
        if (isWaveInProgress) return;
        
        isWaveInProgress = true;
        startRoundButton.gameObject.SetActive(false); // Hide the button

        // Play sound effect if assigned
        if (startRoundAudioSource != null)
        {
            startRoundAudioSource.Play();
        }

        // Get the current wave and start spawning enemies
        Wave currentWave = waves[currentRound - 1];
        enemiesSpawnedInWave = currentWave.TotalEnemies;
        UpdateEnemyCounterUI(); // Update counter text to "Enemies:"

        if (currentWaveCoroutine != null)
        {
            StopCoroutine(currentWaveCoroutine);
        }
        currentWaveCoroutine = StartCoroutine(SpawnWave(currentWave));
    }


    private IEnumerator SpawnWave(Wave waveToSpawn)
    {
        yield return new WaitForSeconds(startWaveDelay);

        Debug.Log($"Spawning wave for Round {currentRound} with {waveToSpawn.TotalEnemies} enemies.");

        // Iterate through each group of enemies defined in the wave
        foreach (EnemyGroup group in waveToSpawn.enemyGroups)
        {
            if (group.enemyPrefab == null)
            {
                Debug.LogError($"Wave {currentRound} has an enemy group with a missing prefab! Skipping group.");
                continue;
            }

            // For each group, spawn the specified number of that enemy type
            for (int i = 0; i < group.count; i++)
            {
                if (playerHealth <= 0) yield break; // Stop if player is dead

                SpawnEnemy(group.enemyPrefab); // Pass the specific prefab to spawn
                yield return new WaitForSeconds(enemySpawnInterval);
            }
        }

        Debug.Log($"Round {currentRound} enemy spawning complete. Waiting for enemies to clear.");
    }
    
    private void SpawnEnemy(GameObject enemyPrefabToSpawn)
    {
        if (enemyPrefabToSpawn == null)
        {
            Debug.LogError("Attempted to spawn a NULL enemy prefab!");
            return;
        }
        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogError("Enemy path waypoints not set in GameManager!");
            return;
        }

        GameObject enemyGO = Instantiate(enemyPrefabToSpawn, pathWaypoints[0].position, Quaternion.identity);
        Enemy enemy = enemyGO.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.SetPath(pathWaypoints);
        }
        else
        {
            Debug.LogError($"Instantiated enemy prefab '{enemyPrefabToSpawn.name}' does not have an Enemy script!");
            Destroy(enemyGO);
        }
    }

    public void EnemyDefeated()
    {
        enemiesRemainingInWave--;
        enemiesRemainingInWave = Mathf.Max(0, enemiesRemainingInWave);
        UpdateEnemyCounterUI();

        Debug.Log($"Enemy Defeated! {enemiesRemainingInWave} remaining.");

        // --- MODIFIED: Check if wave is over using the new flag ---
        if (enemiesRemainingInWave <= 0 && isWaveInProgress)
        {
            isWaveInProgress = false; // Mark wave as over
            Debug.Log("All enemies defeated! Preparing next round...");
            StartCoroutine(StartNextRoundWithDelay(3f));
        }
    }

    private IEnumerator StartNextRoundWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // --- MODIFIED: Now calls PrepareNextRound to set up for the player ---
        PrepareNextRound();
    }

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

            SetPreviewMaterial(currentPlacingTowerPreview, false);

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

        bool isValidPlacement = false;

        if (hitGround)
        {
            currentPlacingTowerPreview.transform.position = new Vector3(hit.point.x, basicTowerPrefab.transform.position.y, hit.point.z);

            Collider towerCollider = basicTowerPrefab.GetComponent<Collider>();
            float checkRadius = 0.5f;
            if (towerCollider != null)
            {
                if (towerCollider is SphereCollider sphereCol) checkRadius = sphereCol.radius * basicTowerPrefab.transform.lossyScale.x;
                else if (towerCollider is CapsuleCollider capCol) checkRadius = Mathf.Max(capCol.radius, capCol.height / 2f) * basicTowerPrefab.transform.lossyScale.x;
                else if (towerCollider is BoxCollider boxCol) checkRadius = Mathf.Max(boxCol.size.x, boxCol.size.z) / 2f * basicTowerPrefab.transform.lossyScale.x;
            }

            Vector3 overlapCheckPosition = new Vector3(hit.point.x, basicTowerPrefab.transform.position.y + 0.1f, hit.point.z);
            
            Collider[] collidersInNoBuildZone = Physics.OverlapSphere(overlapCheckPosition, checkRadius, noBuildZoneLayer);
            bool overlapsNoBuildZone = collidersInNoBuildZone.Length > 0;

            isValidPlacement = !overlapsNoBuildZone;

            SetPreviewMaterial(currentPlacingTowerPreview, isValidPlacement);
        }
        else
        {
            SetPreviewMaterial(currentPlacingTowerPreview, false);
        }

        if (Input.GetMouseButtonDown(0) && hitGround && isValidPlacement)
        {
            PlaceTower(hit.point);
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

        // Finalize placement
        isPlacingTower = false;
        Destroy(currentPlacingTowerPreview);
        currentPlacingTowerPreview = null;
    }

    public void CancelPlacement()
    {
        if (!isPlacingTower) return;
        
        AddMoney(basicTowerCost); // Refund the money since placement was cancelled

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
    }

    public void StartSellingTower()
    {
        if (isPlacingTower) CancelPlacement();

        isSellingTower = !isSellingTower; // Toggle sell mode
        
        // If we just turned sell mode OFF, hide any sell price text
        if (!isSellingTower && currentlyHoveredTower != null)
        {
            currentlyHoveredTower.HideSellPrice();
        }
    }

    public void ExitSellMode()
    {
        if (!isSellingTower) return;

        isSellingTower = false;

        if (currentlyHoveredTower != null)
        {
            currentlyHoveredTower.HideSellPrice();
            currentlyHoveredTower.SetRadiusVisible(false); // Can also hide radius
            currentlyHoveredTower = null;
        }
    }

    private void TrySellHoveredTower()
    {
        if (currentlyHoveredTower != null && isSellingTower)
        {
            SellTower(currentlyHoveredTower);
        }
        ExitSellMode();
    }

    private void SellTower(Tower towerToSell)
    {
        if (towerToSell == null) return;

        int refundAmount = Mathf.FloorToInt(towerToSell.TowerCost * sellRefundPercentage);
        AddMoney(refundAmount);

        if (currentlyHoveredTower == towerToSell)
        {
            currentlyHoveredTower = null;
        }

        Destroy(towerToSell.gameObject);
    }
    
    private void SetPreviewMaterial(GameObject previewGO, bool isValid)
    {
        if (previewGO == null) return;

        Renderer previewRenderer = previewGO.GetComponent<Renderer>();
        if (previewRenderer != null)
        {
            if (isValid)
            {
                if (originalTowerMaterial != null)
                {
                    previewRenderer.material = originalTowerMaterial;
                }
                else
                {
                    previewRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    Debug.LogWarning("SetPreviewMaterial: Original tower material is NULL, using default URP Lit material.", this);
                }
            }
            else
            {
                if (previewInvalidMaterial != null)
                {
                    previewRenderer.material = previewInvalidMaterial;
                }
                else
                {
                    previewRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.red };
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
                // If we are hovering over a new tower
                if (currentlyHoveredTower != hitTower)
                {
                    // Un-highlight the old tower
                    if (currentlyHoveredTower != null)
                    {
                        currentlyHoveredTower.SetRadiusVisible(false);
                        currentlyHoveredTower.HideSellPrice();
                    }
                    
                    currentlyHoveredTower = hitTower;

                    // Highlight the new tower
                    currentlyHoveredTower.SetRadiusVisible(true);
                    if (isSellingTower)
                    {
                        int refund = Mathf.FloorToInt(currentlyHoveredTower.TowerCost * sellRefundPercentage);
                        currentlyHoveredTower.ShowSellPrice(refund);
                    }
                }
            }
            // If our raycast hits something that isn't a tower (but is on the tower layer)
            else
            {
                if (currentlyHoveredTower != null)
                {
                    currentlyHoveredTower.SetRadiusVisible(false);
                    currentlyHoveredTower.HideSellPrice();
                    currentlyHoveredTower = null;
                }
            }
        }
        // If our raycast hits nothing
        else
        {
            if (currentlyHoveredTower != null)
            {
                    currentlyHoveredTower.SetRadiusVisible(false);
                    currentlyHoveredTower.HideSellPrice();
                    currentlyHoveredTower = null;
            }
        }
    }
    
    private void UpdateHealthUI() { if (healthText != null) healthText.text = $"{playerHealth}"; }
    private void UpdateMoneyUI() { if (moneyText != null) moneyText.text = $"{playerMoney}"; }
    private void UpdateRoundUI() { if (roundText != null) roundText.text = $"{currentRound}"; }
    
    // --- MODIFIED: Enemy counter text now changes based on game state ---
    private void UpdateEnemyCounterUI()
    {
        if (enemyCounterText != null)
        {
            if (isWaveInProgress)
            {
                enemyCounterText.text = $"Enemies: {enemiesRemainingInWave}";
            }
            else
            {
                // Shows the count for the upcoming wave during intermission
                enemyCounterText.text = $"Next Wave: {enemiesRemainingInWave}";
            }
        }
    }
    
    public void GameOver(bool won)
    {
        Time.timeScale = 0;
        gameOverPanel.SetActive(true);

        if (won)
        {
            gameOverMessageText.text = "YOU WIN!";
        }
        else
        {
            gameOverMessageText.text = "GAME OVER!";
        }
    }
    
    public void ScrollUpTurretMenu()
    {
        if (turretBuyScrollRect != null)
        {
            turretBuyScrollRect.verticalNormalizedPosition = Mathf.Min(1f, turretBuyScrollRect.verticalNormalizedPosition + scrollAmountPerClick);
        }
    }

    public void ScrollDownTurretMenu()
    {
        if (turretBuyScrollRect != null)
        {
            turretBuyScrollRect.verticalNormalizedPosition = Mathf.Max(0f, turretBuyScrollRect.verticalNormalizedPosition - scrollAmountPerClick);
        }
    }
}