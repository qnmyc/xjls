using System;
using System.Collections.Generic;

[Serializable]
public class CharacterRecord
{
    public int ID;
    public string name;
    public int hp;
    public int mp;
    public int atk;
    public int costMp;
}

[Serializable]
public class CharacterTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<CharacterRecord> data;
}

[Serializable]
public class ClueRecord
{
    public int ID;
    public string clueName;
    public string desc;
    public string getWay;
    public string comboId;
}

[Serializable]
public class ClueTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<ClueRecord> data;
}

[Serializable]
public class ClueConnectRecord
{
    public int ID;
    public int clueld1;
    public int clueld2;
    public float addProgress;
    public string successText;
    public int costMp;
}

[Serializable]
public class ClueConnectTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<ClueConnectRecord> data;
}

[Serializable]
public class DialogueRecord
{
    public int id;
    public string avatar;
    public string name;
    public string text;
    public int nextId;
    public string option;
    public string reward;
}

[Serializable]
public class DialogueTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<DialogueRecord> data;
}

[Serializable]
public class EnemyRecord
{
    public int ID;
    public string name;
    public string type;
    public int hp;
    public int atk;
    public float shieldProgress;
    public string breakAction;
    public string reward;
    public string desc;
}

[Serializable]
public class EnemyTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<EnemyRecord> data;
}

[Serializable]
public class ExploreRecord
{
    public int ID;
    public string areaName;
    public string objaName;
    public int needSkill;
    public int costMp;
    public string reward;
    public string desc;
}

[Serializable]
public class ExploreTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<ExploreRecord> data;
}

[Serializable]
public class ItemRecord
{
    public int ID;
    public string name;
    public int type;
    public string effect;
    public string getWay;
    public string useLimit;
    public string desc;
}

[Serializable]
public class ItemTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<ItemRecord> data;
}

[Serializable]
public class SafehouseRecord
{
    public int ID;
    public string optNname;
    public string cost;
    public string effect;
    public string desc;
}

[Serializable]
public class SafehouseTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<SafehouseRecord> data;
}

[Serializable]
public class SkillRecord
{
    public int ID;
    public string name;
    public int type;
    public int costType;
    public int costValue;
    public string skillEffect;
    public int obj;
    public int effectType;
    public string desc;
}

[Serializable]
public class SkillTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<SkillRecord> data;
}

[Serializable]
public class YinYangEyeCostTableRecord
{
    public int ID;
    public string stateId;
    public string costRate;
    public string desc;
}

[Serializable]
public class YinYangEyeCostTableTable
{
    public string tableName;
    public string sourceFile;
    public string sourceRelativePath;
    public string sheetName;
    public int rowCount;
    public List<YinYangEyeCostTableRecord> data;
}
