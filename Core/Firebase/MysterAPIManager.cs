using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Functions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public class MysteryAPIManager : MonoBehaviour
{
    public static MysteryAPIManager Instance { get; private set; }

    private FirebaseFunctions functions;

    private void Awake()
    {
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

    private void Start()
    {
        StartCoroutine(WaitForFirebaseInit());
    }

    private IEnumerator WaitForFirebaseInit()
    {
        yield return new WaitUntil(() => FirebaseManager.Instance != null && FirebaseManager.Instance.IsInitialized);

        functions = FirebaseFunctions.DefaultInstance;
        Debug.Log("[Mystery API] Ready!");
    }

    public void GenerateMystery(List<string> detectedObjects, Action<MysteryData> onSuccess, Action<string> onError)
    {
        StartCoroutine(GenerateMysteryCoroutine(detectedObjects, onSuccess, onError));
    }

    private IEnumerator GenerateMysteryCoroutine(
        List<string> objectNames,
        Action<MysteryData> onSuccess,
        Action<string> onError)
    {
        Debug.Log($"[Mystery API] Generating mystery with {objectNames.Count} objects...");

        var requestData = new Dictionary<string, object>
    {
        { "detectedObjects", objectNames }
    };

        var function = FirebaseFunctions.DefaultInstance.GetHttpsCallable("generateMystery");
        var task = function.CallAsync(requestData);

        yield return new WaitUntil(() => task.IsCompleted);

        //  WRAP ENTIRE ERROR HANDLING IN TRY-CATCH
        if (task.IsFaulted)
        {
            string errorMsg = "Mystery generation failed";

            try
            {
                // Try to extract error message
                if (task.Exception != null)
                {
                    if (task.Exception.InnerException != null)
                    {
                        errorMsg = task.Exception.InnerException.Message;
                    }
                    else
                    {
                        errorMsg = task.Exception.Message;
                    }
                }

                Debug.LogError($"[Mystery API] Error: {errorMsg}");
            }
            catch (Exception ex)
            {
                // If extracting error message crashes, use generic message
                Debug.LogError($"[Mystery API] Failed to parse error: {ex.Message}");
                errorMsg = "Mystery generation failed (error details unavailable)";
            }

            //  SAFELY INVOKE ERROR CALLBACK
            try
            {
                onError?.Invoke(errorMsg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mystery API] Error callback crashed: {ex.Message}");
            }

            yield break;
        }

        //  WRAP SUCCESS HANDLING IN TRY-CATCH TOO
        try
        {
            object rawData = task.Result.Data;
            string jsonString = JsonConvert.SerializeObject(rawData);

            Debug.Log($"[Mystery API] Raw JSON: {jsonString}");

            JObject root = JObject.Parse(jsonString);

            if (root["data"] != null)
            {
                MysteryData mystery = root["data"].ToObject<MysteryData>();

                Debug.Log($"[Mystery API]  Mystery Generated!");
                Debug.Log($"  Story: {mystery.Story}");
                Debug.Log($"  Victim: {mystery.Victim}");
                Debug.Log($"  Murder Weapon: {mystery.MurderWeapon}");

                try
                {
                    onSuccess?.Invoke(mystery);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Mystery API] Success callback crashed: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError("[Mystery API] Response missing 'data' field");

                try
                {
                    onError?.Invoke("Invalid response format: Missing 'data'");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Mystery API] Error callback crashed: {ex.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Mystery API] Parse error: {e.Message}");

            try
            {
                onError?.Invoke($"Parse error: {e.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mystery API] Error callback crashed: {ex.Message}");
            }
        }
    }
}