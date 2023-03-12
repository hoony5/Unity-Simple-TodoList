using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public class TodoListWindow : EditorWindow
{
    private readonly int Capacity = 32;
    
    private static Dictionary<string, List<(string targetName, string message, int order)>> todoDict =
        new Dictionary<string, List<(string targetName, string message, int order)>>(32);
    private Dictionary<string,List<Vector2>> elementScrollPositions = new Dictionary<string,List<Vector2>>(32);

    private static Dictionary<string, bool> showDetails = new Dictionary<string, bool>(32);
    private static string[] colors = new []{ "red", "orange", "yellow", "green", "cyan", "indigo", "violet", "purple" };
    private Vector2 scrollPosition;

    [MenuItem("Window/Todo List")]
    public static void ShowWindow()
    {
        GetWindow(typeof(TodoListWindow), false, "Todo List");
    }

    private void OnEnable()
    {
        FindAllTodoAttributes();
    }

    private void OnGUI()
    {
        // start layout
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(15);
        EditorGUILayout.BeginVertical( new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            stretchWidth = true,
        });
        // title
        EditorGUILayout.LabelField($"Todo List",
            new GUIStyle(GUI.skin.box)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
            });
        // list info
        EditorGUILayout.LabelField($"( total : <color=yellow>{(todoDict is null ? 0 : todoDict.Sum(i => i.Value.Count))}</color> )",
            new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                richText = true
            });
        EditorGUILayout.EndVertical();
        if(todoDict is null || todoDict.Count == 0)
        {
            EditorGUILayout.EndScrollView();
            return;
        }
        EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.textArea){stretchWidth = true,stretchHeight = true, alignment = TextAnchor.MiddleCenter});
        int index = 0;
        foreach (var kvp in todoDict)
        {
            string fileName = Path.GetFileName(kvp.Key);
            string pathWithoutFileName = kvp.Key.Replace(fileName, "");
            string[] pathElements = pathWithoutFileName.Split('/');
            string stylizedPathWithoutFileName = "";
            
            for (int pathElementIndex = 0;
                 pathElementIndex < pathElements.Length - 1;
                 pathElementIndex++)
            {
                stylizedPathWithoutFileName += $"<color={colors[pathElementIndex % colors.Length]}>{pathElements[pathElementIndex]} ▶</color>";
            }
            EditorGUILayout.LabelField($"<b>{index}</b>. {stylizedPathWithoutFileName}{fileName}  (Todo Count : <color=yellow>{kvp.Value.Count}</color>)", new GUIStyle(GUI.skin.label){richText = true});

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open", GUILayout.Height(24)))
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(kvp.Key));
            }

            bool show = showDetails.ContainsKey(kvp.Key) ? showDetails[kvp.Key] : false;

            string label = show ? "Hide Detail" : "View Detail";

            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                show = !show;
                showDetails[kvp.Key] = show;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(15);
            
            index++;
            if (!show) continue;
            EditorGUI.indentLevel += 2;
            (string targetName, string message, int order)[] orderedArray = kvp.Value.OrderBy(x => x.order).ToArray();
            for (int i = 0; i < orderedArray.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                (string targetName, string message, int order) todo = orderedArray[i];
                string color = "green";
                if (i < 3) color = "red";
                if (2 < i  && i < 6) color = "yellow";
                EditorGUILayout.LabelField($"<color={color}><b>{todo.order}</b></color>. {todo.targetName} {(todo.message.Length >= 30 ? " ▼ Read here " : todo.message.Replace('▲', '◀'))}",
                    new GUIStyle(GUI.skin.label) { richText = true });

                EditorGUILayout.EndHorizontal();

                if (todo.message.Length < 30) continue;
                    
                EditorGUILayout.Space(5);
                elementScrollPositions[kvp.Key][i] = EditorGUILayout.BeginScrollView(elementScrollPositions[kvp.Key][i], GUILayout.Height(200f), GUILayout.ExpandWidth(true));
                todo.message = EditorGUILayout.TextArea(todo.message.Replace('◀', '▲'),
                    new GUIStyle(GUI.skin.textArea)
                    {
                        richText = true,
                        stretchWidth = true,
                        stretchHeight = true,
                    });
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(10);
            }
            EditorGUILayout.Space(15);

            EditorGUI.indentLevel -= 2;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void FindAllTodoAttributes()
    {
        todoDict.Clear();
        showDetails.Clear();
        elementScrollPositions.Clear();

        List<MonoScript> monoScripts = AssetDatabase.FindAssets("t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
            .ToList();

        if (monoScripts.Count == 0)
            return;

        foreach (MonoScript monoScript in monoScripts)
        {
            Type type = monoScript.GetClass();

            if (type == null)
            {
                continue;
            }
            object[] noMemberAttributes = type.GetCustomAttributes(typeof(ToDoAttribute), true);
            // not members
            if (noMemberAttributes.Length != 0)
            {
                string key = AssetDatabase.GetAssetPath(monoScript);

                if (!todoDict.ContainsKey(key))
                {
                    todoDict.Add(key, new List<(string, string, int)>(Capacity));
                    showDetails.Add(key, false);
                    elementScrollPositions.Add(key,new List<Vector2>(32));
                }
                    
                foreach (ToDoAttribute attribute in noMemberAttributes)
                {
                    (string Name, string message, int importance) todo
                        = ($"<b>{type.Name}</b> ◀ {type.BaseType}",
                            $"◀ {attribute.message}",
                            attribute.importance);

                    if (todoDict[key].Contains(todo))
                        todoDict[key].Remove(todo);
                    todoDict[key].Add(todo);
                    elementScrollPositions[key].Add(new Vector2());
                }   
            }
            
            MemberInfo[] members = type.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic |
                                                   BindingFlags.Public | BindingFlags.Static);
                
            foreach (MemberInfo member in members)
            {
                object[] attributes = member.GetCustomAttributes(typeof(ToDoAttribute), true);
                if (attributes.Length == 0) continue;

                string key = AssetDatabase.GetAssetPath(monoScript);

                if (!todoDict.ContainsKey(key))
                {
                    todoDict.Add(key, new List<(string, string, int)>(Capacity));
                    showDetails.Add(key, false);
                    elementScrollPositions.Add(key,new List<Vector2>(32));
                }
                foreach (ToDoAttribute attribute in attributes)
                {
                    (string Name, string message, int importance) todo
                        = ($"<b>{member.Name}</b> ◀ {member.MemberType}",
                            $"◀ {attribute.message}",
                            attribute.importance);

                    if (todoDict[key].Contains(todo))
                        todoDict[key].Remove(todo);
                    todoDict[key].Add(todo);
                    elementScrollPositions[key].Add(new Vector2());
                }
            }
        }
    }
}