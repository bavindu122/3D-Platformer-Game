using UnityEngine;
public class Collectible : MonoBehaviour
{   
    void OnTriggerEnter(Collider other)
    {
        PlayerInventory playerInventory = other.GetComponent<PlayerInventory>();
        
        if(playerInventory != null )
        {
            playerInventory.GemColledted();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
