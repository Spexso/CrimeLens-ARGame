using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Screens")]
    [SerializeField] public GameObject loginScreen;
    [SerializeField] private GameObject preInvestigationScreen;
    [SerializeField] public GameObject investigationScreen;
    [SerializeField] public GameObject resultsScreen;

    void Awake()
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

    public void ShowScreen(GameState state)
    {
        // Hide all screens
        if (loginScreen != null)
            loginScreen.SetActive(false);

        if (preInvestigationScreen != null)
            preInvestigationScreen.SetActive(false);

        if (investigationScreen != null)
            investigationScreen.SetActive(false);

        if (resultsScreen != null)
            resultsScreen.SetActive(false);

        // Show the right one
        switch (state)
        {
            case GameState.Login:
                if (loginScreen != null)
                {
                    loginScreen.SetActive(true);
                    Debug.Log("[UIManager] Showing Login Screen");
                }
                break;

            case GameState.PreInvestigation:
                if (preInvestigationScreen != null)
                {
                    preInvestigationScreen.SetActive(true);
                    Debug.Log("[UIManager] Showing Pre-Investigation Screen");
                }
                break;

            case GameState.Investigation:
                if (investigationScreen != null)
                {
                    investigationScreen.SetActive(true);
                    Debug.Log("[UIManager] Showing Investigation Screen");
                }
                break;

            case GameState.Results:
                if (resultsScreen != null)
                {
                    resultsScreen.SetActive(true);
                    Debug.Log("[UIManager] Showing Results Screen");
                }
                break;
        }
    }
}