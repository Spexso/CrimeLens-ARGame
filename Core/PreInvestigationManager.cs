using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PreInvestigationManager : MonoBehaviour
{
    public static PreInvestigationManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button setupSceneButton;
    [SerializeField] private GameObject loadingSpinner;

    [Header("Settings")]
    [SerializeField] private float setupDuration = 3f;
    [SerializeField] private float autoAdvanceDelay = 1f; // Delay before auto-advancing


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
        if (setupSceneButton != null)
            setupSceneButton.onClick.AddListener(OnSetupSceneButtonClicked);
    }

    private void OnSetupSceneButtonClicked()
    {
        Debug.Log("[PreInvestigation] Setup Scene button clicked");

        if (setupSceneButton != null)
            setupSceneButton.interactable = false;

        if (statusText != null)
            statusText.text = "Setting up crime scene...";

        if (loadingSpinner != null)
            loadingSpinner.SetActive(true);

        // Trigger cosmetics placement
        CrimeSceneCosmeticsManager.Instance?.SetupCrimeScene();

        // Start setup timer
        StartCoroutine(SceneSetupCoroutine());
    }


    private void OnEnable()
    {
        ResetUI();
    }

    public void ResetUI()
    {
        if (statusText != null)
            statusText.text = "Tap 'Setup Crime Scene' to begin";

        if (setupSceneButton != null)
        {
            setupSceneButton.interactable = true;
            setupSceneButton.gameObject.SetActive(true);
        }

        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        Debug.Log("[PreInvestigation] UI reset");
    }

    private IEnumerator SceneSetupCoroutine()
    {
        yield return new WaitForSeconds(setupDuration);

        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = "Crime scene ready. Starting investigation...";

        if (setupSceneButton != null)
            setupSceneButton.gameObject.SetActive(false);

        Debug.Log("[PreInvestigation] Scene setup complete");

        // Auto-advance after short delay
        yield return new WaitForSeconds(autoAdvanceDelay);

        Debug.Log("[PreInvestigation] Auto-advancing to Investigation phase...");
        GameManager.Instance.SetState(GameState.Investigation);

        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }
}