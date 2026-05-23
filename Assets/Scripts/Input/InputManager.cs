using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    // ========== 交互按键 E ==========
    public event Action OnInteractDown;
    public event Action OnInteractHeld;
    public event Action OnInteractUp;

    // ========== 取消 / 返回 ==========
    public event Action OnCancelPressed;        // Esc

    // ========== 跳过对话 / 动画 ==========
    public event Action OnSkipPressed;          // Space

    // ========== 菜单 / 背包 ==========
    public event Action OnInventoryKeyPressed;  // I 键

    // ========== Tab 键（技能轮盘 / 背包共用） ==========
    public event Action OnTabDown;
    public event Action OnTabHeld;
    public event Action OnTabUp;

    // ========== 存档快捷键 ==========
    public event Action OnSavePressed;          // F3

    private bool isInteractKeyDown = false;
    private bool isTabKeyDown = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 交互键 E
        if (Input.GetKeyDown(KeyCode.E))
        {
            isInteractKeyDown = true;
            OnInteractDown?.Invoke();
        }
        if (Input.GetKey(KeyCode.E) && isInteractKeyDown)
        {
            OnInteractHeld?.Invoke();
        }
        if (Input.GetKeyUp(KeyCode.E))
        {
            if (isInteractKeyDown)
            {
                OnInteractUp?.Invoke();
                isInteractKeyDown = false;
            }
        }

        // Tab 键
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isTabKeyDown = true;
            OnTabDown?.Invoke();
        }
        if (Input.GetKey(KeyCode.Tab) && isTabKeyDown)
        {
            OnTabHeld?.Invoke();
        }
        if (Input.GetKeyUp(KeyCode.Tab))
        {
            if (isTabKeyDown)
            {
                OnTabUp?.Invoke();
                isTabKeyDown = false;
            }
        }

        // I 键
        if (Input.GetKeyDown(KeyCode.I))
        {
            OnInventoryKeyPressed?.Invoke();
        }

        // Esc 键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelPressed?.Invoke();
        }

        // Space 键
        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnSkipPressed?.Invoke();
        }

        // F3 键
        if (Input.GetKeyDown(KeyCode.F3))
        {
            OnSavePressed?.Invoke();
        }
    }
}