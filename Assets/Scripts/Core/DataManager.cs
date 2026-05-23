using System.Collections.Generic;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    // 私有存储表
    private CharacterTable characterTable;
    private SkillTable skillTable;
    private EnemyTable enemyTable;
    private ClueConnectTable clueConnectTable;
    private ClueTable clueTable;
    private ItemTable itemTable;
    private ExploreTable exploreTable;
    private SafehouseTable safehouseTable;
    private YinYangEyeCostTableTable yinYangEyeCostTable;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAllTables();
    }

    void LoadAllTables()
    {
        characterTable = LoadData.LoadJSONData<CharacterTable>("Charater");
        skillTable = LoadData.LoadJSONData<SkillTable>("Skill");
        enemyTable = LoadData.LoadJSONData<EnemyTable>("Enemy");
        clueConnectTable = LoadData.LoadJSONData<ClueConnectTable>("ClueConnect");
        clueTable = LoadData.LoadJSONData<ClueTable>("Clue");
        itemTable = LoadData.LoadJSONData<ItemTable>("Item");
        exploreTable = LoadData.LoadJSONData<ExploreTable>("Explore");
        safehouseTable = LoadData.LoadJSONData<SafehouseTable>("Safehouse");
        yinYangEyeCostTable = LoadData.LoadJSONData<YinYangEyeCostTableTable>("YinYangEyeCostTable");

        // 校验
        if (characterTable == null) Debug.LogError("DataManager: CharaterTable 加载失败");
        if (skillTable == null) Debug.LogError("DataManager: SkillTable 加载失败");
        if (enemyTable == null) Debug.LogError("DataManager: EnemyTable 加载失败");
        if (clueConnectTable == null) Debug.LogError("DataManager: ClueConnectTable 加载失败");
        if (clueTable == null) Debug.LogError("DataManager: ClueTable 加载失败");
        if (itemTable == null) Debug.LogError("DataManager: ItemTable 加载失败");
        if (exploreTable == null) Debug.LogError("DataManager: ExploreTable 加载失败");
        if (safehouseTable == null) Debug.LogError("DataManager: SafehouseTable 加载失败");
        if (yinYangEyeCostTable == null) Debug.LogError("DataManager: YinYangEyeCostTable 加载失败");
    }

    // ==================== 单条查询 ====================

    public CharacterRecord GetCharacterByID(int id)
    {
        return characterTable?.data?.Find(c => c.ID == id);
    }

    public SkillRecord GetSkillByID(int id)
    {
        return skillTable?.data?.Find(s => s.ID == id);
    }

    public EnemyRecord GetEnemyByID(int id)
    {
        return enemyTable?.data?.Find(e => e.ID == id);
    }

    public ClueConnectRecord GetClueConnectByID(int id)
    {
        return clueConnectTable?.data?.Find(cc => cc.ID == id);
    }

    public ClueRecord GetClueByID(int id)
    {
        return clueTable?.data?.Find(c => c.ID == id);
    }

    public ItemRecord GetItemByID(int id)
    {
        return itemTable?.data?.Find(i => i.ID == id);
    }

    public ExploreRecord GetExploreByID(int id)
    {
        return exploreTable?.data?.Find(e => e.ID == id);
    }

    public SafehouseRecord GetSafehouseOptionByID(int id)
    {
        return safehouseTable?.data?.Find(s => s.ID == id);
    }

    public YinYangEyeCostTableRecord GetYinYangEyeCostByID(int id)
    {
        return yinYangEyeCostTable?.data?.Find(y => y.ID == id);
    }

    // ==================== 全表获取（给 UI 列表用） ====================

    public List<ItemRecord> GetAllItems()
    {
        return itemTable?.data ?? new List<ItemRecord>();
    }

    public List<ClueRecord> GetAllClues()
    {
        return clueTable?.data ?? new List<ClueRecord>();
    }

    public List<SkillRecord> GetAllSkills()
    {
        return skillTable?.data ?? new List<SkillRecord>();
    }

    public List<CharacterRecord> GetAllCharacters()
    {
        return characterTable?.data ?? new List<CharacterRecord>();
    }

    public List<EnemyRecord> GetAllEnemies()
    {
        return enemyTable?.data ?? new List<EnemyRecord>();
    }

    public List<ExploreRecord> GetAllExplores()
    {
        return exploreTable?.data ?? new List<ExploreRecord>();
    }

    public List<SafehouseRecord> GetAllSafehouseOptions()
    {
        return safehouseTable?.data ?? new List<SafehouseRecord>();
    }
}