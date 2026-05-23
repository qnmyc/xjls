using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }
    [Header("Core Systems")]
    public GameStateMachine StateMachine;

    public void SetState(GameStateMachine.GameState newState)
    {
        if (StateMachine != null)
        {
            StateMachine.ChangeState(newState);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        StateMachine = GetComponent<GameStateMachine>();

        if (StateMachine == null)
        {
            Debug.LogError("GameStateMachine is not assigned in GameManager!");
        }
    }

    public GameStateMachine.GameState GetState()
    {
        if (StateMachine != null)
            return StateMachine.CurrentState;

        return GameStateMachine.GameState.Explore;
    }

}
