using UnityEngine;
//UI提示数据结构，供InteractionManager传递给InteractionHUD显示
[System.Serializable]
public struct UIPromptData 
{
    public bool isVisible;
    public Sprite keyIcon;   // 按键UI      
    public string keyText;
    public string promptText;
    public bool showProgress;  //是否显示长按进度条     
    public float progressValue;
}
