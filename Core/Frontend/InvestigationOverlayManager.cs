using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class InvestigationManager : MonoBehaviour
{
    public static InvestigationManager Instance { get; private set; }

    [Header("UI Elements Text")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI mysteryText;

    [Header("UI Elements Button")]
    [SerializeField] private Button scanButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button mysteryLogButton;
    [SerializeField] private Button debugButton;

    [Header("UI Elements Misc")]
    [SerializeField] private GameObject loadingSpinner;
    [SerializeField] private GameObject mysteryLogPanel;
    [SerializeField] private Transform foundObjectsContainer;
    [SerializeField] private GameObject objectEntryPrefab;
    [SerializeField] private List<ObjectSpriteMapping> objectSpriteMappings = new List<ObjectSpriteMapping>();
    [SerializeField] private Sprite defaultObjectSprite;


    [Header("Settings")]
    [SerializeField] private float fakeScanDuration = 3f;
    [SerializeField] private float gameplayTimeLimit = 120f; // 2 minutes

    private Dictionary<string, Sprite> spriteMap;
    private bool isScanning = false;
    private bool isPlaying = false;
    public float gameplayTimer = 0f;
    private bool isVisible = false;
    private int totalScansCompleted = 0;
    private List<GameObject> objectEntryInstances = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (scanButton != null)
            scanButton.onClick.AddListener(OnScanButtonClicked);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);

        if (mysteryLogButton != null)
            mysteryLogButton.onClick.AddListener(OnMysterLogButtonClicked);

        if (debugButton != null)
            debugButton.onClick.AddListener(OnDebugButtonClicked);

        mysteryLogPanel.SetActive(false);

        InitializeUI();
        InitializeSpriteMap();
    }

    private void OnDebugButtonClicked()
    {
        MysteryData testMystery = new MysteryData
        {
            Story = "Debug Mystery: The case of the missing debug button.",
            MurderWeapon = "Chair"
        };
        GameManager.Instance?.OnMysterySuccess(testMystery);
    }

    private string FormatMysteryText(string rawMystery)
    {
        return rawMystery;
    }

    public void ToggleMysteryLog()
    {
        isVisible = !isVisible;
        mysteryLogPanel.SetActive(isVisible);
    }

    public void UpdateMysteryText(string mysteryData)
    {
        if (mysteryText != null)
        {
            mysteryText.text = FormatMysteryText(mysteryData);
        }
    }

    private void OnMysterLogButtonClicked()
    {
        ToggleMysteryLog();
    }

    public void ResetInvestigation()
    {
        Debug.Log("[InvestigationManager] Resetting investigation screen");

        isScanning = false;
        isPlaying = false;
        gameplayTimer = gameplayTimeLimit;
        isVisible = false;
        totalScansCompleted = 0;

        // CHANGED: Clear all object entries
        ClearObjectEntries();

        // Reset mystery log
        if (mysteryLogPanel != null)
            mysteryLogPanel.SetActive(false);

        if (mysteryText != null)
            mysteryText.text = "";

        // Reset scan button
        if (scanButton != null)
        {
            scanButton.interactable = true;
            scanButton.gameObject.SetActive(true);
        }

        // Reset start game button
        if (startGameButton != null)
        {
            startGameButton.interactable = false;
            startGameButton.gameObject.SetActive(true);
        }

        // Reset UI elements
        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = "Scan scene to start the murder mystery";

        if (timerText != null)
            timerText.text = $"Time: {Mathf.FloorToInt(gameplayTimeLimit / 60):00}:{Mathf.FloorToInt(gameplayTimeLimit % 60):00}";
    }

    // Update OnEnable to use the reset method
    private void OnEnable()
    {
        ResetInvestigation();
    }

    private void Update()
    {
        if (isPlaying)
        {
            // Count down timer
            gameplayTimer -= Time.deltaTime;

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(gameplayTimer / 60f);
                int seconds = Mathf.FloorToInt(gameplayTimer % 60f);
                timerText.text = $"Time: {minutes:00}:{seconds:00}";
            }

            // Check if time ran out
            if (gameplayTimer <= 0f)
            {
                OnTimeUp();
            }
        }
    }

    private void InitializeUI()
    {
        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = "Scan scene to start the murder mystery";

        ClearObjectEntries();

        if (startGameButton != null)
            startGameButton.interactable = false;

        if (timerText != null)
            timerText.text = $"Time: {Mathf.FloorToInt(gameplayTimeLimit / 60):00}:{Mathf.FloorToInt(gameplayTimeLimit % 60):00}";
    }

    private void OnScanButtonClicked()
    {
        if (isScanning) return;
        StartCoroutine(ScanProcess());
    }

    private void OnStartGameButtonClicked()
    {
        if (startGameButton != null)
            startGameButton.interactable = false;

        if (scanButton != null)
            scanButton.interactable = false;

        Debug.Log("[InvestigationManager] Start Game button clicked");

        // Trigger mystery generation in GameManager
        GameManager.Instance.initiateStartGame();
    }

    public void OnInsufficientObjects(int currentCount)
    {
        if (statusText != null)
            statusText.text = $"Need at least 3 objects! Currently have {currentCount}. Scan more.";

        if (scanButton != null)
            scanButton.interactable = true;

        Debug.LogWarning($"[InvestigationManager] Insufficient objects: {currentCount}/3");
    }

    private IEnumerator ScanProcess()
    {
        isScanning = true;

        if (scanButton != null)
            scanButton.interactable = false;

        if (loadingSpinner != null)
            loadingSpinner.SetActive(true);

        if (statusText != null)
            statusText.text = "Scanning environment...";

        // YOLO MODE
        yield return new WaitForSeconds(fakeScanDuration);
        YOLOIntegrationManager.Instance?.StartYOLODetection();

        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        isScanning = false;
    }

    public void OnScanFailed(string errorMessage)
    {
        if (scanButton != null)
            scanButton.interactable = true;

        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = $"Scan failed: {errorMessage}";

        isScanning = false;
    }

    public void OnScanComplete()
    {
        // Show generating status
        if (statusText != null)
            statusText.text = "Generating mystery...";

        if (loadingSpinner != null)
            loadingSpinner.SetActive(true);
    }

    public void OnSingleScanComplete()
    {
        isScanning = false;
        totalScansCompleted++;

        // Re-enable scan button for next scan
        if (scanButton != null)
            scanButton.interactable = true;

        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = $"Scan {totalScansCompleted} complete! Scan more or start game.";

        Debug.Log($"[InvestigationManager] Scan session {totalScansCompleted} complete");
    }

    public void OnObjectAdded(YOLODetection newObject, int totalCount)
    {
        // Instantiate ONE new entry for this scan
        if (foundObjectsContainer != null && objectEntryPrefab != null)
        {
            GameObject entry = Instantiate(objectEntryPrefab, foundObjectsContainer);

            // Set object data on entry
            ObjectListEntry entryComponent = entry.GetComponent<ObjectListEntry>();
            if (entryComponent != null)
            {
                entryComponent.Setup(new DetectedObjectItem(newObject.objectName, GetObjectSprite(newObject.objectName)));
            }

            objectEntryInstances.Add(entry);
        }

        // Enable/disable start game button based on total count
        if (startGameButton != null)
        {
            startGameButton.interactable = totalCount >= 3;
        }

        Debug.Log($"[InvestigationManager] Added object entry: {newObject.objectName} (Total: {totalCount})");
    }

    private Sprite GetObjectSprite(string objectName)
    {
        if (spriteMap == null)
        {
            Debug.LogWarning("[InvestigationManager] Sprite map not initialized!");
            return defaultObjectSprite;
        }

        // Try to find sprite (case-insensitive)
        string key = objectName.ToLower();
        if (spriteMap.ContainsKey(key))
        {
            return spriteMap[key];
        }

        // Fallback to default sprite
        Debug.LogWarning($"[InvestigationManager] No sprite found for '{objectName}', using default");
        return defaultObjectSprite;
    }

    public void OnMysteryGenerated(MysteryData mystery)
    {
        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (scanButton != null)
            scanButton.gameObject.SetActive(false);

        UpdateMysteryText(mystery.Story);

        // Start gameplay
        StartGameplay();
    }

    public void OnMysteryGenerationFailed(string errorMessage)
    {
        if (scanButton != null)
            scanButton.interactable = true;

        if (startGameButton != null)
            startGameButton.interactable = true;

        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = $"Error: {errorMessage}. Try scanning again.";
    }

    private void StartGameplay()
    {
        isPlaying = true;
        gameplayTimer = gameplayTimeLimit;

        if (statusText != null)
            statusText.text = "Find the murder weapon before time runs out!";

        Debug.Log("[InvestigationManager] Gameplay started!");
    }

    public void RevealObject(string objectName)
    {
        Debug.Log($"[InvestigationManager] Murder weapon found: {objectName}");

        if (statusText != null)
            statusText.text = $"Found the murder weapon: {objectName}!";
    }

    private void OnTimeUp()
    {
        isPlaying = false;

        Debug.Log("[InvestigationManager] Time's up! Player lost.");
        GameManager.Instance.SetState(GameState.Results);
    }

    private void ClearObjectEntries()
    {
        foreach (GameObject entry in objectEntryInstances)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }

        objectEntryInstances.Clear();
    }

    // Build dictionary for fast sprite lookup
    private void InitializeSpriteMap()
    {
        spriteMap = new Dictionary<string, Sprite>();

        foreach (var mapping in objectSpriteMappings)
        {
            if (!string.IsNullOrEmpty(mapping.objectName) && mapping.sprite != null)
            {
                // Convert to lowercase for case-insensitive matching
                spriteMap[mapping.objectName.ToLower()] = mapping.sprite;
            }
        }

        Debug.Log($"[InvestigationManager] Initialized sprite map with {spriteMap.Count} entries");
    }
}


[Serializable]
public class ObjectSpriteMapping
{
    public string objectName;
    public Sprite sprite;
}