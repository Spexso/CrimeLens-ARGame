using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;


[System.Serializable]
public class DetectedObjectMapping
{
    public string objectName;
    public GameObject prefab;
}

public class CrimeSceneCosmeticsManager : MonoBehaviour
{
    public static CrimeSceneCosmeticsManager Instance;

    [Header("Prefabs")]
    [SerializeField] private GameObject chalkOutlinePrefab;
    [SerializeField] private GameObject yellowBarrierPrefab;

    [Header("AR Components")]
    [SerializeField] private ARRaycastManager arRaycastManager;
    [SerializeField] private ARPlaneManager arPlaneManager;
    [SerializeField] private Camera arCamera;

    [Header("Settings")]
    [SerializeField] private int numberOfBarriers = 4;
    [SerializeField] private float barrierRadius = 1.5f; // Distance from center

    [Header("Audio")]
    [SerializeField] private AudioSource ambientAudioSource;    // Thrilling ambience
    [SerializeField] private AudioSource backgroundAudioSource; // Police radio chatter
    [SerializeField] private AudioClip crimeSceneAmbience;
    [SerializeField] private AudioClip crimeSceneBackground;


    [Header("Detected Object Prefabs")]
    [SerializeField] private List<DetectedObjectMapping> detectedObjectMappings = new List<DetectedObjectMapping>();
    [SerializeField] private GameObject defaultObjectPrefab; // Fallback if specific prefab not found

    [Header("Spawn Settings")]
    [SerializeField] private float objectSpawnRadius = 0.8f; // Distance from chalk outline center
    [SerializeField] private float objectHeightOffset = 0.02f; // Slightly above ground

    // Runtime lookup dictionary
    private Dictionary<string, GameObject> objectNameToPrefab = new Dictionary<string, GameObject>();

    // Track spawned object prefabs
    private List<GameObject> spawnedObjectPrefabs = new List<GameObject>();
    private GameObject activeChalkOutline;
    private List<GameObject> activeBarriers = new List<GameObject>();
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

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

        // Auto-assign components
        if (arRaycastManager == null)
            arRaycastManager = FindFirstObjectByType<ARRaycastManager>();

        if (arPlaneManager == null)
            arPlaneManager = FindFirstObjectByType<ARPlaneManager>();

        if (arCamera == null)
            arCamera = Camera.main;

