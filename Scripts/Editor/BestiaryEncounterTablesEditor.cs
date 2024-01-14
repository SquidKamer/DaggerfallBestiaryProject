using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.Globalization;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;

namespace DaggerfallBestiaryProject
{
    internal class BestiaryEncounterTablesEditor : EditorWindow
    {
        static BestiaryEncounterTablesEditor instance;

        string workingMod;
        string activeFile;
        string activeEncounterTable;

        class EncounterTable
        {
            public string Name;
            public int[] TableIndices;
            public int[] EnemyIds;
        }

        EncounterTable[] activeFileEncounterTables;

        class CustomEnemy
        {
            public int Id;
            public string Name;
            public MobileTeams Team;
            public int Level;
        }
        Dictionary<int, CustomEnemy> customEnemyDb = new Dictionary<int, CustomEnemy>();

        Dictionary<MobileTeams, int> currentTableTeamCounts;
        Dictionary<int, int> classicEnemyUseCount;
        Dictionary<int, int> customEnemyUseCount;

        Vector2 scroll1Pos;
        Vector2 scroll2Pos;
        
        [MenuItem("Daggerfall Tools/Encounter Tables Editor")]
        static void Init()
        {
            instance = (BestiaryEncounterTablesEditor)GetWindow(typeof(BestiaryEncounterTablesEditor));
            instance.titleContent = new GUIContent("Encounter Tables Editor");
        }

        private void OnEnable()
        {
            workingMod = null;
            activeFile = null;
            activeEncounterTable = null;
            activeFileEncounterTables = null;
        }

        void OnGUI()
        {
            float baseX = 0;
            float baseY = 0;
            float availableWidth = 0; 

            GUI.Label(new Rect(baseX + 4, baseY + 4, 124, 16), "Active Mod: ");
            baseX += 128;

            if (EditorGUI.DropdownButton(new Rect(baseX + 4, baseY + 4, 160, 16), new GUIContent(workingMod), FocusType.Passive))
            {
                void OnItemClicked(object mod)
                {
                    workingMod = (string)mod;
                    activeFile = null;
                    activeEncounterTable = null;
                    currentTableTeamCounts = null;
                    LoadCustomEnemies();
                }

                GenericMenu menu = new GenericMenu();
                foreach (string mod in BestiaryModManager.GetDevMods())
                {
                    menu.AddItem(new GUIContent(mod), workingMod == mod, OnItemClicked, mod);
                }

                menu.DropDown(new Rect(92, baseY + 8, 160, 16));
            }

            baseX += 164;

            availableWidth = position.width - baseX;

            if(GUI.Button(new Rect(baseX + availableWidth - 128, baseY + 4, 124, 16), "Refresh"))
            {
                RefreshFile();
            }

            baseX = 0;
            baseY += 20;

            using(new EditorGUI.DisabledScope(string.IsNullOrEmpty(workingMod)))
            {
                GUI.Label(new Rect(baseX + 4, baseY + 4, 124, 16), "Active File: ");
                baseX += 128;

                if (EditorGUI.DropdownButton(new Rect(baseX + 4, baseY + 4, 160, 16), new GUIContent(GetEncounterTableName(activeFile)), FocusType.Passive))
                {
                    void OnItemClicked(object file)
                    {
                        activeFile = (string)file;
                        activeEncounterTable = null;
                        currentTableTeamCounts = null;
                        LoadActiveFile();
                        LoadFileStatistics();
                    }

                    GenericMenu menu = new GenericMenu();

                    ModInfo workingModInfo = BestiaryModManager.GetModInfo(workingMod);
                    var encounterTableFiles = workingModInfo.Files.Where(file => file.EndsWith(".tdb.csv"));
                    foreach (string file in encounterTableFiles)
                    {
                        menu.AddItem(new GUIContent(GetEncounterTableName(file)), activeFile == file, OnItemClicked, file);
                    }

                    menu.DropDown(new Rect(92, baseY + 8, 160, 16));
                }

                baseX = 0;
                baseY += 20;

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(activeFile)))
                {
                    GUI.Label(new Rect(baseX + 4, baseY + 4, 124, 16), "Encounter Table: ");
                    baseX += 128;

                    if (EditorGUI.DropdownButton(new Rect(baseX + 4, baseY + 4, 160, 16), new GUIContent(activeEncounterTable), FocusType.Passive))
                    {
                        void OnItemClicked(object tableName)
                        {
                            activeEncounterTable = (string)tableName;
                            LoadTableStatistics();
                        }

                        GenericMenu menu = new GenericMenu();

                        menu.AddItem(new GUIContent("--"), string.IsNullOrEmpty(activeEncounterTable), (_) => { activeEncounterTable = null; }, null);

                        foreach (EncounterTable table in activeFileEncounterTables)
                        {
                            menu.AddItem(new GUIContent(table.Name), activeEncounterTable == table.Name, OnItemClicked, table.Name);
                        }

                        menu.DropDown(new Rect(92, baseY + 8, 160, 16));
                    }

                    baseX = 0;
                    baseY += 20;
                }

