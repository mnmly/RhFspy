using Rhino;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class RhinoDocDataExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void SetData<T>(this RhinoDoc doc, string key, T data)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        string serializedData = JsonSerializer.Serialize(data, _jsonOptions);
        doc.Strings.SetString(key, serializedData);
    }

    public static T GetData<T>(this RhinoDoc doc, string key)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        string serializedData = doc.Strings.GetValue(key);
        if (string.IsNullOrEmpty(serializedData))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(serializedData, _jsonOptions);
        }
        catch (JsonException ex)
        {
            RhinoApp.WriteLine($"Error deserializing data: {ex.Message}");
            return default;
        }
    }

    public static bool RemoveData(this RhinoDoc doc, string key)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        doc.Strings.Delete(key);
        return true;
    }

    public static void SetList<T>(this RhinoDoc doc, string key, List<T> list)
    {
        SetData(doc, key, list);
    }

    public static List<T> GetList<T>(this RhinoDoc doc, string key)
    {
        return GetData<List<T>>(doc, key) ?? new List<T>();
    }

    public static void AddToList<T>(this RhinoDoc doc, string key, T item)
    {
        var list = GetList<T>(doc, key);
        list.Add(item);
        SetList(doc, key, list);
    }

    public static bool RemoveFromList<T>(this RhinoDoc doc, string key, T item)
    {
        var list = GetList<T>(doc, key);
        bool removed = list.Remove(item);
        if (removed)
        {
            SetList(doc, key, list);
        }
        return removed;
    }
}