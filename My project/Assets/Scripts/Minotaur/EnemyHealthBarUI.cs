using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour
{
    [SerializeField] MinotaurHealth health;
    [SerializeField] Slider slider;
    [SerializeField] Transform billboard;  // the canvas root
    Camera cam;

    void Awake()
    {
        cam = Camera.main;
        if (health != null && slider != null)
        {
            slider.minValue = 0;
            slider.maxValue = health != null ? health.GetType()
                .GetField("maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(health) as int? ?? 100 : 100;
            slider.value = health.Current;
        }
    }

    void LateUpdate()
    {
        if (billboard && cam)
        {
            var toCam = billboard.position - cam.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
                billboard.forward = toCam.normalized;
        }
        if (health && slider) slider.value = health.Current;
    }
}
