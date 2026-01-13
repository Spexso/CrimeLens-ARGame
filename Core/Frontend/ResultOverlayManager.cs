using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;


public class ResultOverlayManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button playAgainButton;

    [Header("Result Messages")]
    [SerializeField] private string victoryHeader = "Case Solved!";
    [SerializeField] private string defeatHeader = "Case Unsolved";
    [SerializeField] private Color victoryColor = Color.green;
    [SerializeField] private Color defeatColor = Color.red;

    private void Awake()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }
    }

    private void OnDestroy()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
        }
    }

    private void OnPlayAgainClicked()
    {
        // Transition back to Investigation screen (scanning phase)
        GameManager.Instance.PlayAgain();
    }

    public void UpdateResults()
    {
        if (GameManager.Instance?.IsSolved == true)
        {
            // Victory
            headerText.text = victoryHeader;
            headerText.color = victoryColor;

            string murderWeapon = GameManager.Instance.CurrentMystery?.MurderWeapon ?? "Unknown";
            string victim = GameManager.Instance.CurrentMystery?.Victim ?? "Unknown";

            resultText.text = BuildResultMessage(true, murderWeapon, victim);
        }
        else
        {
            // Loss
            headerText.text = defeatHeader;
            headerText.color = defeatColor;

            string murderWeapon = GameManager.Instance.CurrentMystery?.MurderWeapon ?? "Unknown";
            string victim = GameManager.Instance.CurrentMystery?.Victim ?? "Unknown";

            resultText.text = BuildResultMessage(false, murderWeapon, victim);
        }
    }

    private string BuildResultMessage(bool isVictory, string murderWeapon, string victim)
    {
        if (isVictory)
        {
            return $"Excellent work, detective!\n\n" +
                   $"You correctly identified:\n" +
                   $"Murder Weapon: {murderWeapon}\n" +
                   $"Victim: {victim}\n\n" +
                   $"Justice has been served.";
        }
        else
        {
            return $"The case remains unsolved.\n\n" +
                   $"The correct answer was:\n" +
                   $"Murder Weapon: {murderWeapon}\n" +
                   $"Victim: {victim}\n\n" +
                   $"Better luck next time, detective.";
        }
    }
}
