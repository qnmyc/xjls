using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum DialogueRewardType
{
    None = 0,
    Inventory = 1,
    Clue = 2,
    WorldKnowledge = 3,
    Consumable = 4,
}

public class DialogueManager : MonoBehaviour
{
    private const string DefaultDialogueCanvasResourcePath = "Prefabs/DialogueCanvas";
    private const string DialogueReadStateFileName = "dialogue_read_state.json";

    public static DialogueManager Instance { get; private set; }

    public string CurrentDialogueTableName => dialogueTableName;

    [SerializeField] private GameObject dialogueCanvasRoot;
    [SerializeField] private GameObject dialogueCanvasPrefab;
    [SerializeField] private string dialogueTableName = "Chapter1";
    [SerializeField] private bool persistReadState = true;

    private DialogueCanvasController activeCanvasController;
    private GameObject activeCanvasInstance;
    private DialogueTable activeTable;
    private DialogueRecord currentRecord;
    private DialogueReadStateData readStateData = new DialogueReadStateData();
    private readonly Dictionary<string, HashSet<int>> readStateByTable = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

    private static string DialogueReadStateFilePath => Path.Combine(Application.persistentDataPath, DialogueReadStateFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadReadState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StartDialogue(int dialogueID)
    {
        Debug.Log($"DialogueManager: Dialogue Start {dialogueID}");
        GameManager.Instance?.SetState(GameStateMachine.GameState.Dialogue);
        ShowDialogueCanvas(dialogueID);
    }

    public bool LoadDialogueTable(string sheetName)
    {
        string normalizedSheetName = NormalizeTableName(sheetName);
        if (string.IsNullOrEmpty(normalizedSheetName))
        {
            Debug.LogError("DialogueManager.LoadDialogueTable failed: sheetName is null or empty.", this);
            return false;
        }

        UnloadDialogueTable();

        DialogueTable loadedTable = LoadData.LoadJSONData<DialogueTable>(normalizedSheetName);
        if (loadedTable == null)
        {
            Debug.LogError($"DialogueManager.LoadDialogueTable failed: unable to load dialogue table '{normalizedSheetName}'.", this);
            return false;
        }

        dialogueTableName = normalizedSheetName;
        activeTable = loadedTable;
        EnsureReadStateTable(normalizedSheetName);
        return true;
    }

    public void UnloadDialogueTable()
    {
        currentRecord = null;
        activeTable = null;
    }

    public bool IsCurrentDialogueRead(int dialogueId)
    {
        return IsDialogueRead(dialogueTableName, dialogueId);
    }

    public bool IsDialogueRead(string sheetName, int dialogueId)
    {
        string normalizedSheetName = NormalizeTableName(sheetName);
        if (string.IsNullOrEmpty(normalizedSheetName) || dialogueId <= 0)
        {
            return false;
        }

        return readStateByTable.TryGetValue(normalizedSheetName, out HashSet<int> readIds) && readIds.Contains(dialogueId);
    }

    public void EndDialogue()
    {
        Debug.Log("DialogueManager: Dialogue End");
        HideDialogueCanvas();
    }

    public void ShowDialogueCanvas(int dialogueId)
    {
        LoadDialogueCanvas();
        ShowRecord(dialogueId);
    }

    public void HideDialogueCanvas()
    {
        UnloadDialogueCanvas();
        GameManager.Instance?.SetState(GameStateMachine.GameState.Explore);
    }

    public void ContinueDialogue()
    {
        if (currentRecord == null || currentRecord.nextId <= 0)
        {
            EndDialogue();
            return;
        }

        ShowRecord(currentRecord.nextId);
    }

    public void SelectOption(int nextId)
    {
        if (nextId <= 0)
        {
            EndDialogue();
            return;
        }

        ShowRecord(nextId);
    }

    private void LoadDialogueCanvas()
    {
        GameObject canvasInstance = ResolveDialogueCanvasInstance();
        if (canvasInstance == null)
        {
            Debug.LogError("DialogueManager could not find or create the dialogue canvas instance.", this);
            return;
        }

        if (dialogueCanvasRoot != null && !dialogueCanvasRoot.activeSelf)
        {
            dialogueCanvasRoot.SetActive(true);
        }

        if (!canvasInstance.activeSelf)
        {
            canvasInstance.SetActive(true);
        }

        activeCanvasController = GetOrCreateCanvasController(canvasInstance);
        activeCanvasInstance = canvasInstance;
        activeCanvasController.Initialize(this);
    }

    private void UnloadDialogueCanvas()
    {
        GameObject canvasInstance = activeCanvasInstance != null ? activeCanvasInstance : FindDialogueCanvasInstance();
        if (canvasInstance == null)
        {
            currentRecord = null;
            return;
        }

        if (activeCanvasController != null && activeCanvasController.gameObject == canvasInstance)
        {
            activeCanvasController = null;
        }

        currentRecord = null;
        activeCanvasInstance = null;
        canvasInstance.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(canvasInstance);
        }
        else
        {
            DestroyImmediate(canvasInstance);
        }
    }

