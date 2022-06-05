using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallConnect;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Globalization;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using System.Linq;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Questing;

namespace DaggerfallBestiaryProject
{
    public class BestiaryMod : MonoBehaviour
    {
        private static Mod mod;

        public static BestiaryMod Instance { get; private set; }

        public struct CustomCareer
        {
            public DFCareer dfCareer;
        }

        private Dictionary<string, CustomCareer> customCareers = new Dictionary<string, CustomCareer>();

        public struct CustomEnemy
        {
            public MobileEnemy mobileEnemy;
            public string name;
            public string career;
        }

        private Dictionary<int, CustomEnemy> customEnemies = new Dictionary<int, CustomEnemy>();

        public Dictionary<int, CustomEnemy> CustomEnemies { get { return customEnemies; } }

        // Returns true if the id is a monster id. False if it's a career id
        // The id doesn't have to refer to an actual enemy
        public static bool IsMonster(int enemyId)
        {
            // Ids 0 to 127 are monsters
            // 128 to 255 is classes
            // and these two alternate every 128 ids
            return ((enemyId / 128) % 2) == 0;
        }


        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            Instance = go.AddComponent<BestiaryMod>();

            mod.IsReady = true;
        }

        private void Start()
        {            
            ParseCustomCareers();
            ParseCustomEnemies();
        }

        IEnumerable<TextAsset> GetDBAssets(string extension)
        {
            HashSet<string> names = new HashSet<string>();
            foreach(Mod mod in ModManager.Instance.EnumerateEnabledModsReverse())
            {
                foreach(string file in mod.ModInfo.Files.Where(filePath => filePath.EndsWith(extension)).Select(filePath => Path.GetFileName(filePath)))
                {
                    names.Add(file);
                }
            }

            foreach(string name in names)
            {
                ModManager.Instance.TryGetAsset(name, clone: false, out TextAsset asset);
                yield return asset;
            }
        }

        void ParseCustomCareers()
        {
            foreach (TextAsset asset in GetDBAssets(".cdb.csv"))
            {
                var stream = new StreamReader(new MemoryStream(asset.bytes));

                string header = stream.ReadLine();

                string[] fields = header.Split(';', ',');

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
                        Debug.LogError($"Career DB file '{asset.name}': could not find field '{fieldName}' in header");
                        return false;
                    }
                    return true;
                }

                if (!GetIndex("Name", out int NameIndex)) continue;
                if (!GetIndex("HitPointsPerLevel", out int HPIndex)) continue;
                if (!GetIndex("Strength", out int StrengthIndex)) continue;
                if (!GetIndex("Intelligence", out int IntelligenceIndex)) continue;
                if (!GetIndex("Willpower", out int WillpowerIndex)) continue;
                if (!GetIndex("Agility", out int AgilityIndex)) continue;
                if (!GetIndex("Endurance", out int EnduranceIndex)) continue;
                if (!GetIndex("Personality", out int PersonalityIndex)) continue;
                if (!GetIndex("Speed", out int SpeedIndex)) continue;
                if (!GetIndex("Luck", out int LuckIndex)) continue;