                baseY += 24; // Spacing

                if (!string.IsNullOrEmpty(activeEncounterTable))
                {
                    EncounterTable tableData = activeFileEncounterTables.First(table => table.Name == activeEncounterTable);

                    GUI.Label(new Rect(baseX + 4, baseY + 4, 84, 16), "Tables: ");

                    for (int i = 0; i < tableData.TableIndices.Length; ++i)
                    {
                        int tableIndex = tableData.TableIndices[i];
                        GUI.Label(new Rect(baseX + 8, baseY + 24 + i * 24, 148, 20), TableIndexToName(tableIndex));
                    }

                    baseX += 160;

                    GUI.Label(new Rect(baseX + 4, baseY + 4, 84, 16), "Enemies: ");

                    for (int i = 0; i < tableData.EnemyIds.Length; ++i)
                    {
                        int enemyId = tableData.EnemyIds[i];

                        string enemyName;
                        if(enemyId == -1)
                        {
                            enemyName = null;
                        }
                        else if (customEnemyDb.TryGetValue(enemyId, out CustomEnemy enemy) && !string.IsNullOrEmpty(enemy.Name))
                        {
                            enemyName = enemy.Name;
                        }
                        else if (Enum.IsDefined(typeof(MobileTypes), enemyId))
                        {
                            enemyName = ((MobileTypes)enemyId).ToString();
                        }                        
                        else
                        {
                            enemyName = $"Unknown id '{enemyId}'";
                        }

                        if(enemyId != -1)
                            GUI.Label(new Rect(baseX + 8, baseY + 24 + i * 24, 204, 20), $"{i + 1:D2}. ({enemyId:D3}) {enemyName}");
                        else
                            GUI.Label(new Rect(baseX + 8, baseY + 24 + i * 24, 204, 20), $"{i + 1:D2}. [EMPTY]");
                    }

                    baseX += 212;

                    GUI.Label(new Rect(baseX + 4, baseY + 4, 84, 16), "Level: ");

                    for (int i = 0; i < tableData.EnemyIds.Length; ++i)
                    {
                        int enemyId = tableData.EnemyIds[i];
                        if (enemyId == -1)
                            continue;

                        int enemyLevel;
                        if (customEnemyDb.TryGetValue(enemyId, out CustomEnemy enemy) && enemy.Level != 0)
                        {
                            enemyLevel = enemy.Level;
                        }
                        else if (Enum.IsDefined(typeof(MobileTypes), enemyId))
                        {
                            enemyLevel = EnemyBasics.Enemies.First(m => m.ID == enemyId).Level;
                        }
                        else
                        {
                            enemyLevel = 0;
                        }

                        if (enemyLevel != 0)
                        {
                            GUI.Label(new Rect(baseX + 8, baseY + 24 + i * 24, 148, 20), enemyLevel.ToString());
                        }
                    }

                    baseX += 160;

                    if (currentTableTeamCounts != null && currentTableTeamCounts.Count > 0)
                    {
                        GUI.Label(new Rect(baseX + 4, baseY + 4, 84, 16), "Teams: ");

                        var teamCountEntries = currentTableTeamCounts.ToList();
                        teamCountEntries.Sort((tc1, tc2) => tc2.Value.CompareTo(tc1.Value));

                        for (int i = 0; i < teamCountEntries.Count; ++i)
                        {
                            var tableCountPair = teamCountEntries[i];
                            var team = tableCountPair.Key;
                            var count = tableCountPair.Value;

                            GUI.Label(new Rect(baseX + 8, baseY + 24 + i * 24, 148, 20), $"{team}: {count}");
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(activeFile)) // string.IsNullOrEmpty(activeEncounterTable)
                {
                    GUI.Label(new Rect(baseX + 4, baseY + 4, 104, 16), "Classic enemy:");
                    GUI.Label(new Rect(baseX + 112, baseY + 4, 84, 16), "Use count:");

                    float availableHeight = position.height - baseY - 24;
                    scroll1Pos = GUI.BeginScrollView(new Rect(baseX + 4, baseY + 24, 200, availableHeight - 4), scroll1Pos, new Rect(0, 0, 180, customEnemyDb.Keys.Count * 24), false, true);

                    try
                    {
                        int i = 0;
                        foreach (var mobileType in Enum.GetValues(typeof(MobileTypes)).Cast<MobileTypes>())
                        {
                            GUI.Label(new Rect(0, 24 * i, 104, 16), mobileType.ToString());

                            if (classicEnemyUseCount.TryGetValue((int)mobileType, out int useCount))
                            {
                                GUI.Label(new Rect(108, 24 * i, 84, 16), useCount.ToString());
                            }
                            else
                            {
                                GUI.Label(new Rect(108, 24 * i, 84, 16), "0");
                            }

                            ++i;
                        }
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }

                    baseX += 216;

                    GUI.Label(new Rect(baseX + 4, baseY + 4, 104, 16), "Custom enemy:");
                    GUI.Label(new Rect(baseX + 112, baseY + 4, 84, 16), "Use count:");

                    availableHeight = position.height - baseY - 24;

                    var customEnemies = customEnemyDb.Where(enemyPair => !Enum.IsDefined(typeof(MobileTypes), enemyPair.Key));

                    scroll2Pos = GUI.BeginScrollView(new Rect(baseX + 4, baseY + 24, 200, availableHeight - 4), scroll2Pos, new Rect(0, 0, 180, customEnemies.Count() * 24), false, true);

                    try
                    {
                        int i = 0;
                        foreach (var enemyPair in customEnemies)
                        {
                            CustomEnemy enemy = enemyPair.Value;

                            GUI.Label(new Rect(0, 24 * i, 104, 16), enemy.Name);

                            if (customEnemyUseCount.TryGetValue(enemy.Id, out int useCount))
                            {
                                GUI.Label(new Rect(108, 24 * i, 84, 16), useCount.ToString());
                            }
                            else
                            {
                                GUI.Label(new Rect(108, 24 * i, 84, 16), "0");
                            }

                            ++i;
                        }
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }
                }
            }
        }

