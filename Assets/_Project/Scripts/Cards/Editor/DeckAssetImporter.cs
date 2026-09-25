using System.IO;
using UnityEditor;
using UnityEngine;
using ArcanaWars.Cards.Decks;

namespace ArcanaWars.Cards.Editor
{
    /// <summary>
    /// Brings decks built in the deck builder screen (saved as JSON, because a running game cannot write
    /// project assets) into the project as DeckAssets, so they can be dragged onto GameApp in the
    /// Inspector.
    ///
    /// This is the one-way door between the two persistence formats. It's automated rather than done
    /// by hand for the same reason the card database is: it's repetitive *data* work with an exact
    /// right answer, and idempotent — re-running it updates the existing assets in place instead of
    /// making a second copy, so it's safe to run whenever a deck changes.
    /// </summary>
    public static class DeckAssetImporter
    {
        private const string OutputFolder = "Assets/_Project/Data/Decks";

        [MenuItem("Tools/Arcana Wars/Import Saved Decks as Assets")]
        public static void ImportSavedDecks()
        {
            var names = DeckStorage.SavedDeckNames();
            if (names.Count == 0)
            {
                Debug.LogWarning($"No saved decks found in {DeckStorage.DirectoryPath}. Build one in the deck builder screen and press Save first.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            int imported = 0;

            foreach (var name in names)
            {
                var deck = DeckStorage.TryLoad(name, out string error);
                if (deck == null)
                {
                    Debug.LogError($"Skipped \"{name}\": {error}");
                    continue;
                }

                string path = $"{OutputFolder}/{DeckStorage.SanitiseFileName(name)}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<DeckAsset>(path);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<DeckAsset>();
                    asset.EditorSetContents(deck);
                    AssetDatabase.CreateAsset(asset, path);
                }
                else
                {
                    asset.EditorSetContents(deck);
                    EditorUtility.SetDirty(asset);
                }

                imported++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Imported {imported} deck(s) from {DeckStorage.DirectoryPath} into {OutputFolder}.");
        }

        /// <summary>Handy when a saved deck can't be found — the persistent data path differs per platform and per Unity project name.</summary>
        [MenuItem("Tools/Arcana Wars/Show Saved Decks Folder")]
        public static void ShowSavedDecksFolder()
        {
            Directory.CreateDirectory(DeckStorage.DirectoryPath);
            EditorUtility.RevealInFinder(DeckStorage.DirectoryPath);
        }
    }
}
