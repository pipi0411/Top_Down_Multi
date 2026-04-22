using TMPro;
using UnityEngine;

public class JoinCodeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private string prefix = " ";

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (targetText == null)
        {
            return;
        }

        string code = NetworkButtons.LatestJoinCode;
        targetText.text = string.IsNullOrEmpty(code) ? string.Empty : prefix + code;
    }
}
