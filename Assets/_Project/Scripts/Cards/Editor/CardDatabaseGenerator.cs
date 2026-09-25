using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ArcanaWars.Core;
using ArcanaWars.Cards.MinorArcana;
using ArcanaWars.Cards.CourtCards;
using ArcanaWars.Cards.MajorArcana;

namespace ArcanaWars.Cards.Editor
{
    /// <summary>
    /// One-click generator for the full 78-card database (GDD Part 2): 36 Minor Arcana, 20 Court
    /// Cards, 22 Major Arcana. Safe to re-run — it updates the existing asset at each path instead
    /// of duplicating it, so re-running after a rules/formula change (e.g. editing MinorArcanaRules)
    /// refreshes every card's data without hand-editing 78 files.
    /// </summary>
    public static class CardDatabaseGenerator
    {
        private const string RootFolder = "Assets/_Project/Data/Cards";
        private const string MinorFolder = RootFolder + "/MinorArcana";
        private const string CourtFolder = RootFolder + "/CourtCards";
        private const string MajorFolder = RootFolder + "/MajorArcana";

        [MenuItem("Tools/Arcana Wars/Generate Card Database")]
        public static void Generate()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MinorFolder);
            EnsureFolder(CourtFolder);
            EnsureFolder(MajorFolder);

            var minor = GenerateMinorArcana();
            var court = GenerateCourtCards();
            var major = GenerateMajorArcana();

            // One asset listing them all, so a scene needs a single reference instead of 78.
            var database = CreateOrLoad<CardDatabase>($"{RootFolder}/CardDatabase.asset");
            database.EditorSetContents(minor, court, major);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int count = minor.Count + court.Count + major.Count;
            Debug.Log($"Arcana Wars: generated/updated {count} card assets under {RootFolder}, and indexed them in CardDatabase.asset.");
        }

        private static List<MinorArcanaCardDefinition> GenerateMinorArcana()
        {
            var created = new List<MinorArcanaCardDefinition>();
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                for (int value = MinorArcanaRules.MinValue; value <= MinorArcanaRules.MaxValue; value++)
                {
                    var asset = CreateOrLoad<MinorArcanaCardDefinition>($"{MinorFolder}/{value}_of_{element}.asset");
                    asset.EditorSetIdentity(element, value);
                    EditorUtility.SetDirty(asset);
                    created.Add(asset);
                }
            }
            return created;
        }

        private static List<CourtCardDefinition> GenerateCourtCards()
        {
            var created = new List<CourtCardDefinition>();
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                foreach (CourtRank rank in Enum.GetValues(typeof(CourtRank)))
                {
                    var asset = CreateOrLoad<CourtCardDefinition>($"{CourtFolder}/{rank}_of_{element}.asset");
                    asset.EditorSetIdentity(element, rank);
                    EditorUtility.SetDirty(asset);
                    created.Add(asset);
                }
            }
            return created;
        }

        private static List<MajorArcanaCardDefinition> GenerateMajorArcana()
        {
            var created = new List<MajorArcanaCardDefinition>();
            foreach (var data in MajorArcanaData.All)
            {
                var asset = CreateOrLoad<MajorArcanaCardDefinition>($"{MajorFolder}/{data.CardId}.asset");
                asset.EditorApply(data);
                EditorUtility.SetDirty(asset);
                created.Add(asset);
            }
            return created;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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
