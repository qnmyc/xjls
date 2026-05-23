using System;
using System.Collections.Generic;

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
