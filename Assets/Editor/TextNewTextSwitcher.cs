using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TextNewTextSwitcher
{
    private const string CreateNewTextMenu = "GameObject/UI/NewText";
    private const string UnityTextMenu = "GameObject/UI/Legacy/Text";
    private const string SwitchToNewTextMenu = "CONTEXT/Text/Switch To NewText";
    private const string SwitchToTextMenu = "CONTEXT/NewText/Switch To Text";
    private const int NewTextMenuPriority = 2080;
    private const string UnityMenuOptionsTypeName = "UnityEditor.UI.MenuOptions";
    private static readonly Dictionary<Type, MonoScript> ScriptCache = new Dictionary<Type, MonoScript>();
    private static MethodInfo unityAddTextMethod;

    [MenuItem(CreateNewTextMenu, false, NewTextMenuPriority)]
    private static void CreateNewText(MenuCommand command)
    {
        Undo.SetCurrentGroupName("Create NewText");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject textObject = CreateUnityTextObject(command);
        Text text = textObject != null ? textObject.GetComponent<Text>() : null;
        if (text == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return;
        }

        Undo.RecordObject(textObject, "Create NewText");
        textObject.name = "NewText";
        GameObjectUtility.EnsureUniqueNameForSibling(textObject);

        NewText newText = ReplaceComponent<Text, NewText>(text, "Create NewText", false);
        if (newText == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return;
        }

        Selection.activeGameObject = textObject;
        Undo.SetCurrentGroupName("Create NewText");
        Undo.CollapseUndoOperations(undoGroup);
    }

    [MenuItem(SwitchToNewTextMenu)]
    private static void SwitchToNewText(MenuCommand command)
    {
        ReplaceComponent<Text, NewText>(command.context as Text, "Switch Text To NewText");
    }

    [MenuItem(SwitchToNewTextMenu, true)]
    private static bool ValidateSwitchToNewText(MenuCommand command)
    {
        return command.context is Text text && !(text is NewText);
    }

    [MenuItem(SwitchToTextMenu)]
    private static void SwitchToText(MenuCommand command)
    {
        ReplaceComponent<NewText, Text>(command.context as NewText, "Switch NewText To Text");
    }

    [MenuItem(SwitchToTextMenu, true)]
    private static bool ValidateSwitchToText(MenuCommand command)
    {
        return command.context is NewText;
    }

    private static void ReplaceComponent<TSource, TDestination>(TSource source, string undoName)
        where TSource : MonoBehaviour
        where TDestination : MonoBehaviour
    {
        ReplaceComponent<TSource, TDestination>(source, undoName, true);
    }

    private static TDestination ReplaceComponent<TSource, TDestination>(TSource source, string undoName, bool collapseUndo)
        where TSource : MonoBehaviour
        where TDestination : MonoBehaviour
    {
        if (source == null)
            return null;

        GameObject gameObject = source.gameObject;
        MonoScript destinationScript = GetMonoScript(typeof(TDestination));
        if (destinationScript == null)
        {
            Debug.LogError($"{undoName} failed: could not find script for {typeof(TDestination).Name}.");
            return null;
        }

        Undo.SetCurrentGroupName(undoName);
        int undoGroup = Undo.GetCurrentGroup();

        Undo.RecordObject(source, undoName);

        SerializedObject serializedObject = new SerializedObject(source);
        SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
        if (scriptProperty == null)
        {
            Debug.LogError($"{undoName} failed on {source.gameObject.name}: m_Script property was not found.");
            if (collapseUndo)
                Undo.CollapseUndoOperations(undoGroup);
            return null;
        }

        scriptProperty.objectReferenceValue = destinationScript;
        serializedObject.ApplyModifiedProperties();

        TDestination destination = gameObject.GetComponent<TDestination>();
        EditorUtility.SetDirty(gameObject);
        if (destination != null)
        {
            EditorUtility.SetDirty(destination);
            PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
        }

        if (collapseUndo)
            Undo.CollapseUndoOperations(undoGroup);

        return destination;
    }

    private static GameObject CreateUnityTextObject(MenuCommand command)
    {
        GameObject contextObject = command.context as GameObject;
        if (contextObject != null)
            Selection.activeGameObject = contextObject;

        if (!InvokeUnityAddText(command) && !EditorApplication.ExecuteMenuItem(UnityTextMenu))
        {
            Debug.LogError("Create NewText failed: could not invoke Unity Legacy Text menu.");
            return null;
        }

        GameObject textObject = Selection.activeGameObject;
        if (textObject == null || textObject.GetComponent<Text>() == null)
        {
            Debug.LogError("Create NewText failed: Unity Legacy Text menu did not create a selected Text object.");
            return null;
        }

        return textObject;
    }

    private static bool InvokeUnityAddText(MenuCommand command)
    {
        MethodInfo addTextMethod = ResolveUnityAddTextMethod();
        if (addTextMethod == null)
            return false;

        try
        {
            addTextMethod.Invoke(null, new object[] { command });
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Create NewText fallback: Unity Legacy Text reflection failed. {exception.Message}");
            return false;
        }
    }

    private static MethodInfo ResolveUnityAddTextMethod()
    {
        if (unityAddTextMethod != null)
            return unityAddTextMethod;

        Type menuOptionsType = null;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            menuOptionsType = assembly.GetType(UnityMenuOptionsTypeName);
            if (menuOptionsType != null)
                break;
        }

        if (menuOptionsType == null)
            return null;

        unityAddTextMethod = menuOptionsType.GetMethod(
            "AddText",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(MenuCommand) },
            null);

        return unityAddTextMethod;
    }

    private static MonoScript GetMonoScript(Type type)
    {
        if (ScriptCache.TryGetValue(type, out MonoScript cachedScript))
            return cachedScript;

        string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == type)
            {
                ScriptCache[type] = script;
                return script;
            }
        }

        MonoScript temporaryScript = GetMonoScriptFromTemporaryComponent(type);
        if (temporaryScript != null)
            ScriptCache[type] = temporaryScript;

        return temporaryScript;
    }

    private static MonoScript GetMonoScriptFromTemporaryComponent(Type type)
    {
        GameObject temporaryObject = new GameObject($"Temp_{type.Name}_ScriptLookup");
        temporaryObject.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            MonoBehaviour component = temporaryObject.AddComponent(type) as MonoBehaviour;
            return component != null ? MonoScript.FromMonoBehaviour(component) : null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporaryObject);
        }
    }
}
