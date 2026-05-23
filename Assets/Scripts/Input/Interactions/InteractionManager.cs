using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;


public class InteractionManager : MonoBehaviour//键盘交互控制系统
{
    public static InteractionManager Instance { get; private set; }

    [Header("短按/长按判定")]
    public float shortPressThreshold = 0.3f;   // <=此值为短按, >此值触发长按
    public float longPressThreshold = 0.8f;    // 长按默认值

    [Header("冷却设置")]
    public float pickUpCd = 0.2f;        // 拾取冷却
    public float toggleCd = 0.5f;        // 切换冷却

    private List<InteractableObject> registeredObjects = new List<InteractableObject>();
    // 当前最高优先级对象
    [HideInInspector] public InteractableObject currentBestTarget;

    private bool isHolding = false;
    private float holdTimer = 0f;
    private InteractableObject holdTarget;
    private Dictionary<int, float> cooldowns = new Dictionary<int, float>();// 对象ID与冷却结束时间的映射
    private bool isInteractKeyDown = false;// 交互键是否按下

    public event Action<UIPromptData> OnPromptChanged;
    public event Action<float> OnHoldProgressChanged;         // 进度 0~1
    public event Action<InteractableObject> OnInteractionTriggered;
    public event Action OnInteractionInterrupted;

    private GameStateMachine stateMachine;

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

    void Start()
    {
        stateMachine = GameManager.Instance?.StateMachine;
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged += OnGameStateChanged;
        }
        if(InputManager.Instance != null)
    {
            InputManager.Instance.OnInteractDown += OnInteractKeyDown;
            InputManager.Instance.OnInteractHeld += OnInteractKeyHeld;
            InputManager.Instance.OnInteractUp += OnInteractKeyUp;
        }
    }

    void Update()
    {
        // 1. 处理冷却
        UpdateCooldowns();

        // 2. 长按进度计算
        if (isHolding && holdTarget != null)
        {
            holdTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(holdTimer / holdTarget.holdDuration);
            OnHoldProgressChanged?.Invoke(progress);

            // 达到长按阈值
            if (holdTimer >= holdTarget.holdDuration)
            {
                TriggerInteraction(holdTarget);
                StopHolding();
            }
        }
    }

    //函数

    void UpdateCooldowns()
    {
        if (cooldowns.Count == 0) return;
        List<int> keys = new List<int>(cooldowns.Keys);
        foreach (int id in keys)
        {
            cooldowns[id] -= Time.deltaTime;
            if (cooldowns[id] <= 0f)
                cooldowns.Remove(id);
        }
    }

    void StartCooldown(InteractableObject obj)
    {
        if (obj.interactableType == InteractType.PickUp)
        {
            cooldowns[obj.interactableID] = pickUpCd;
            obj.isOnCooldown = true;
            StartCoroutine(EndCooldown(obj));
        }
        else if (obj.interactableType == InteractType.Toggle)
        {
            cooldowns[obj.interactableID] = toggleCd;
            obj.isOnCooldown = true;
            StartCoroutine(EndCooldown(obj));
        }
    }
    System.Collections.IEnumerator EndCooldown(InteractableObject obj)
    {
        yield return new WaitForSeconds(cooldowns[obj.interactableID]);
        obj.isOnCooldown = false;
    }

    void OnGameStateChanged(GameStateMachine.GameState newState)
    {
        if (newState != GameStateMachine.GameState.Explore)
        {
            ForceInterrupt();// 进入非探索状态强制中断交互
        }
        else
        {
            EvaluateBestTarget(); // 回到探索状态重新按优先级评估
        }
    }

    public void ForceInterrupt()
    {
        if (isHolding)
        {
            StopHolding();
            OnHoldProgressChanged?.Invoke(0f);
            OnInteractionInterrupted?.Invoke();
        }
        // 如果当前有短按未完成，这里也可以清空状态
    }
    void StopHolding()
    {
        isHolding = false;
        holdTimer = 0f;
        holdTarget = null;
    }

    void EvaluateBestTarget()
    {
        if (stateMachine != null && stateMachine.CurrentState != GameStateMachine.GameState.Explore)
        {
            SetBestTarget(null);
            return;
        }

        InteractableObject best = null;
        float bestPriority = float.MaxValue;

        foreach (var obj in registeredObjects)
        {
            if (obj.isOnCooldown) continue;

            int typePriority = (int)obj.priority;
            float dist = Vector3.Distance(Camera.main.transform.position, obj.transform.position);
            //优先级排序：类型优先级（提到的2个类型）> 距离（同类型近的优先）
            float score = typePriority * 1000f + dist;

            if (score < bestPriority)
            {
                bestPriority = score;
                best = obj;
            }
        }

        SetBestTarget(best);
    }
    void SetBestTarget(InteractableObject target)
    {
        if (currentBestTarget == target) return;
        currentBestTarget = target;

        NotifyUI();// 通知UI更新提示
    }
    void NotifyUI()
    {
        if (currentBestTarget == null)
        {
            OnPromptChanged?.Invoke(new UIPromptData { isVisible = false });
            return;
        }

        UIPromptData data = new UIPromptData
        {
            isVisible = true,
            keyText = currentBestTarget.keyCode.ToString(),
            promptText = currentBestTarget.promptText,
            showProgress = currentBestTarget.requiresHold && isHolding,
            progressValue = isHolding ? holdTimer / currentBestTarget.holdDuration : 0f
        };
        OnPromptChanged?.Invoke(data);
    }

    void OnInteractKeyDown()// 按下交互键
    {
        if (currentBestTarget == null) return;
        if (currentBestTarget.isOnCooldown) return;

        isInteractKeyDown = true;

        // 对于切换类：短按直接处理
        if (currentBestTarget.triggerType == TriggerType.KeyToggle)
        {
            TriggerInteraction(currentBestTarget);
            StartCooldown(currentBestTarget);
            return;
        }

        // 如果需要长按，开始计时
        if (currentBestTarget.requiresHold)
        {
            isHolding = true;
            holdTimer = 0f;
            holdTarget = currentBestTarget;
        }
        // 否则等待松开判断短按
    }
    void TriggerInteraction(InteractableObject obj)
    {
        Debug.Log($"触发交互: {obj.promptText}");
        obj.onInteract?.Invoke();
        OnInteractionTriggered?.Invoke(obj);
        EvaluateBestTarget();//交互成功后重新按优先级评估
    }

    void OnInteractKeyHeld()//按住交互键时
    {
        if (!isHolding)
            holdTimer += Time.deltaTime;
    }


    void OnInteractKeyUp()//松开交互键
    {
        if (!isInteractKeyDown) return;
        isInteractKeyDown = false;

        if (isHolding)
        {
            // 松开时如果没达到长按，则视为短按
            StopHolding();
            OnHoldProgressChanged?.Invoke(0f);
        }
        else if (currentBestTarget != null && !currentBestTarget.requiresHold)
        {// 短按触发
            if (holdTimer <= shortPressThreshold) // holdTimer 其实就是按键持续时间
            {
                TriggerInteraction(currentBestTarget);
                StartCooldown(currentBestTarget);
            }
        }
        holdTimer = 0f; // 重置按住计时
    }

    void OnDestroy()
    {
        if (stateMachine != null)
            stateMachine.OnStateChanged -= OnGameStateChanged;
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnInteractDown -= OnInteractKeyDown;
            InputManager.Instance.OnInteractHeld -= OnInteractKeyHeld;
            InputManager.Instance.OnInteractUp -= OnInteractKeyUp;
        }
    }
}