    private void ShowRecord(int dialogueId)
    {
        DialogueRecord record = FindRecord(dialogueId);
        if (record == null)
        {
            Debug.LogWarning($"DialogueManager could not find dialogue id {dialogueId} in table '{dialogueTableName}'.", this);
            EndDialogue();
            return;
        }

        currentRecord = record;
        bool newlyRead = MarkDialogueRead(dialogueTableName, record.id);
        if (newlyRead)
        {
            ApplyDialogueReward(record.reward);
        }

        activeCanvasController?.ShowDialogue(record);
    }

    private DialogueRecord FindRecord(int dialogueId)
    {
        DialogueTable table = GetDialogueTable();
        if (table == null || table.data == null)
        {
            return null;
        }

        foreach (DialogueRecord record in table.data)
        {
            if (record != null && record.id == dialogueId)
            {
                return record;
            }
        }

        return null;
    }

    private DialogueTable GetDialogueTable()
    {
        if (activeTable != null && string.Equals(activeTable.tableName, dialogueTableName, StringComparison.OrdinalIgnoreCase))
        {
            return activeTable;
        }

        return LoadDialogueTable(dialogueTableName) ? activeTable : null;
    }

    private bool MarkDialogueRead(string sheetName, int dialogueId)
    {
        string normalizedSheetName = NormalizeTableName(sheetName);
        if (string.IsNullOrEmpty(normalizedSheetName) || dialogueId <= 0)
        {
            return false;
        }

        HashSet<int> readIds = EnsureReadStateTable(normalizedSheetName);
        if (!readIds.Add(dialogueId))
        {
            return false;
        }

        SyncReadStateData();
        SaveReadState();
        return true;
    }

    private void ApplyDialogueReward(string rawReward)
    {
        if (!TryParseDialogueReward(rawReward, out DialogueReward reward))
        {
            return;
        }

        switch (reward.Type)
        {
            case DialogueRewardType.Inventory:
                // TODO: Call the inventory reward manager with reward.TargetId.
                Debug.Log($"TODO Dialogue reward: add inventory item '{reward.TargetId}'.", this);
                break;
            case DialogueRewardType.Clue:
                // TODO: Call the clue reward manager with reward.TargetId.
                Debug.Log($"TODO Dialogue reward: unlock clue '{reward.TargetId}'.", this);
                break;
            case DialogueRewardType.WorldKnowledge:
                // TODO: Call the world-knowledge reward manager with reward.TargetId.
                Debug.Log($"TODO Dialogue reward: unlock world knowledge '{reward.TargetId}'.", this);
                break;
            case DialogueRewardType.Consumable:
                // TODO: Call the consumable reward manager with reward.TargetId.
                Debug.Log($"TODO Dialogue reward: add consumable '{reward.TargetId}'.", this);
                break;
            default:
                Debug.LogWarning($"DialogueManager ignored unsupported reward type '{reward.Type}' from '{reward.RawValue}'.", this);
                break;
        }
    }

    private bool TryParseDialogueReward(string rawReward, out DialogueReward reward)
    {
        reward = default(DialogueReward);
        if (string.IsNullOrWhiteSpace(rawReward))
        {
            return false;
        }

        string trimmedReward = rawReward.Trim();
        int separatorIndex = trimmedReward.IndexOf(':');
        if (separatorIndex < 0)
        {
            separatorIndex = trimmedReward.IndexOf('：');
        }

        if (separatorIndex <= 0 || separatorIndex >= trimmedReward.Length - 1)
        {
            Debug.LogWarning($"DialogueManager failed to parse reward '{rawReward}'. Expected format 'type:id'.", this);
            return false;
        }

        string rawType = trimmedReward.Substring(0, separatorIndex).Trim();
        string targetId = trimmedReward.Substring(separatorIndex + 1).Trim();
        if (!int.TryParse(rawType, out int typeCode))
        {
            Debug.LogWarning($"DialogueManager failed to parse reward type '{rawType}' from '{rawReward}'.", this);
            return false;
        }

        DialogueRewardType rewardType = (DialogueRewardType)typeCode;
        if (!Enum.IsDefined(typeof(DialogueRewardType), rewardType) || rewardType == DialogueRewardType.None)
        {
            Debug.LogWarning($"DialogueManager failed to parse reward '{rawReward}': unknown reward type '{typeCode}'.", this);
            return false;
        }

        if (string.IsNullOrEmpty(targetId))
        {
            Debug.LogWarning($"DialogueManager failed to parse reward '{rawReward}': target id is empty.", this);
            return false;
        }

        reward = new DialogueReward(rewardType, targetId, trimmedReward);
        return true;
    }

