using UnityEngine;
public class Collectible : MonoBehaviour
{
    public int scoreValue = 10;
    
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // Find the ScoreManager and add score
            ScoreManager scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager != null)
            {
                scoreManager.AddScore(scoreValue);
            }
            
            Destroy(gameObject);
        }
    }
}
