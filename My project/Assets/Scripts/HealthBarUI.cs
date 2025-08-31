using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] PlayerHealth playerHealth;
    [SerializeField] Slider slider;
    [SerializeField] Gradient colorByPercent;
    [SerializeField] Image fillImage;

    void Reset()
    {
        slider = GetComponentInChildren<Slider>();
        AutoFindPlayerHealth();
    }

    void Awake()
    {
        if (!slider) slider = GetComponentInChildren<Slider>();
        if (!playerHealth) AutoFindPlayerHealth();
    }

    void OnEnable()
    {
        if (!playerHealth || !slider) return;

        slider.minValue = 0;
        slider.maxValue = playerHealth.MaxHealth;
        slider.value = playerHealth.Current;
        ApplyFillColor(playerHealth.Current, playerHealth.MaxHealth);

        playerHealth.onHealthChanged.AddListener(OnHealthChanged);
    }

    void OnDisable()
    {
        if (playerHealth) playerHealth.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    void OnHealthChanged(int current, int max)
    {
        Debug.Log($"[UI] Health changed: {current}/{max}");
        if (!slider) return;
        if (slider.maxValue != max) slider.maxValue = max;
        slider.value = current;
        ApplyFillColor(current, max);
    }

    void ApplyFillColor(int current, int max)
    {
        if (!fillImage || colorByPercent == null) return;
        float t = max > 0 ? (float)current / max : 0f;
        fillImage.color = colorByPercent.Evaluate(t);
    }

    void AutoFindPlayerHealth()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) playerHealth = player.GetComponent<PlayerHealth>();
    }
}