    private HashSet<int> EnsureReadStateTable(string sheetName)
    {
        if (!readStateByTable.TryGetValue(sheetName, out HashSet<int> readIds))
        {
            readIds = new HashSet<int>();
            readStateByTable[sheetName] = readIds;
        }

        return readIds;
    }

    private void LoadReadState()
    {
        readStateData = new DialogueReadStateData();
        readStateByTable.Clear();

        if (!persistReadState || !File.Exists(DialogueReadStateFilePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(DialogueReadStateFilePath);
            DialogueReadStateData loadedData = JsonUtility.FromJson<DialogueReadStateData>(json);
            if (loadedData != null && loadedData.tables != null)
            {
                readStateData = loadedData;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"DialogueManager failed to load read state: {exception.Message}", this);
        }

        BuildReadStateLookup();
    }

    private void SaveReadState()
    {
        if (!persistReadState)
        {
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(readStateData, true);
            File.WriteAllText(DialogueReadStateFilePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"DialogueManager failed to save read state: {exception.Message}", this);
        }
    }

    private void BuildReadStateLookup()
    {
        readStateByTable.Clear();

        if (readStateData.tables == null)
        {
            readStateData.tables = new List<DialogueReadTableState>();
            return;
        }

        foreach (DialogueReadTableState tableState in readStateData.tables)
        {
            string tableName = NormalizeTableName(tableState != null ? tableState.tableName : null);
            if (string.IsNullOrEmpty(tableName))
            {
                continue;
            }

            HashSet<int> readIds = EnsureReadStateTable(tableName);
            if (tableState.readDialogueIds == null)
            {
                continue;
            }

            foreach (int dialogueId in tableState.readDialogueIds)
            {
                if (dialogueId > 0)
                {
                    readIds.Add(dialogueId);
                }
            }
        }
    }

    private void SyncReadStateData()
    {
        readStateData.tables.Clear();

        foreach (KeyValuePair<string, HashSet<int>> pair in readStateByTable)
        {
            DialogueReadTableState tableState = new DialogueReadTableState
            {
                tableName = pair.Key,
                readDialogueIds = new List<int>(pair.Value)
            };
            tableState.readDialogueIds.Sort();
            readStateData.tables.Add(tableState);
        }
    }

    private static string NormalizeTableName(string tableName)
    {
        return string.IsNullOrWhiteSpace(tableName) ? string.Empty : tableName.Trim();
    }

    private GameObject ResolveDialogueCanvasInstance()
    {
        Transform root = GetDialogueCanvasRoot();
        GameObject existingInstance = root != null ? FindDialogueCanvasInstance(root) : FindDialogueCanvasInstance();
        if (existingInstance != null)
        {
            return existingInstance;
        }

        GameObject prefab = dialogueCanvasPrefab != null ? dialogueCanvasPrefab : Resources.Load<GameObject>(DefaultDialogueCanvasResourcePath);
        if (prefab == null)
        {
            return null;
        }

        GameObject canvasInstance = root != null ? Instantiate(prefab, root) : Instantiate(prefab);
        canvasInstance.name = prefab.name;
        return canvasInstance;
    }

    private Transform GetDialogueCanvasRoot()
    {
        return dialogueCanvasRoot != null ? dialogueCanvasRoot.transform : null;
    }

    private GameObject FindDialogueCanvasInstance()
    {
        Transform root = GetDialogueCanvasRoot();
        return root != null ? FindDialogueCanvasInstance(root) : FindSceneDialogueCanvasInstance();
    }

    private static GameObject FindDialogueCanvasInstance(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.GetComponent<DialogueCanvasController>() != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindSceneDialogueCanvasInstance()
    {
        DialogueCanvasController[] controllers = Resources.FindObjectsOfTypeAll<DialogueCanvasController>();
        foreach (DialogueCanvasController controller in controllers)
        {
            if (controller != null && controller.gameObject.scene.IsValid())
            {
                return controller.gameObject;
            }
        }

        return null;
    }

    private static DialogueCanvasController GetOrCreateCanvasController(GameObject canvasInstance)
    {
        DialogueCanvasController controller = canvasInstance.GetComponent<DialogueCanvasController>();
        if (controller == null)
        {
            controller = canvasInstance.AddComponent<DialogueCanvasController>();
        }

        return controller;
    }

    [Serializable]
    private sealed class DialogueReadStateData
    {
        public List<DialogueReadTableState> tables = new List<DialogueReadTableState>();
    }

    [Serializable]
    private sealed class DialogueReadTableState
    {
        public string tableName;
        public List<int> readDialogueIds = new List<int>();
    }

    private readonly struct DialogueReward
    {
        public DialogueReward(DialogueRewardType type, string targetId, string rawValue)
        {
            Type = type;
            TargetId = targetId;
            RawValue = rawValue;
        }

        public DialogueRewardType Type { get; }
        public string TargetId { get; }
        public string RawValue { get; }
    }
}
