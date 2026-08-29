using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingsButtonRelay : MonoBehaviour
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        button.onClick.RemoveListener(OpenSettings);
        button.onClick.AddListener(OpenSettings);
    }

    void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OpenSettings);
    }

    void OpenSettings()
    {
        SettingsPopupUI.Show();
    }
}
