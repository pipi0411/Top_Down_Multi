using TMPro;
using UnityEngine;

public class CoinCounterUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI coinText;
    [SerializeField] int minimumDigits = 3;

    void Awake()
    {
        if (coinText == null)
            coinText = GetComponentInChildren<TextMeshProUGUI>(true);

        UpdateCoinText(PickupItem.Coins);
    }

    void OnEnable()
    {
        PickupItem.OnCoinsChanged += UpdateCoinText;
        UpdateCoinText(PickupItem.Coins);
    }

    void OnDisable()
    {
        PickupItem.OnCoinsChanged -= UpdateCoinText;
    }

    void UpdateCoinText(int coins)
    {
        if (coinText == null) return;
        int safeDigits = Mathf.Max(1, minimumDigits);
        coinText.text = Mathf.Max(0, coins).ToString($"D{safeDigits}");
    }
}
