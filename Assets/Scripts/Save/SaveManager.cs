using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
       
    }

    public void SaveGame()
    {
        Debug.Log("SaveManager: Game Saved");
    }

    public void LoadGame()
    {
        Debug.Log("SaveManager: Game Loaded");
    }
}