        string GetEncounterTableName(string tablePath)
        {
            if (string.IsNullOrEmpty(tablePath))
                return string.Empty;

            return Path.GetFileNameWithoutExtension(tablePath).Replace(".tdb", "");
        }

        string TableIndexToName(int tableIndex)
        {
            if(tableIndex < 19)
            {
                return ((DFRegion.DungeonTypes)tableIndex).ToString();
            }

            switch(tableIndex)
            {
                case 19: return "Underwater";
                case 20: return "Desert City Night";
                case 21: return "Desert Day";
                case 22: return "Desert Night";
                case 23: return "Mountain City Night";
                case 24: return "Mountain Day";
                case 25: return "Mountain Night";
                case 26: return "Rainforest City Night";
                case 27: return "Rainforest Day";
                case 28: return "Rainforest Night";
                case 29: return "Subtropical City Night";
                case 30: return "Subtropical Day";
                case 31: return "Subtropical Night";
                case 32: return "Woodlands City Night";
                case 33: return "Woodlands Day";
                case 34: return "Woodlands Night";
                case 35: return "Haunted City Night";
                case 36: return "Haunted Day";
                case 37: return "Haunted Night";
            }

            throw new Exception($"Could not find table name for index '{tableIndex}'");
        }

        static Regex CsvSplit = new Regex("(?:^|,)(\"(?:\\\\\"|[^\"])*\"|[^,]*)", RegexOptions.Compiled);

