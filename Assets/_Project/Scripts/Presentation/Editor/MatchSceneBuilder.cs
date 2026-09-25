using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ArcanaWars.Cards;

namespace ArcanaWars.Presentation.Editor
{
    /// <summary>
    /// Builds a ready-to-play match scene in one click, for the same reason the card generator
    /// exists: hand-wiring a scene is mechanical work that a tool does more reliably.
    /// </summary>
    public static class MatchSceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string ScenePath = ScenesFolder + "/Match.unity";
        private const string DatabasePath = "Assets/_Project/Data/Cards/CardDatabase.asset";

        [MenuItem("Tools/Arcana Wars/Create Match Scene")]
        public static void CreateMatchScene()
        {
            var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (database == null)
            {
                EditorUtility.DisplayDialog(
                    "Card database missing",
                    "Run Tools > Arcana Wars > Generate Card Database first — the scene needs it to build decks.",
                    "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.10f, 0.13f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var matchGo = new GameObject("Match");
            var view = matchGo.AddComponent<MatchView>();

            // cardDatabase is a private [SerializeField], so it's assigned through SerializedObject
            // rather than by making the field public just for tooling's sake.
            var serialized = new SerializedObject(view);
            serialized.FindProperty("cardDatabase").objectReferenceValue = database;
            serialized.ApplyModifiedProperties();

            EnsureFolder(ScenesFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Arcana Wars: created {ScenePath}. Press Play to run a match.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
