﻿using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;

using SFML.System;

using NEngine.GameObjects;

using NEngineEditor.Converters.Json;
using NEngineEditor.Converters.Parsing;
using NEngineEditor.Managers;
using NEngineEditor.Model;
using NEngineEditor.ViewModel;

namespace NEngineEditor.Helpers;
public static class SceneLoader
{
    public static List<MainViewModel.LayeredGameObject> LoadSceneFromSceneModel(SceneModel sceneModel)
    {
        List<MainViewModel.LayeredGameObject> loadedGameObjects = [];
        string sceneName = "Unnamed Scene";

        static object? ConvertProperty(string typeOfValue, string value)
        {
            return typeOfValue switch
            {
                "string" or "String" => value,
                "bool" or "Boolean" => bool.Parse(value),
                "int" or "Int32" => int.Parse(value),
                "float" or "Single" => float.Parse(value),
                "double" or "Double" => double.Parse(value),
                "Vector2u" => Vector2uParser.ParseOrZero(value),
                "Vector2f" => Vector2fParser.ParseOrZero(value),
                "Vector2i" => Vector2iParser.ParseOrZero(value),
                "Vector3f" => Vector3fParser.ParseOrZero(value),
                "Reference" or "reference" or "Guid" or "guid" => Guid.Parse(value),
                _ => ""
            };
        }

        static string? FindFilePathMatchingTypeInProject(string subdirectory, string className)
        {
            try
            {
                foreach (string file in Directory.GetFiles(subdirectory))
                {
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    if (fileNameWithoutExtension.Equals(className, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }

                // Recursively check subdirectories
                foreach (string directory in Directory.GetDirectories(subdirectory))
                {
                    string? result = FindFilePathMatchingTypeInProject(directory, className);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogError($"Access denied to {subdirectory}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error accessing {subdirectory}: {e.Message}");
            }

            // File not found
            return null;
        }
        try
        {
            sceneName = sceneModel.Name ?? sceneName;
            List<GameObjectWrapperModel> sceneGameObjectData = sceneModel.SceneGameObjects ?? [];
            List<GameObjectWrapperModel> invalidGameObjects = [];
            foreach (GameObjectWrapperModel gameObjectData in sceneGameObjectData)
            {
                if
                (
                    gameObjectData.GameObjectClass is null
                    || FindFilePathMatchingTypeInProject(Path.Join(MainWindow.ProjectDirectory, "Assets"), gameObjectData.GameObjectClass) is not string pathToFile
                    || ScriptCompiler.CompileAndInstantiateFromFile(pathToFile) is not GameObject gameObject
                )
                {
                    invalidGameObjects.Add(gameObjectData);
                    Logger.LogWarning($"Invalid GameObject {gameObjectData} found in scene that was being loaded");
                    continue;
                }
                gameObject.Name = gameObjectData.Name;
                loadedGameObjects.Add(new() { RenderLayer = gameObjectData.RenderLayer, GameObject = gameObject });
            }
            sceneGameObjectData.RemoveAll(invalidGameObjects.Contains);
            // resolve properties (second loop to resolve Guid references to objects which need to be instantiated)
            foreach ((int i, GameObjectWrapperModel gameObjectData) in sceneGameObjectData.Select((value, i) => (i, value)))
            {
                try
                {
                    GameObject gameObject = loadedGameObjects[i].GameObject;
                    Type gameObjectType = gameObject.GetType();
                    if (gameObjectData.GameObjectPropertyNameTypeValue is null)
                    {
                        continue;
                    }
                    foreach (string memberName in gameObjectData.GameObjectPropertyNameTypeValue.Keys)
                    {
                        if (gameObjectType.GetProperty(memberName) is PropertyInfo propertyInfo)
                        {
                            GameObjectWrapperModel.TypeValuePair typeValue = gameObjectData.GameObjectPropertyNameTypeValue[memberName];
                            if (typeValue.Type is null || typeValue.Value is null || ConvertProperty(typeValue.Type, typeValue.Value) is not object propertyValue)
                            {
                                continue;
                            }
                            propertyInfo.SetValue(gameObject, propertyValue);
                        }
                        else if (gameObjectType.GetField(memberName) is FieldInfo fieldInfo)
                        {
                            GameObjectWrapperModel.TypeValuePair typeValue = gameObjectData.GameObjectPropertyNameTypeValue[memberName];
                            if (typeValue.Type is null || typeValue.Value is null)
                            {
                                continue;
                            }
                            object? fieldValue = ConvertProperty(typeValue.Type, typeValue.Value);
                            if (fieldValue is string fieldString && string.IsNullOrEmpty(fieldString))
                            {
                                Type? fieldType = gameObject.GetType().GetField(memberName)?.FieldType;
                                if (fieldType is not null)
                                {
                                    Enum.TryParse(fieldType, typeValue.Value, out fieldValue);
                                }
                            }
                            if (fieldValue == null)
                            {
                                continue;
                            }
                            if (fieldValue is Guid guidProperty)
                            {
                                int foundIndex = sceneGameObjectData.FindIndex(gObjData => gObjData.Guid == guidProperty);
                                if (foundIndex != -1)
                                {
                                    fieldInfo.SetValue(gameObject, loadedGameObjects[foundIndex].GameObject);
                                }
                                else
                                {
                                    fieldInfo.SetValue(gameObject, null);
                                }
                            }
                            else
                            {
                                fieldInfo.SetValue(gameObject, fieldValue);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load gameObject: {gameObjectData.Name} in scene: {sceneModel.Name} due to an Exception: {ex}");
                }
            }
            Logger.LogInfo($"Loaded scene {sceneModel.Name}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"An Exception Occurred while loading the scene, Exception: {ex}");
        }

        return loadedGameObjects;
    }

    public static (string sceneName, List<MainViewModel.LayeredGameObject>) LoadSceneFromJson(string jsonString)
    {
        JsonSerializerOptions jsonSerializerOptions = new();
        JsonConverter[] jsonConverters = [
            new Vector2fConverter(),
            new Vector2iConverter(),
            new Vector2uConverter(),
            new Vector3fConverter()
        ];
        foreach (JsonConverter jsonConverter in jsonConverters)
        {
            jsonSerializerOptions.Converters.Add(jsonConverter);
        }
        SceneModel sceneModel = JsonSerializer.Deserialize<SceneModel>(jsonString, jsonSerializerOptions) ?? throw new InvalidDataException("Provided Scene Model was null");
        return (sceneModel.Name ?? "Unnamed Scene", LoadSceneFromSceneModel(sceneModel));
    }

    public static SceneModel WriteGameObjectsToScene(string sceneName, IList<MainViewModel.LayeredGameObject> gameObjects)
    {
        SceneModel sceneToWrite = new() { Name = sceneName, SceneGameObjects = [] };

        static bool IsSupportedType(Type type)
        {
            return type == typeof(bool) ||
                type == typeof(byte) ||
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(ushort) ||
                type == typeof(int) ||
                type == typeof(uint) ||
                type == typeof(long) ||
                type == typeof(ulong) ||
                type == typeof(float) ||
                type == typeof(double) ||
                type == typeof(decimal) ||
                type == typeof(string) ||
                type.IsEnum ||
                type == typeof(Vector2f) ||
                type == typeof(Vector2i) ||
                type == typeof(Vector2u) ||
                type == typeof(Vector3f);
        }

        static string? MemberValueToString(object? value) => value switch
        {
            sbyte or byte or int or uint or short or ushort or long or ulong or float or double or decimal or bool or string or Enum => value.ToString(),
            Vector2i v => $"{{ {v.X}, {v.Y} }}",
            Vector2f v => $"{{ {v.X}, {v.Y} }}",
            Vector2u v => $"{{ {v.X}, {v.Y} }}",
            Vector3f v => $"{{ {v.X}, {v.Y}, {v.Z} }}",
            _ => ""
        };
        int unnamedGOIndex = 1;
        foreach (MainViewModel.LayeredGameObject lgo in gameObjects)
        {
            sceneToWrite.SceneGameObjects.Add(new(lgo.GameObject.GetType().FullName ?? "GameObject")
            {
                Name = lgo.GameObject.Name ?? $"Unnamed GO {unnamedGOIndex++}",
                Guid = Guid.NewGuid(),
                RenderLayer = lgo.RenderLayer,
                // set through reflection in second foreach where GameObject references to each other are resolved or Guid.Empty
                GameObjectPropertyNameTypeValue = [],
            });
        }
        foreach ((MainViewModel.LayeredGameObject lgo, int i) in gameObjects.Select((lgo, i) => (lgo, i)))
        {
            // resolve object references found in sceneToWrite.SceneGameObjects to their assigned Guids,
            // resolve regular references to their output string (ie. Vector2f -> { {x}, {y} }
            foreach (PropertyInfo property in lgo.GameObject.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsSupportedType(property.PropertyType))
                {
                    sceneToWrite.SceneGameObjects[i].GameObjectPropertyNameTypeValue?.Add(property.Name, new()
                    {
                        Type = property.PropertyType.Name,
                        Value = MemberValueToString(property.GetValue(lgo.GameObject))
                    });
                }
                else if (property.PropertyType.IsAssignableTo(typeof(GameObject)))
                {
                    object? foundObject = gameObjects.FirstOrDefault(sgo => sgo.GameObject == property.GetValue(lgo.GameObject));
                    int sceneGameObjectIndex = foundObject is MainViewModel.LayeredGameObject foundLgo ? gameObjects.IndexOf(foundLgo) : -1;
                    sceneToWrite.SceneGameObjects[i].GameObjectPropertyNameTypeValue?.Add(property.Name, new()
                    {
                        Type = "Reference",
                        Value = (sceneGameObjectIndex != -1 ? sceneToWrite.SceneGameObjects[sceneGameObjectIndex].Guid : Guid.Empty).ToString()
                    });
                }
            }

            foreach (FieldInfo field in lgo.GameObject.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsSupportedType(field.FieldType))
                {
                    sceneToWrite.SceneGameObjects[i].GameObjectPropertyNameTypeValue?.Add(field.Name, new()
                    {
                        Type = field.FieldType.Name,
                        Value = MemberValueToString(field.GetValue(lgo.GameObject))
                    });
                }
                else if (field.FieldType.IsAssignableTo(typeof(GameObject)))
                {
                    object? foundObject = gameObjects.FirstOrDefault(sgo => sgo.GameObject == field.GetValue(lgo.GameObject));
                    int sceneGameObjectIndex = foundObject is MainViewModel.LayeredGameObject foundLgo ? gameObjects.IndexOf(foundLgo) : -1;
                    sceneToWrite.SceneGameObjects[i].GameObjectPropertyNameTypeValue?.Add(field.Name, new()
                    {
                        Type = "Reference",
                        Value = (sceneGameObjectIndex != -1 ? sceneToWrite.SceneGameObjects[sceneGameObjectIndex].Guid : Guid.Empty).ToString()
                    });
                }
            }
        }

        return sceneToWrite;
    }
}
