using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class LoadData
{
    private const string ExportedTablesResourcesPath = "GameData/ExportedTables";

    [Serializable]
    private class ManifestData
    {
        public List<ManifestEntry> tables;
    }

    [Serializable]
    private class ManifestEntry
    {
        public string tableName;
        public string outputRelativePath;
    }

    private static readonly Dictionary<string, object> TableCache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> manifestCache;
    private static bool manifestLoadFailed;

    /// <summary>
    /// 按表名从 Resources/GameData/ExportedTables 加载对应 JSON，并反序列化为指定的数据类型。
    /// </summary>
    /// <typeparam name="T">
    /// 导表生成的目标类型，通常为 XXXTable，例如 ItemTable。
    /// </typeparam>
    /// <param name="tableName">
    /// 表名，对应导出 JSON 中的 tableName，例如 "Item" 或 "Text"。
    /// </param>
    /// <returns>
    /// 成功时返回反序列化后的表数据对象，通常其 data 字段中包含表的所有记录；
    /// 失败时返回 null，例如表不存在、资源加载失败或类型与 JSON 结构不匹配。
    /// </returns>
    /// 示例：ItemTable itemTable = LoadData.LoadJSONData<ItemTable>("Item");
    public static T LoadJSONData<T>(string tableName) where T : class
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            Debug.LogError("LoadJSONData failed: tableName is null or empty.");
            return null;
        }

        string normalizedTableName = tableName.Trim();
        string cacheKey = typeof(T).FullName + ":" + normalizedTableName;
        if (TableCache.TryGetValue(cacheKey, out object cachedValue))
        {
            return cachedValue as T;
        }

        string resourcePath = ResolveTableResourcePath(normalizedTableName);
        if (string.IsNullOrEmpty(resourcePath))
        {
            if (!manifestLoadFailed)
            {
                Debug.LogError($"LoadJSONData failed: table '{normalizedTableName}' not found in tables_manifest.json under Resources/{ExportedTablesResourcesPath}.");
            }
            return null;
        }

        TextAsset tableAsset = Resources.Load<TextAsset>(resourcePath);
        if (tableAsset == null)
        {
            Debug.LogError($"LoadJSONData failed: unable to load resource '{resourcePath}'.");
            return null;
        }

        T tableData = JsonUtility.FromJson<T>(tableAsset.text);
        if (tableData == null)
        {
            Debug.LogError($"LoadJSONData failed: unable to deserialize '{resourcePath}' as {typeof(T).Name}.");
            return null;
        }

        if (!ValidateLoadedTable(tableData, tableAsset.text, normalizedTableName, resourcePath, typeof(T)))
        {
            return null;
        }

        TableCache[cacheKey] = tableData;
        return tableData;
    }

    private static string ResolveTableResourcePath(string tableName)
    {
        Dictionary<string, string> manifest = LoadManifest();
        if (manifest.TryGetValue(tableName, out string outputRelativePath))
        {
            return BuildResourcesPath(outputRelativePath);
        }

        return null;
    }

    private static Dictionary<string, string> LoadManifest()
    {
        if (manifestCache != null)
        {
            return manifestCache;
        }

        manifestCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        manifestLoadFailed = false;

        TextAsset manifestAsset = Resources.Load<TextAsset>(BuildResourcesPath("tables_manifest.json"));
        if (manifestAsset == null)
        {
            manifestLoadFailed = true;
            Debug.LogError($"LoadJSONData failed: missing manifest at Resources/{ExportedTablesResourcesPath}/tables_manifest.json.");
            return manifestCache;
        }

        ManifestData manifestData = JsonUtility.FromJson<ManifestData>(manifestAsset.text);
        if (manifestData == null || manifestData.tables == null)
        {
            manifestLoadFailed = true;
            Debug.LogError($"LoadJSONData failed: unable to parse manifest at Resources/{ExportedTablesResourcesPath}/tables_manifest.json.");
            return manifestCache;
        }

        for (int i = 0; i < manifestData.tables.Count; i++)
        {
            ManifestEntry entry = manifestData.tables[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.tableName) || string.IsNullOrWhiteSpace(entry.outputRelativePath))
            {
                continue;
            }

            manifestCache[entry.tableName.Trim()] = entry.outputRelativePath;
        }

        return manifestCache;
    }

    private static string BuildResourcesPath(string outputRelativePath)
    {
        if (string.IsNullOrWhiteSpace(outputRelativePath))
        {
            return ExportedTablesResourcesPath;
        }

        string normalized = outputRelativePath.Replace('\\', '/');
        if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - ".json".Length);
        }

        return string.IsNullOrEmpty(normalized)
            ? ExportedTablesResourcesPath
            : ExportedTablesResourcesPath + "/" + normalized;
    }

    private static bool ValidateLoadedTable<T>(T tableData, string rawJson, string tableName, string resourcePath, Type targetType) where T : class
    {
        FieldInfo tableNameField = targetType.GetField("tableName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (tableNameField == null || tableNameField.FieldType != typeof(string))
        {
            Debug.LogError($"LoadJSONData failed: target type '{targetType.Name}' must contain a string field named 'tableName'.");
            return false;
        }

        string loadedTableName = tableNameField.GetValue(tableData) as string;
        if (string.IsNullOrWhiteSpace(loadedTableName))
        {
            Debug.LogError($"LoadJSONData failed: deserialized '{resourcePath}' as {targetType.Name}, but required field 'tableName' is empty. The target type likely does not match the exported JSON structure.");
            return false;
        }

        if (!string.Equals(loadedTableName, tableName, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"LoadJSONData failed: deserialized '{resourcePath}' as {targetType.Name}, but JSON tableName='{loadedTableName}' does not match requested table '{tableName}'.");
            return false;
        }

        FieldInfo dataField = targetType.GetField("data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        bool jsonContainsData = rawJson != null && rawJson.IndexOf("\"data\"", StringComparison.Ordinal) >= 0;
        if (dataField == null)
        {
            Debug.LogError($"LoadJSONData failed: target type '{targetType.Name}' must contain a field named 'data' to receive exported rows.");
            return false;
        }

        if (jsonContainsData && dataField.GetValue(tableData) == null)
        {
            Debug.LogError($"LoadJSONData failed: deserialized '{resourcePath}' as {targetType.Name}, but field 'data' is null. The target type likely does not match the exported JSON structure.");
            return false;
        }

        Type rowType = GetCollectionElementType(dataField.FieldType);
        if (rowType == null)
        {
            Debug.LogError($"LoadJSONData failed: target type '{targetType.Name}' field 'data' must be an array or generic collection type.");
            return false;
        }

        List<ExportedColumnSchema> columns = ReadColumnSchema(rawJson);
        for (int i = 0; i < columns.Count; i++)
        {
            ExportedColumnSchema column = columns[i];
            FieldInfo rowField = rowType.GetField(column.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (rowField == null)
            {
                Debug.LogError($"LoadJSONData failed: row type '{rowType.Name}' is missing field '{column.Name}' required by '{resourcePath}'.");
                return false;
            }

            Type expectedFieldType = MapSchemaType(column.Type);
            if (expectedFieldType != null && rowField.FieldType != expectedFieldType)
            {
                Debug.LogError($"LoadJSONData failed: row type '{rowType.Name}' field '{column.Name}' has type '{rowField.FieldType.Name}', expected '{expectedFieldType.Name}' from '{resourcePath}'.");
                return false;
            }
        }

        return true;
    }

    private static Type GetCollectionElementType(Type collectionType)
    {
        if (collectionType == null)
        {
            return null;
        }

        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            Type[] genericArguments = collectionType.GetGenericArguments();
            if (genericArguments.Length == 1)
            {
                return genericArguments[0];
            }
        }

        return null;
    }

    private static List<ExportedColumnSchema> ReadColumnSchema(string rawJson)
    {
        var columns = new List<ExportedColumnSchema>();
        if (string.IsNullOrEmpty(rawJson))
        {
            return columns;
        }

        MatchCollection matches = Regex.Matches(
            rawJson,
            "\\{\\s*\"name\"\\s*:\\s*\"(?<name>[^\"]+)\"\\s*,\\s*\"type\"\\s*:\\s*\"(?<type>[^\"]+)\"",
            RegexOptions.CultureInvariant);

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            if (!match.Success)
            {
                continue;
            }

            columns.Add(new ExportedColumnSchema
            {
                Name = match.Groups["name"].Value,
                Type = match.Groups["type"].Value
            });
        }

        return columns;
    }

    private static Type MapSchemaType(string schemaType)
    {
        switch ((schemaType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "int":
                return typeof(int);
            case "long":
                return typeof(long);
            case "float":
                return typeof(float);
            case "double":
                return typeof(double);
            case "bool":
                return typeof(bool);
            case "string":
                return typeof(string);
            default:
                return null;
        }
    }

    private static void ResetCaches()
    {
        TableCache.Clear();
        manifestCache = null;
        manifestLoadFailed = false;
    }

    private sealed class ExportedColumnSchema
    {
        public string Name;
        public string Type;
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorCacheInvalidation()
    {
        ResetCaches();
    }

    public static void ClearCacheForEditor()
    {
        ResetCaches();
    }

    private sealed class ExportedTableCachePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (TouchesExportedTables(importedAssets)
                || TouchesExportedTables(deletedAssets)
                || TouchesExportedTables(movedAssets)
                || TouchesExportedTables(movedFromAssetPaths))
            {
                ResetCaches();
            }
        }

        private static bool TouchesExportedTables(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                if (!string.IsNullOrEmpty(assetPath)
                    && assetPath.Replace('\\', '/').StartsWith("Assets/Resources/GameData/ExportedTables/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
#endif
}