        static string[] SplitCsvLine(string line)
        {
            List<string> list = new List<string>();
            foreach (Match match in CsvSplit.Matches(line))
            {
                string curr = match.Value;
                if (0 == curr.Length)
                {
                    list.Add("");
                }

                list.Add(curr.TrimStart(',', ';').Replace("\\\"", "\"").Trim('\"'));
            }

            return list.ToArray();
        }

        int[] ParseArrayArg(string Arg, string Context)
        {
            if (string.IsNullOrEmpty(Arg))
                return Array.Empty<int>();

            // Strip brackets
            if (Arg[0] == '[' || Arg[0] == '{')
            {
                // Check for end bracket
                if (Arg[0] == '[' && Arg[Arg.Length - 1] != ']'
                    || Arg[0] == '{' && Arg[Arg.Length - 1] != '}')
                    throw new InvalidDataException($"Error parsing ({Context}): array argument has mismatched brackets");

                Arg = Arg.Substring(1, Arg.Length - 2);
            }

            string[] Frames = Arg.Split(',', ';');
            return Frames.Select(Frame => string.IsNullOrEmpty(Frame) ? "-1" : Frame).Select(int.Parse).ToArray();
        }

        void LoadCustomEnemies()
        {
            customEnemyDb = new Dictionary<int, CustomEnemy>();

            foreach (TextAsset asset in BestiaryModManager.FindAssets<TextAsset>(workingMod, "*.mdb.csv"))
            {
                var stream = new StreamReader(new MemoryStream(asset.bytes));

                string header = stream.ReadLine();

                string[] fields = header.Split(';', ',').Select(field => field.Trim('\"')).ToArray();

                bool GetIndex(string fieldName, out int index)
                {
                    index = -1;
                    for (int i = 0; i < fields.Length; ++i)
                    {
                        if (fields[i].Equals(fieldName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            index = i;
                            break;
                        }
                    }
                    if (index == -1)
                    {
                        Debug.LogError($"Monster DB file '{asset.name}': could not find field '{fieldName}' in header");
                        return false;
                    }
                    return true;
                }

                int? GetIndexOpt(string fieldName)
                {
                    int index = -1;
                    for (int i = 0; i < fields.Length; ++i)
                    {
                        if (fields[i].Equals(fieldName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            index = i;
                            break;
                        }
                    }
                    if (index == -1)
                    {
                        return null;
                    }
                    return index;
                }

                if (!GetIndex("ID", out int IdIndex)) continue;
                int? NameIndex = GetIndexOpt("Name");
                int? LevelIndex = GetIndexOpt("Level");
                int? TeamIndex = GetIndexOpt("Team");

                CultureInfo cultureInfo = new CultureInfo("en-US");
                int lineNumber = 1;
                while (stream.Peek() >= 0)
                {
                    ++lineNumber;

                    string line = stream.ReadLine();

                    try
                    {
                        string[] tokens = SplitCsvLine(line);

                        CustomEnemy enemy = new CustomEnemy();

                        int mobileID = int.Parse(tokens[IdIndex]);

                        enemy.Id = mobileID;

                        if (NameIndex.HasValue && !string.IsNullOrEmpty(tokens[NameIndex.Value]))
                        {
                            enemy.Name = tokens[NameIndex.Value];
                        }
                        
                        if(TeamIndex.HasValue && !string.IsNullOrEmpty(tokens[TeamIndex.Value]))
                        {
                            enemy.Team = (MobileTeams)Enum.Parse(typeof(MobileTeams), tokens[TeamIndex.Value]);
                        }

                        if(LevelIndex.HasValue && !string.IsNullOrEmpty(tokens[LevelIndex.Value]))
                        {
                            enemy.Level = int.Parse(tokens[LevelIndex.Value]);
                        }

                        customEnemyDb.Add(mobileID, enemy);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error while parsing {asset.name}:{lineNumber}: {ex}");
                    }
                }
            }
        }

        void LoadActiveFile()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(activeFile);
            if(asset == null)
            {
                activeFile = null;
                return;
            }

            TextReader stream = new StringReader(asset.text);

            List<EncounterTable> encounterTables = new List<EncounterTable>();

            string header = stream.ReadLine();

            string[] fields = header.Split(';', ',').Select(field => field.Trim('\"')).ToArray();

            if (fields.Length != 22)
            {
                Debug.LogError($"Error while parsing {asset.name}: table database has invalid format (expected 22 columns)");
                return;
            }

            CultureInfo cultureInfo = new CultureInfo("en-US");
            int lineNumber = 1;
            while (stream.Peek() >= 0)
            {
                ++lineNumber;

                string line = stream.ReadLine();
                try
                {
                    string[] tokens = SplitCsvLine(line);
                    if (tokens.Length != 22)
                    {
                        Debug.LogError($"Error while parsing {asset.name}:{lineNumber}: table database line has invalid format (expected 22 columns)");
                        break;
                    }

                    EncounterTable table = new EncounterTable();
                    table.Name = tokens[0];
                    table.TableIndices = ParseArrayArg(tokens[1], $"line={lineNumber}, column=2");
                    table.EnemyIds = tokens.Skip(2).Select(id => !string.IsNullOrEmpty(id) ? int.Parse(id) : -1).ToArray();

                    encounterTables.Add(table);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error while parsing {asset.name}:{lineNumber}: {ex}");
                }
            }

            activeFileEncounterTables = encounterTables.ToArray();
        }

        void RefreshFile()
        {
            int activeEncounterTableIndex = -1;
            if (!string.IsNullOrEmpty(activeEncounterTable))
                activeEncounterTableIndex = Array.FindIndex(activeFileEncounterTables, t => t.Name == activeEncounterTable);

            LoadActiveFile();
            LoadFileStatistics();

            if (activeEncounterTableIndex != -1)
            {
                activeEncounterTable = activeFileEncounterTables[activeEncounterTableIndex].Name;
                LoadTableStatistics();
            }
        }

        void LoadFileStatistics()
        {
            classicEnemyUseCount = new Dictionary<int, int>();
            customEnemyUseCount = new Dictionary<int, int>();

            foreach(var encounterTable in activeFileEncounterTables)
            {
                foreach(var enemyId in encounterTable.EnemyIds)
                {
                    if (enemyId == -1)
                        continue;

                    if(Enum.IsDefined(typeof(MobileTypes), enemyId))
                    {
                        if(classicEnemyUseCount.TryGetValue(enemyId, out int previousCount))
                        {
                            classicEnemyUseCount[enemyId] = previousCount + 1;
                        }
                        else
                        {
                            classicEnemyUseCount.Add(enemyId, 1);
                        }
                    }
                    else if (customEnemyDb.ContainsKey(enemyId))
                    {
                        if (customEnemyUseCount­.TryGetValue(enemyId, out int previousCount))
                        {
                            customEnemyUseCount[enemyId] = previousCount + 1;
                        }
                        else
                        {
                            customEnemyUseCount.Add(enemyId, 1);
                        }
                    }
                    else
                    {
                        Debug.LogError($"Invalid enemy '{enemyId}' in encounter table '{encounterTable.Name}'");
                    }
                }
            }
        }

        void LoadTableStatistics()
        {
            currentTableTeamCounts = new Dictionary<MobileTeams, int>();

            EncounterTable tableData = activeFileEncounterTables.First(table => table.Name == activeEncounterTable);

            foreach(int enemyId in tableData.EnemyIds)
            {
                if (enemyId == -1)
                    continue;

                if (Enum.IsDefined(typeof(MobileTypes), enemyId))
                {
                    MobileEnemy enemy = EnemyBasics.Enemies.First(e => e.ID == enemyId);
                    if(enemy.Team != MobileTeams.PlayerEnemy)
                    {
                        int count;
                        if (!currentTableTeamCounts.TryGetValue(enemy.Team, out count))
                        {
                            count = 0;
                            currentTableTeamCounts.Add(enemy.Team, count);
                        }

                        currentTableTeamCounts[enemy.Team] = count + 1;
                    }
                }
                else if(customEnemyDb.TryGetValue(enemyId, out CustomEnemy enemy))
                {
                    if (enemy.Team != MobileTeams.PlayerEnemy)
                    {
                        int count;
                        if (!currentTableTeamCounts.TryGetValue(enemy.Team, out count))
                        {
                            count = 0;
                            currentTableTeamCounts.Add(enemy.Team, count);
                        }

                        currentTableTeamCounts[enemy.Team] = count + 1;
                    }
                }
            }
        }
    }
}