                CultureInfo cultureInfo = new CultureInfo("en-US");
                int lineNumber = 1;
                while (stream.Peek() >= 0)
                {
                    ++lineNumber;

                    string line = stream.ReadLine();

                    try
                    {
                        string[] tokens = SplitCsvLine(line);

                        DFCareer career = new DFCareer();

                        career.Name = tokens[NameIndex];

                        // Skip careers we've already assigned
                        // FindAssets traverses mods from high to low priority, so the first mod in this list wins
                        if (customCareers.ContainsKey(career.Name))
                            continue;

                        career.HitPointsPerLevel = int.Parse(tokens[HPIndex], cultureInfo);
                        career.Strength = int.Parse(tokens[StrengthIndex], cultureInfo);
                        career.Intelligence = int.Parse(tokens[IntelligenceIndex], cultureInfo);
                        career.Willpower = int.Parse(tokens[WillpowerIndex], cultureInfo);
                        career.Agility = int.Parse(tokens[AgilityIndex], cultureInfo);
                        career.Endurance = int.Parse(tokens[EnduranceIndex], cultureInfo);
                        career.Personality = int.Parse(tokens[PersonalityIndex], cultureInfo);
                        career.Speed = int.Parse(tokens[SpeedIndex], cultureInfo);
                        career.Luck = int.Parse(tokens[LuckIndex], cultureInfo);

                        CustomCareer customCareer = new CustomCareer();
                        customCareer.dfCareer = career;

                        customCareers.Add(career.Name, customCareer);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error while parsing {asset.name}:{lineNumber}: {ex}");
                    }
                }
            }
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

        int[] ParseAnimArg(string Arg, string Context)
        {
            if (string.IsNullOrEmpty(Arg))
                return Array.Empty<int>();

            // Strip brackets
            if(Arg[0] == '[' || Arg[0] == '{')
            {
                // Check for end bracket
                if(Arg[0] == '[' && Arg[Arg.Length - 1] != ']'
                    || Arg[0] == '{' && Arg[Arg.Length - 1] != '}')
                    throw new InvalidDataException($"Error parsing ({Context}): array argument has mismatched brackets");

                Arg = Arg.Substring(1, Arg.Length - 2);
            }

            string[] Frames = Arg.Split(',', ';');
            return Frames.Select(Frame => string.IsNullOrEmpty(Frame) ? "-1" : Frame).Select(int.Parse).ToArray();
        }

        bool ParseBool(string Value, string Context)
        {
            if (string.IsNullOrEmpty(Value))
                return false;

            if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            throw new InvalidDataException($"Error parsing ({Context}): invalid boolean value '{Value}'");
        }

        void ParseCustomEnemies()
        {
            List<MobileEnemy> enemies = EnemyBasics.Enemies.ToList();
            List<string> questEnemyLines = new List<string>();

            foreach (TextAsset asset in GetDBAssets(".mdb.csv"))
            {
                var stream = new StreamReader(new MemoryStream(asset.bytes));

                string header = stream.ReadLine();

                string[] fields = header.Split(';', ',');

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
                if (!GetIndex("Name", out int NameIndex)) continue;
                if (!GetIndex("Career", out int CareerIndex)) continue;
                if (!GetIndex("MaleTexture", out int MaleTextureIndex)) continue;
                if (!GetIndex("FemaleTexture", out int FemaleTextureIndex)) continue;
                if (!GetIndex("CorpseTextureArchive", out int CorpseTextureArchiveIndex)) continue;
                if (!GetIndex("CorpseTextureRecord", out int CorpseTextureRecordIndex)) continue;
                if (!GetIndex("HasIdle", out int HasIdleIndex)) continue;
                if (!GetIndex("CastsMagic", out int CastsMagicIndex)) continue;
                if (!GetIndex("HasRangedAttack", out int HasRangedAttackIndex)) continue;
                if (!GetIndex("PrimaryAttackAnimFrames", out int PrimaryAttackAnimFramesIndex)) continue;

                int? LevelIndex = GetIndexOpt("Level");

                CultureInfo cultureInfo = new CultureInfo("en-US");
                int lineNumber = 1;
                while (stream.Peek() >= 0)
                {
                    ++lineNumber;

                    string line = stream.ReadLine();

                    try
                    {
                        string[] tokens = SplitCsvLine(line);

                        MobileEnemy mobile = new MobileEnemy();

                        mobile.ID = int.Parse(tokens[IdIndex]);
                        mobile.MaleTexture = int.Parse(tokens[MaleTextureIndex]);
                        mobile.FemaleTexture = int.Parse(tokens[FemaleTextureIndex]);
                        int CorpseArchive = int.Parse(tokens[CorpseTextureArchiveIndex]);
                        int CorpseRecord = int.Parse(tokens[CorpseTextureRecordIndex]);
                        mobile.CorpseTexture = EnemyBasics.CorpseTexture(CorpseArchive, CorpseRecord);
                        mobile.HasIdle = ParseBool(tokens[HasIdleIndex], $"line={lineNumber}, column={HasIdleIndex}");
                        mobile.CastsMagic = ParseBool(tokens[CastsMagicIndex], $"line={lineNumber}, column={CastsMagicIndex}");
                        mobile.HasRangedAttack1 = ParseBool(tokens[HasRangedAttackIndex], $"line={lineNumber}, column={HasRangedAttackIndex}");

                        if(mobile.HasRangedAttack1)
                        {
                            mobile.RangedAttackAnimFrames = new int[] { 3, 2, 0, 0, 0, -1, 1, 1, 2, 3 };
                        }

                        mobile.PrimaryAttackAnimFrames = ParseAnimArg(tokens[PrimaryAttackAnimFramesIndex], $"line={lineNumber}, column={PrimaryAttackAnimFramesIndex}");

                        if(mobile.CastsMagic)
                        {
                            if(mobile.HasRangedAttack1)
                            {
                                // We have both ranged and casting
                                mobile.HasRangedAttack2 = true;
                            }
                            else
                            {
                                // Casting is our only ranged attack
                                mobile.HasRangedAttack1 = true;
                            }
                            mobile.SpellAnimFrames = new int[] { 0, 1, 2, 3, 3 };
                        }

                        if(LevelIndex.HasValue && !string.IsNullOrEmpty(tokens[LevelIndex.Value]))
                        {
                            mobile.Level = int.Parse(tokens[LevelIndex.Value]);
                        }
                        else if(IsMonster(mobile.ID))
                        {
                            Debug.LogWarning($"Monster '{mobile.ID}' did not have a level specified. Defaulting to 1");
                            mobile.Level = 1;
                        }

                        if (customEnemies.ContainsKey(mobile.ID))
                            continue;

                        CustomEnemy customEnemy = new CustomEnemy();
                        customEnemy.mobileEnemy = mobile;
                        customEnemy.name = tokens[NameIndex];
                        customEnemy.career = tokens[CareerIndex];

                        if(!customCareers.TryGetValue(customEnemy.career, out CustomCareer customCareer))
                        {
                            Debug.LogError($"Monster '{mobile.ID}' has unknown career '{customEnemy.career}'");
                            continue;
                        }

                        customEnemies.Add(mobile.ID, customEnemy);

                        enemies.Add(mobile);
                        DaggerfallEntity.RegisterCustomCareerTemplate(mobile.ID, customCareer.dfCareer);
                        questEnemyLines.Add($"{mobile.ID}, {customEnemy.name.Replace(' ', '_')}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error while parsing {asset.name}:{lineNumber}: {ex}");
                    }
                }
            }

            EnemyBasics.Enemies = enemies.ToArray();
            QuestMachine.Instance.FoesTable.AddIntoTable(questEnemyLines.ToArray());
        }
    }
}
