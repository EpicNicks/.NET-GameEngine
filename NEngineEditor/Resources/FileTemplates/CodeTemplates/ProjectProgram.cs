﻿using System.Reflection;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

using SFML.System;
using SFML.Graphics;

using NEngine;
using NEngine.Window;
using NEngine.GameObjects;

/// <summary>
/// Based in the root directory of your project.
/// Runs the scenes created here along with your config files in the Engine's Application class.
/// Modify at your own risk.
/// </summary>
public partial class Program
{
    //  Application.WindowName = <project property window name>
    //      ... rest of properties
    //  List<SceneData> = TraverseAndDeserializeScenesInProjectFolders()
    //  foreach SceneData s => Application.AddScene(s.ToScene())
    //  Application.Run()

    // TODO: click on this file and remove the C# Compile build action 

    // NO REFERENCES TO NEngineEditor ALLOWED
    //  ALL TYPES MUST BE DEFINED OR REDEFINED HERE EXPLICITLY
    public static void Main()
    {
        ProjectSettings? projectSettings = LoadProjectSettings();
        if (projectSettings is not null)
        {
            Application.Instance.GameWindow.WindowBackgroundColor = projectSettings.BackgroundColor();
            Application.WindowTitle = projectSettings.ProjectName ?? "NEngine Game";

            // load scenes
            List<SceneData> sceneDataList = LoadSceneDataFiles(projectSettings);
            List<Scene> scenes = BuildScenesFromSceneData(sceneDataList);

            foreach (Scene scene in scenes)
            {
                Application.AddScene(scene);
            }
        }

        Application.Run();
    }

    private partial class ProjectSettings
    {
        public string? ProjectName { get; set; }
        public string? DefaultBackgroundColor { get; set; }
        /// <summary>
        /// An ordered list of scenes to be added
        /// </summary>
        public List<string>? ScenePaths { get; set; }

        public Color BackgroundColor()
        {
            byte ParseColorStringAtRange(Range idx) => byte.Parse(DefaultBackgroundColor[idx], NumberStyles.HexNumber);
            byte ParseColorStringAtIndex(int idx) => ParseColorStringAtRange(idx..++idx);

            if (DefaultBackgroundColor is null || ValidColorRegex().IsMatch(DefaultBackgroundColor))
            {
                return Color.White;
            }
            if (DefaultBackgroundColor.Length == 3)
            {
                return new Color(ParseColorStringAtIndex(1), ParseColorStringAtIndex(2), ParseColorStringAtIndex(3));
            }
            return new Color(ParseColorStringAtRange(1..2), ParseColorStringAtRange(3..4), ParseColorStringAtRange(5..6));
        }

        [GeneratedRegex("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")]
        private static partial Regex ValidColorRegex();
    }

    private class SceneData
    {
        public string? Name { get; set; }
        public List<GameObjectData>? SceneGameObjects { get; set; }
    }

    private class GameObjectData
    {
        public Guid Guid { get; set; }
        public string? Name { get; set; }
        public string? GameObjectClass { get; set; }
        public Dictionary<string, TypeValuePair>? GameObjectPropertyNameTypeValue { get; set; }
        public RenderLayer RenderLayer { get; set; }

        public class TypeValuePair
        {
            public string? Type { get; set; }
            public string? Value { get; set; }
        }
    }

    private static partial class Vector2fParser
    {
        public static Vector2f ParseOrZero(string vector2fString)
        {
            var match = ValidVector2fRegex().Match(vector2fString);
            if (match.Groups.Count == 2 && float.TryParse(match.Groups[0].Value, out float x) && float.TryParse(match.Groups[1].Value, out float y))
            {
                return new(x, y);
            }
            return new(0, 0);
        }
        [GeneratedRegex(@"\{\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*\}")]
        public static partial Regex ValidVector2fRegex();
    }

    private static partial class Vector2iParser
    {
        public static Vector2i ParseOrZero(string vector2iString)
        {
            var match = ValidVector2iRegex().Match(vector2iString);
            if (match.Groups.Count == 2 && int.TryParse(match.Groups[0].Value, out int x) && int.TryParse(match.Groups[1].Value, out int y))
            {
                return new(x, y);
            }
            return new(0, 0);
        }
        [GeneratedRegex(@"\{\s*(\d+)\s*,\s*(\d+)\s*\}")]
        public static partial Regex ValidVector2iRegex();
    }
    private static partial class Vector2uParser
    {
        public static Vector2u ParseOrZero(string vector2uString)
        {
            var match = ValidVector2uRegex().Match(vector2uString);
            if (match.Groups.Count == 2 && uint.TryParse(match.Groups[0].Value, out uint x) && uint.TryParse(match.Groups[1].Value, out uint y))
            {
                return new(x, y);
            }
            return new(0, 0);
        }
        [GeneratedRegex(@"\{\s*(\d+)\s*,\s*(\d+)\s*\}")]
        public static partial Regex ValidVector2uRegex();
    }

    private static partial class Vector3fParser
    {
        public static Vector3f ParseOrZero(string vector3fString)
        {
            var match = ValidVector3fRegex().Match(vector3fString);
            if (match.Groups.Count == 3 && float.TryParse(match.Groups[0].Value, out float x) && float.TryParse(match.Groups[1].Value, out float y) && float.TryParse(match.Groups[2].Value, out float z))
            {
                return new(x, y, z);
            }
            return new(0, 0, 0);
        }
        [GeneratedRegex(@"\{\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*\}")]
        public static partial Regex ValidVector3fRegex();
    }