        // Build object prefab dictionary
        BuildObjectPrefabDictionary();
    }

    private void BuildObjectPrefabDictionary()
    {
        objectNameToPrefab.Clear();

        foreach (DetectedObjectMapping mapping in detectedObjectMappings)
        {
            if (!string.IsNullOrEmpty(mapping.objectName) && mapping.prefab != null)
            {
                // Normalize the key (lowercase, trim whitespace)
                string normalizedKey = mapping.objectName.ToLower().Trim();

                if (!objectNameToPrefab.ContainsKey(normalizedKey))
                {
                    objectNameToPrefab.Add(normalizedKey, mapping.prefab);
                }
                else
                {
                    Debug.LogWarning($"[CrimeSceneCosmetics] Duplicate object name '{mapping.objectName}' found in mappings");
                }
            }
        }

        Debug.Log($"[CrimeSceneCosmetics] Built dictionary with {objectNameToPrefab.Count} object types");
    }

    public void SpawnObjectsAroundChalkOutline(YOLODetection MurderWeapon)
    {
        string objectName = MurderWeapon.objectName;
        if (activeChalkOutline == null)
        {
            Debug.LogError("[CrimeSceneCosmetics] Cannot spawn objects - chalk outline not placed yet");
            return;
        }

        if (MurderWeapon == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] No object names provided to spawn");
            return;
        }

        ClearSpawnedObjects();

        Vector3 chalkCenter = activeChalkOutline.transform.position;

        GameObject prefabToSpawn = GetPrefabForObject(objectName);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[CrimeSceneCosmetics] No prefab found for '{objectName}', skipping");
            return;
        }

        int multiplier = 1;
        if (objectName == "Frying-pan" || objectName == "Chair")
        {
            multiplier = 100;
        }

        float angle = Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * objectSpawnRadius,
            objectHeightOffset * multiplier,
            Mathf.Sin(angle) * objectSpawnRadius
        );
        Vector3 spawnPosition = chalkCenter + offset;

        // Random rotation for variety
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Spawn the prefab
        GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, rotation);
        spawnedObject.name = $"Object_{objectName}";
        spawnedObjectPrefabs.Add(spawnedObject);

        Debug.Log($"[CrimeSceneCosmetics] Spawned '{objectName}' prefab at {spawnPosition}");

        Debug.Log($"[CrimeSceneCosmetics] Spawned {spawnedObjectPrefabs.Count} object prefabs around chalk outline");
    }

    private GameObject GetPrefabForObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return defaultObjectPrefab;

        string normalizedName = objectName.ToLower().Trim();

        if (objectNameToPrefab.TryGetValue(normalizedName, out GameObject prefab))
        {
            return prefab;
        }

        Debug.LogWarning($"[CrimeSceneCosmetics] No specific prefab for '{objectName}', using default");
        return defaultObjectPrefab;
    }

    private void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedObjectPrefabs)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        spawnedObjectPrefabs.Clear();

        Debug.Log("[CrimeSceneCosmetics] Cleared all spawned object prefabs");
    }

    public void SetupCrimeScene()
    {
        Debug.Log("[CrimeSceneCosmetics] Setting up crime scene...");

        // Clear any existing cosmetics
        ClearCosmetics();

        // Find ground position
        Vector3 centerPosition = FindGroundPosition();

        if (centerPosition != Vector3.zero)
        {
            PlaceChalkOutline(centerPosition);
            PlaceYellowBarriers(centerPosition);
        }
        else
        {
            Debug.LogWarning("[CrimeSceneCosmetics] Could not find ground plane - will retry");
        }
    }

    private Vector3 FindGroundPosition()
    {
        // Try screen center first
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (arRaycastManager != null && arRaycastManager.Raycast(screenCenter, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            return raycastHits[0].pose.position;
        }

        // Fallback: Find largest horizontal plane
        ARPlane largestPlane = FindLargestHorizontalPlane();
        if (largestPlane != null)
        {
            return largestPlane.center;
        }

        // Last resort: Place in front of camera
        if (arCamera != null)
        {
            return arCamera.transform.position + arCamera.transform.forward * 2f;
        }

        return Vector3.zero;
    }

    private ARPlane FindLargestHorizontalPlane()
    {
        ARPlane[] planes = FindObjectsByType<ARPlane>(FindObjectsSortMode.None);
        ARPlane largest = null;
        float maxArea = 0f;

        foreach (ARPlane plane in planes)
        {
            if (plane.alignment == PlaneAlignment.HorizontalUp)
            {
                float area = plane.size.x * plane.size.y;
                if (area > maxArea)
                {
                    maxArea = area;
                    largest = plane;
                }
            }
        }

        return largest;
    }

    private void PlaceChalkOutline(Vector3 centerPosition)
    {
        if (chalkOutlinePrefab == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] Chalk outline prefab not assigned");
            return;
        }

        // Place slightly above ground
        Vector3 position = centerPosition + Vector3.up * 0.01f;
        Quaternion rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);

        activeChalkOutline = Instantiate(chalkOutlinePrefab, position, rotation);
        activeChalkOutline.name = "ChalkOutline";

        Debug.Log($"[CrimeSceneCosmetics] Placed chalk outline at {position}");
    }

    private void PlaceYellowBarriers(Vector3 centerPosition)
    {
        if (yellowBarrierPrefab == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] Yellow barrier prefab not assigned");
            return;
        }

        // Place barriers in a circle around the center
        float angleStep = 360f / numberOfBarriers;

        for (int i = 0; i < numberOfBarriers; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * barrierRadius;
            Vector3 position = centerPosition + offset;

            // Rotate barrier to face center
            Quaternion rotation = Quaternion.LookRotation(centerPosition - position);

            GameObject barrier = Instantiate(yellowBarrierPrefab, position, rotation);
            barrier.name = $"Barrier_{i + 1}";
            activeBarriers.Add(barrier);
        }

        Debug.Log($"[CrimeSceneCosmetics] Placed {numberOfBarriers} yellow barriers");
    }

    public void PlayBackgroundSound()
    {
        if (backgroundAudioSource == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] background audio source not assigned");
            return;
        }

        if (crimeSceneBackground == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] Crime scene background clip not assigned");
            return;
        }

        backgroundAudioSource.clip = crimeSceneBackground;
        backgroundAudioSource.loop = true;
        backgroundAudioSource.volume = 0.2f;
        backgroundAudioSource.Play();
    }

    public void PlayAmbientSound()
    {
        if (ambientAudioSource == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] Ambient audio source not assigned");
            return;
        }

        if (crimeSceneAmbience == null)
        {
            Debug.LogWarning("[CrimeSceneCosmetics] Crime scene ambience clip not assigned");
            return;
        }

        ambientAudioSource.clip = crimeSceneAmbience;
        ambientAudioSource.loop = true;
        ambientAudioSource.volume = 0.3f; // Subtle background ambience
        ambientAudioSource.Play();

        Debug.Log("[CrimeSceneCosmetics] Playing ambient crime scene audio");
    }

    public void StopAmbientSound()
    {
        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Stop();
        }
    }

    public void StopBackgroundSound()
    {
        if (backgroundAudioSource != null && backgroundAudioSource.isPlaying)
        {
            backgroundAudioSource.Stop();
        }
    }

    public void ClearCosmetics()
    {
        // Destroy chalk outline
        if (activeChalkOutline != null)
        {
            Destroy(activeChalkOutline);
            activeChalkOutline = null;
        }

        // Destroy all barriers
        foreach (GameObject barrier in activeBarriers)
        {
            if (barrier != null)
            {
                Destroy(barrier);
            }
        }
        activeBarriers.Clear();

        // Destroy all spawned objects
        ClearSpawnedObjects();

        // Stop audio
        StopAmbientSound();

        Debug.Log("[CrimeSceneCosmetics] Cleared all cosmetics");
    }
}