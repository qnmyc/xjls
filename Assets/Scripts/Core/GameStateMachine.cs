using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStateMachine : MonoBehaviour
{
    public enum GameState { Explore, Dialogue, Inventory, Paused }

    
    public GameState CurrentState { get; private set; } = GameState.Explore;

    
    public event Action<GameState> OnStateChanged;

    public bool CanPlayerMove()
    {
        return CurrentState == GameState.Explore;
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        GameState oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"ChangeState: {oldState} ¡ú {newState}");
        OnStateChanged?.Invoke(newState);  
    }

    public bool IsInState(GameState state)
    {
        return CurrentState == state;
    }
}
