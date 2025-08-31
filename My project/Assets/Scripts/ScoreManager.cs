using UnityEngine;
using UnityEngine.UI;
public class ScoreManager : MonoBehaviour
{
    public Text scoreText;
    private int score;
    void Start()
    {
        score = 0;
        UpdateScoreText();
    }
    public void AddScore(int value)
    {
        score += value;
        UpdateScoreText();
    }
    void UpdateScoreText()
    {
        scoreText.text = "Score: " + score;
    }
}