    private static object? ConvertProperty(string typeOfValue, string value) 
        => typeOfValue switch
        {
            "string" or "String" => value,
            "bool" => bool.Parse(value),
            "int" => int.Parse(value),
            "float" => float.Parse(value),
            "double" => double.Parse(value),
            "Vector2u" => Vector2uParser.ParseOrZero(value),
            "Vector2f" => Vector2fParser.ParseOrZero(value),
            "Vector2i" => Vector2iParser.ParseOrZero(value),
            "Vector3f" => Vector3fParser.ParseOrZero(value),
            "Reference" or "reference" or "Guid" or "guid" => Guid.Parse(value),
            _ => null
        };

    private static ProjectSettings? LoadProjectSettings()
    {
        string configPath = "Assets/ProjectConfig.json";
        if (!Directory.Exists(configPath))
        {
            return default;
        }
        string jsonContentString = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<ProjectSettings>(jsonContentString);
    }

    private static List<SceneData> LoadSceneDataFiles(ProjectSettings projectSettings)
    {
        // Scenes need to be specified in order in Project Settings

        //List<string> scenePaths = FindScenesInPath("Assets");
        // foreach string path in scenePaths => File.ReadAllText(path) |> JsonSerializer.Deserialize<SceneData>
        List<SceneData> scenes = [];
        List<string> scenePaths = projectSettings.ScenePaths ?? [];
        foreach (string path in scenePaths)
        {
            string sceneJsonString = File.ReadAllText(path);
            try
            {
                SceneData? sceneData = JsonSerializer.Deserialize<SceneData>(sceneJsonString);
                // TODO: ensure the SceneData deserialization accounts for nested objects such as GameObjectData.TypeValuePair
                if (sceneData is not null)
                {
                    scenes.Add(sceneData);
                }
            }
            catch (JsonException)
            {
                // not sure how to handle debug logging on a build yet
                //  if debug set (somehow) set the build type to console app?
            }
        }

        return scenes;
    }

    private static readonly string[] _specialProperties = ["Position", "Rotation"];

    private static List<Scene> BuildScenesFromSceneData(List<SceneData> sceneDataList)
    {
        List<Scene> builtScenes = [];
        int unnamedSceneCount = 1;
        foreach (SceneData sceneData in sceneDataList)
        {
            List<GameObjectData> sceneGameObjectData = sceneData.SceneGameObjects ?? [];
            List<(RenderLayer renderLayer, GameObject gameObject)> toAddGameObjects = [];
            foreach (GameObjectData gameObjectData in sceneGameObjectData)
            {
                if 
                (
                    gameObjectData.GameObjectClass is null 
                    || Type.GetType(gameObjectData.GameObjectClass) is not Type gameObjectType 
                    || Activator.CreateInstance(gameObjectType) is not GameObject gameObject
                )
                {
                    continue;
                }
                toAddGameObjects.Add((gameObjectData.RenderLayer, gameObject));
            }
            // resolve properties (second loop to resolve Guid references to objects which need to be instantiated)
            foreach ((int i, GameObjectData gameObjectData) in sceneGameObjectData.Select((value, i) => (i, value)))
            {
                try
                {
                    if (gameObjectData.GameObjectClass is null)
                    {
                        continue;
                    }
                    if (Type.GetType(gameObjectData.GameObjectClass) is not Type gameObjectType)
                    {
                        continue;
                    }
                    GameObject gameObject = toAddGameObjects[i].gameObject;
                    if (gameObjectData.GameObjectPropertyNameTypeValue is null)
                    {
                        continue;
                    }
                    foreach (string memberName in gameObjectData.GameObjectPropertyNameTypeValue.Keys)
                    {
                        if (_specialProperties.Contains(memberName) && gameObject is Positionable)
                        {
                            PropertyInfo? propertyInfo = gameObjectType.GetProperty(memberName);
                            GameObjectData.TypeValuePair typeValue = gameObjectData.GameObjectPropertyNameTypeValue[memberName];
                            if (propertyInfo is null || typeValue.Type is null || typeValue.Value is null || ConvertProperty(typeValue.Type, typeValue.Value) is not object propertyValue)
                            {
                                continue;
                            }
                            propertyInfo.SetValue(gameObject, propertyValue);
                        }
                        else
                        {
                            FieldInfo? fieldInfo = gameObjectType.GetField(memberName);
                            GameObjectData.TypeValuePair typeValue = gameObjectData.GameObjectPropertyNameTypeValue[memberName];
                            if (fieldInfo is null || typeValue.Type is null || typeValue.Value is null || ConvertProperty(typeValue.Type, typeValue.Value) is not object fieldValue)
                            {
                                continue;
                            }
                            
                            if (fieldValue is Guid guidProperty)
                            {
                                int foundIndex = sceneGameObjectData.IndexOf(sceneGameObjectData.Where(gObjData => gObjData.Guid == guidProperty).First());
                                fieldInfo.SetValue(gameObject, toAddGameObjects[foundIndex].gameObject);
                            }
                            else
                            {
                                fieldInfo.SetValue(gameObject, fieldValue);
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
            builtScenes.Add(new Scene(sceneData.Name ?? $"Scene {unnamedSceneCount++}", add =>
            {
                add([.. toAddGameObjects]);
            }));
        }
        return builtScenes;
    }
}
