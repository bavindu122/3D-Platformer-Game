using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public int NumberOfGems
    {
        get; private set;
    }

    public void GemColledted()
    {
        NumberOfGems++;
    }
}
