using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    private List<int> itemIds = new List<int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
     
    }

   

    public void AddItem(int itemID)
    {
        itemIds.Add(itemID);
        Debug.Log($"InventoryManager: Get{itemID}");
    }

    public bool HasItem(int itemID)
    {
        return itemIds.Contains(itemID);
    }
    public List<int> GetAllItems()
    {
        return new List<int>(itemIds);
    }

}
