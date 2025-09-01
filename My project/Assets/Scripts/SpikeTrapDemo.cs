using System.Collections;
using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Animator spikeTrapAnim;
    [SerializeField] Collider damageCollider;

    [Header("Timings")]
    public float openDuration = 2f;
    public float closeDuration = 2f;

    private bool isActive = false;

    void Awake()
    {
        if (spikeTrapAnim == null)
            spikeTrapAnim = GetComponent<Animator>();

        if (damageCollider == null)
            damageCollider = GetComponentInChildren<Collider>();

        if (damageCollider != null)
            damageCollider.enabled = false; // off at start

        StartCoroutine(OpenCloseTrap());
    }

    IEnumerator OpenCloseTrap()
    {
        // ---- OPEN ----
        spikeTrapAnim.SetTrigger("open");
        yield return new WaitForSeconds(0.4f); // wait until spikes visually up
        SetActive(true);

        yield return new WaitForSeconds(openDuration);

        // ---- CLOSE ----
        spikeTrapAnim.SetTrigger("close");
        yield return new WaitForSeconds(0.3f); // let animation start closing
        SetActive(false);

        yield return new WaitForSeconds(closeDuration);

        StartCoroutine(OpenCloseTrap());
    }

    void SetActive(bool value)
    {
        isActive = value;
        if (damageCollider != null) damageCollider.enabled = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        PlayerAnimation player = other.GetComponent<PlayerAnimation>();
        if (player != null && !player.IsDead())
        {
            player.Die(true);
            Debug.Log("☠️ Player died on spike trap");
        }
    }
}
