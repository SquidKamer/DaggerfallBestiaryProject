using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility;
using FullSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DaggerfallBestiaryProject
{    
    [fsObject("v1")]
    struct BestiarySaveData_v1
    {
        public BestiaryTrollCorpseData_v1[] ActiveCorpses;
    }

    public class BestiarySaveInterface : MonoBehaviour, IHasModSaveData
    {
        Dictionary<ulong, BestiaryTrollCorpseSerializer> activeCorpseSerializers = new Dictionary<ulong, BestiaryTrollCorpseSerializer>();

        #region Unity
        void OnEnable()
        {
            SaveLoadManager.OnStartLoad += SaveLoadManager_OnStartLoad;
        }
                
        void OnDisable()
        {
            SaveLoadManager.OnStartLoad -= SaveLoadManager_OnStartLoad;
        }
        #endregion

        private void SaveLoadManager_OnStartLoad(SaveData_v1 saveData)
        {

        }

        public Type SaveDataType { get { return typeof(BestiarySaveData_v1); } }

        public object NewSaveData()
        {
            BestiarySaveData_v1 data = new BestiarySaveData_v1();
            data.ActiveCorpses = new BestiaryTrollCorpseData_v1[0] { };
            return data;
        }

        public object GetSaveData()
        {
            BestiarySaveData_v1 data = new BestiarySaveData_v1();
            data.ActiveCorpses = activeCorpseSerializers.Values.Select(serializer => (BestiaryTrollCorpseData_v1)serializer.GetSaveData()).ToArray();
            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            BestiarySaveData_v1 data = (BestiarySaveData_v1)saveData;

            foreach(BestiaryTrollCorpseData_v1 corpseData in data.ActiveCorpses)
            {
                if (corpseData.RespawnBuffer <= 0.0f)
                    continue;

                if(!activeCorpseSerializers.TryGetValue(corpseData.LoadID, out BestiaryTrollCorpseSerializer serializer))
                {
                    DaggerfallLoot loot = GameObjectHelper.CreateDroppedLootContainer(GameManager.Instance.PlayerObject, corpseData.LoadID, corpseData.TextureArchive, corpseData.TextureRecord);
                    serializer = loot.GetComponentInChildren<BestiaryTrollCorpseSerializer>();
                    var corpseBillboard = loot.GetComponentInChildren<BestiaryTrollCorpseBillboard>();
                    if (corpseBillboard != null)
                        corpseBillboard.PostParentedSetup(); // Ensures the BestiaryTrollCorpseEntity exists before we RestoreSaveData
                }

                if (serializer == null)
                    continue;

                serializer.RestoreSaveData(corpseData);
            }
        }

        public void RegisterActiveSerializer(BestiaryTrollCorpseSerializer serializer)
        {
            activeCorpseSerializers.Add(serializer.LoadID, serializer);
        }

        public void DeregisterActiveSerialier(BestiaryTrollCorpseSerializer serializer)
        {
            activeCorpseSerializers.Remove(serializer.LoadID);
        }
    }
}
