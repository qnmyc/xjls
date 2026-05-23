using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

//键盘交互对象组件，挂在场景中需要交互的物体上，配合InteractionManager实现交互逻辑
public enum InteractType 
{
    Dialogue,       // 对话
    Investigate,    // 调查
    PickUp,         // 拾取
    Mechanism,      // 机关门梯子
    Toggle          // 切换类（阴阳眼开关）
}

public enum TriggerType
{
    Auto,           // 自动触发
    KeyPress,       // 按键触发
    KeyToggle       // 按键切换
}

public enum InteractPriority//优先级
{
    Plot = 0,       // 剧情强制
    Combat = 1,     // 战斗
    Normal = 2      // 普通探索
}

public class InteractableObject : MonoBehaviour
{
    [Header("基础配置")]
    public int interactableID; //表格中的ID           
    public InteractType interactableType;
    public TriggerType triggerType;
    public InteractPriority priority = InteractPriority.Normal;//见上

    [Header("按键与提示")]
    public KeyCode keyCode = KeyCode.E;       // 没给，我先用E键了
    public string promptText;                 // 提示文字，如"对话"、"拾取"

    [Header("长按设置")]
    public bool requiresHold = false;
    public float holdDuration = 0.8f;         // 长按所需时长

    [Header("交互半径")]
    public float interactRadius = 2f;

    [Header("回调")]
    public UnityEvent onInteract;             // 成功触发时执行的动作

    [HideInInspector] public bool isOnCooldown = false;
}
