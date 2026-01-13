using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections;
using UnityEngine.InputSystem;

public class LoginScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private GameObject leaderboardPanel;

    [Header("Validation")]
    [SerializeField] private int minUsernameLength = 3;
    [SerializeField] private int maxUsernameLength = 20;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.3f);

    private bool isLoggingIn = false;

    void Start()
    {
        loginButton?.onClick.AddListener(OnLoginClick);
        leaderboardButton?.onClick.AddListener(OnLeaderboardButtonClicked);
        usernameInput?.onValueChanged.AddListener(OnUsernameChanged);
        usernameInput?.onEndEdit.AddListener(OnUsernameEndEdit);

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        loginButton.interactable = false;

        // Wait for Firebase to initialize
        StartCoroutine(WaitForFirebaseInit());
    }

    private void OnLeaderboardButtonClicked()
    {
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(!leaderboardPanel.activeSelf);

            if (leaderboardPanel.activeSelf)
            {
                LeaderboardManager.Instance.LoadLeaderboard();
            }
        }
    }

    IEnumerator WaitForFirebaseInit()
    {
        // Wait for Firebase
        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsInitialized)
        {
            yield return null;
        }

        Debug.Log("[LoginScreen] Firebase ready!");

        // Focus on input field after init
        yield return new WaitForSeconds(0.1f);
        usernameInput.Select();
        usernameInput.ActivateInputField();
    }

    void OnEnable()
    {
        // Clear when screen appears
        if (usernameInput != null)
        {
            usernameInput.text = "";
            ResetInputFieldColor();
        }

        if (errorText != null)
            errorText.gameObject.SetActive(false);

        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        isLoggingIn = false;

        // Focus on input field
        StartCoroutine(FocusInputFieldDelayed());
    }

    IEnumerator FocusInputFieldDelayed()
    {
        yield return null;

        if (usernameInput != null)
        {
            usernameInput.Select();
            usernameInput.ActivateInputField();
        }
    }

    void OnUsernameChanged(string value)
    {
        if (errorText != null)
            errorText.gameObject.SetActive(false);

        ResetInputFieldColor();

        // Update button state
        UpdateLoginButtonState();
    }

    void OnUsernameEndEdit(string value)
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                OnLoginClick();
            }
        }
    }

    void OnLoginClick()
    {
        if (isLoggingIn) return;

        string username = usernameInput.text.Trim();

        if (!ValidateUsername(username, out string errorMessage))
        {
            ShowError(errorMessage);
            return;
        }

        // Start login process
        StartLogin(username);
    }

    bool ValidateUsername(string username, out string errorMessage)
    {
        errorMessage = "";

        if (string.IsNullOrEmpty(username))
        {
            errorMessage = "Please enter a username";
            return false;
        }

        if (username.Length < minUsernameLength)
        {
            errorMessage = $"Username must be at least {minUsernameLength} characters";
            return false;
        }

        if (username.Length > maxUsernameLength)
        {
            errorMessage = $"Username must be less than {maxUsernameLength} characters";
            return false;
        }

        // Check valid characters (alphanumeric + underscore)
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            errorMessage = "Username can only contain letters, numbers, and underscores";
            return false;
        }

        return true;
    }

    void StartLogin(string username)
    {
        isLoggingIn = true;
        SetInteractable(false);

        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);

        // Start Firebase authentication
        FirebaseManager.Instance.LoginWithUsername(username, OnLoginSuccess, OnLoginFailed);
    }

    void OnLoginSuccess(string username, string userId)
    {
        Debug.Log($"[LoginScreen] Login successful: {username} ({userId})");

        // Show success feedback
        ShowSuccess("Login successful!");

        // Wait a moment for visual feedback
        StartCoroutine(TransitionToNextState(username, userId));
    }

    void OnLoginFailed(string error)
    {
        Debug.LogError($"[LoginScreen] Login failed: {error}");

        // Hide loading
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        ShowError("Login failed. Please try again.");

        // Re-enable interaction
        isLoggingIn = false;
        SetInteractable(true);
    }

    IEnumerator TransitionToNextState(string username, string userId)
    {
        // Wait a moment for success message
        yield return new WaitForSeconds(0.5f);

        // Hide loading
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        isLoggingIn = false;

        // Notify GameManager
        GameManager.Instance.OnLoginComplete(username, userId);
    }

    void UpdateLoginButtonState()
    {
        if (loginButton == null) return;

        string username = usernameInput.text.Trim();
        bool isValid = !string.IsNullOrEmpty(username) &&
                       username.Length >= minUsernameLength &&
                       username.Length <= maxUsernameLength;

        loginButton.interactable = isValid && !isLoggingIn;
    }

    void SetInteractable(bool interactable)
    {
        if (usernameInput != null)
            usernameInput.interactable = interactable;

        if (loginButton != null)
            loginButton.interactable = interactable;
    }

    void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = errorColor;
            errorText.gameObject.SetActive(true);
        }

        // Visual feedback on input field
        if (usernameInput != null && usernameInput.image != null)
        {
            usernameInput.image.color = errorColor;
        }
    }

    void ShowSuccess(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = successColor;
            errorText.gameObject.SetActive(true);
        }

        // Visual feedback on input field
        if (usernameInput != null && usernameInput.image != null)
        {
            usernameInput.image.color = successColor;
        }
    }

    void ResetInputFieldColor()
    {
        if (usernameInput != null && usernameInput.image != null)
        {
            usernameInput.image.color = normalColor;
        }
    }
}