using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;

public enum GameState
{
    Login,
    PreInvestigation,
    Investigation,
    Results
}

public class GameManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private PreInvestigationManager preInvestigationManager;
    [SerializeField] private InvestigationManager investigationManager;
    [SerializeField] private ResultOverlayManager resultOverlayManager;
    [SerializeField] private CrimeSceneCosmeticsManager crimeSceneCosmeticsManager;
    [Header("AR Components")]
    [SerializeField] private ARCameraManager arCameraManager;
    [SerializeField] private ARSession arSession;

    [Header("Debug/Testing")]
    [SerializeField] private bool useDummyObjectsForTesting = false;
    [SerializeField]
    private List<string> debugDummyObjects = new List<string>
    {
        "knife",
        "scissors",
        "Frying-pan"
    };

    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Login;
    public string Username { get; private set; }
    public string UserId { get; private set; }
    public MysteryData CurrentMystery { get; private set; }

    private List<YOLODetection> detectedObjects = new List<YOLODetection>();

    private YOLODetection correctMurderWeapon = new YOLODetection("", Vector2.zero, new Rect(), 0f);
    private bool weaponFound = false;
    public bool IsSolved { get; set; }
    private float completionTime;
    public bool UseDummyObjectsForTesting => useDummyObjectsForTesting;
    public List<string> DebugDummyObjects => debugDummyObjects;

    void Awake()
    {
#if UNITY_EDITOR
        useDummyObjectsForTesting = false;
#endif
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetState(GameState.Login);
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
        TaggedLogger.LogGameManager($"[GameManager] State: {newState}");

        UIManager.Instance?.ShowScreen(newState);
        OnStateEnter(newState);
    }

    void OnStateEnter(GameState state)
    {
        switch (state)
        {
            case GameState.Login:
                // Disable AR camera on login screen
                SetARCameraActive(false);
                break;

            case GameState.PreInvestigation:
                SetARCameraActive(true);
                // PreInvestigationManager handles the rest
                crimeSceneCosmeticsManager?.PlayBackgroundSound();
                PreInvestigationManager.Instance?.ResetUI();
                break;

            case GameState.Investigation:
                // Investigation screen handles scanning and gameplay
                SetARCameraActive(true);

                if (useDummyObjectsForTesting)
                {
                    YOLOIntegrationManager a = FindAnyObjectByType<YOLOIntegrationManager>();

                    if (a)
                    {
                        foreach (string item in debugDummyObjects)
                        {
                            YOLODetection Dummy = new YOLODetection(item, Vector2.down, Rect.zero, 0);
                            a.HandleSuccessfulScan(Dummy);
                        }
                    }
                }
                break;

            case GameState.Results:
                // Just save to Firebase - UI handles display
                SetARCameraActive(false);
                resultOverlayManager?.UpdateResults();
                crimeSceneCosmeticsManager?.StopAmbientSound();
                FirebaseManager.Instance?.SaveMysteryResult(IsSolved, completionTime);
                break;
        }
    }

    private void SetARCameraActive(bool active)
    {
        if (arCameraManager != null)
        {
            arCameraManager.enabled = active;
            TaggedLogger.LogGameManager($"[GameManager] AR Camera: {(active ? "ENABLED" : "DISABLED")}");
        }

        if (arSession != null)
        {
            arSession.enabled = active;
            TaggedLogger.LogGameManager($"[GameManager] AR Session: {(active ? "ENABLED" : "DISABLED")}");
        }
    }

    public void OnScanComplete(YOLODetection detectedObject)
    {
        // Avoid duplicates
        if (!detectedObjects.Any(d => d.objectName == detectedObject.objectName))
        {
            detectedObjects.Add(detectedObject);
            TaggedLogger.LogGameManager($"[GameManager] Added: {detectedObject.objectName} (Total: {detectedObjects.Count})");
        }
        else
        {
            TaggedLogger.LogGameManager($"[GameManager] {detectedObject.objectName} already detected, skipping");
        }

        // Notify InvestigationManager to add UI entry
        InvestigationManager.Instance?.OnObjectAdded(detectedObject, detectedObjects.Count);
    }

    public void initiateStartGame()
    {
        // Validate minimum objects
        if (detectedObjects.Count < 3)
        {
            TaggedLogger.LogGameManager($"[GameManager] Need 3 objects, only have {detectedObjects.Count}");
            // TODO: Notify InvestigationManager
            // InvestigationManager.Instance?.OnInsufficientObjects(detectedObjects.Count);
            return;
        }

        InvestigationManager.Instance?.OnScanComplete();
        List<string> objectNames = detectedObjects.Select(d => d.objectName).ToList();
        MysteryAPIManager.Instance?.GenerateMystery(objectNames, OnMysterySuccess, OnMysteryError);
    }

    public void OnMysterySuccess(MysteryData mystery)
    {
        CurrentMystery = mystery;
        weaponFound = false;
        completionTime = 0f;

        // Create new instance
        correctMurderWeapon = new YOLODetection(
            mystery.MurderWeapon,
            Vector2.zero,
            new Rect(),
            1.0f
        );

        TaggedLogger.LogGameManager($"[GameManager] Mystery received!");
        TaggedLogger.LogGameManager($"Murder weapon to find: {correctMurderWeapon}");

        ObjectDetectionManager.Instance?.ConfirmMarkers();
        InvestigationManager.Instance?.OnMysteryGenerated(mystery);
        crimeSceneCosmeticsManager?.PlayAmbientSound();
        crimeSceneCosmeticsManager?.SpawnObjectsAroundChalkOutline(correctMurderWeapon);
    }

    void OnMysteryError(string error)
    {
        TaggedLogger.LogGameManager($"[GameManager] Mystery generation failed: {error}");
        ObjectDetectionManager.Instance?.ClearAllMarkers();
        InvestigationManager.Instance?.OnMysteryGenerationFailed(error);
    }

    public void OnLoginComplete(string user, string firebaseId)
    {
        Username = user;
        UserId = firebaseId;
        SetState(GameState.PreInvestigation);
    }

    public void OnMarkerClicked(string objectName)
    {
        if (CurrentMystery == null)
        {
            TaggedLogger.LogGameManager("[GameManager] No mystery generated yet!");
            return;
        }

        if (weaponFound)
        {
            TaggedLogger.LogGameManager($"[GameManager] Murder weapon already found!");
            return;
        }

        if (objectName == correctMurderWeapon.objectName)
        {
            weaponFound = true;
            IsSolved = true;

            TaggedLogger.LogGameManager($"[GameManager] ✓ CORRECT! Found murder weapon: {objectName}");

            InvestigationManager.Instance?.RevealObject(objectName);

            // Player wins immediately
            TaggedLogger.LogGameManager("[GameManager] ✓ Murder weapon found! Player wins!");
            completionTime = investigationManager?.gameplayTimer ?? 0f;
            SetState(GameState.Results);
        }
        else
        {
            weaponFound = false;
            IsSolved = false;

            // Player loses
            TaggedLogger.LogGameManager($"[GameManager] ✗ WRONG! {objectName} is not the murder weapon");
            SetState(GameState.Results);
        }
    }

    public void PlayAgain()
    {
        TaggedLogger.LogGameManager("[GameManager] PlayAgain called - resetting game");

        // Clear all AR markers
        ObjectDetectionManager.Instance?.ClearAllMarkers();

        // Reset all game data
        ResetGameData();

        // Reset investigation screen
        InvestigationManager.Instance?.ResetInvestigation();

        crimeSceneCosmeticsManager?.StopAmbientSound();
        crimeSceneCosmeticsManager?.StopBackgroundSound();
        crimeSceneCosmeticsManager?.ClearCosmetics();

        SetState(GameState.PreInvestigation);
    }

    public void BackToMenu()
    {
        ResetGameData();
        SetState(GameState.Login);
    }

    void ResetGameData()
    {
        TaggedLogger.LogGameManager("[GameManager] Resetting game data");

        detectedObjects.Clear();
        correctMurderWeapon = new YOLODetection("", Vector2.zero, new Rect(), 0f);  // Reset to empty
        weaponFound = false;
        CurrentMystery = null;
        IsSolved = false;
        completionTime = 0f;
    }

    public bool IsWeaponFound()
    {
        return weaponFound;
    }

    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(completionTime / 60f);
        int seconds = Mathf.FloorToInt(completionTime % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    internal bool HasDetectedObject(string objectName)
    {
        return detectedObjects.Any(d => d.objectName.Equals(objectName, System.StringComparison.OrdinalIgnoreCase));
    }
}