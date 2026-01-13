using UnityEngine;

public class TaggedLogger : MonoBehaviour
{
    private static ILogger logger = Debug.unityLogger;
    
    // Define custom tags for different systems
    private const string TAG_FIREBASE = "Firebase";
    private const string TAG_DETECTION = "ObjectDetection";
    private const string TAG_AR = "ARSystem";
    private const string TAG_MYSTERY = "MysteryAPI";
    private const string TAG_UI = "UIManager";

    private const string TAG_GAMEMANAGER = "GameManager";
    private const string TAG_YOLOINTEGRATIONMANAGER = "YOLOIntegrationManager";
    private const string TAG_NEWYOLODETECTION = "YOLODetection";
    private const string TAG_OBJECTDETECTIONMANAGER = "ObjectDetectionManager";

    // Log methods with tags
    public static void LogGameManager(object message) => logger.Log(TAG_GAMEMANAGER, message);
    public static void LogYOLOIntegrationManager(object message) => logger.Log(TAG_YOLOINTEGRATIONMANAGER, message);
    public static void LogNEWYOLODetection(object message) => logger.Log(TAG_NEWYOLODETECTION, message);
    public static void LogObjectDetectionManager(object message) => logger.Log(TAG_OBJECTDETECTIONMANAGER, message);


    public static void LogFirebase(object message) => logger.Log(TAG_FIREBASE, message);
    public static void LogDetection(object message) => logger.Log(TAG_DETECTION, message);
    public static void LogAR(object message) => logger.Log(TAG_AR, message);
    public static void LogMystery(object message) => logger.Log(TAG_MYSTERY, message);
    public static void LogUI(object message) => logger.Log(TAG_UI, message);

    // Error variants
    public static void LogFirebaseError(object message) => logger.LogError(TAG_FIREBASE, message);
    public static void LogDetectionError(object message) => logger.LogError(TAG_DETECTION, message);
    public static void LogDetectionWarning(object message) => logger.LogWarning(TAG_DETECTION, message);
    public static void LogARError(object message) => logger.LogError(TAG_AR, message);
}