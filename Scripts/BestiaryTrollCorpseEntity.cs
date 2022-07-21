using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DaggerfallBestiaryProject
{
    public class BestiaryTrollCorpseEntity : DaggerfallEntity
    {
        int enemyID = 0;

        public float RespawnTime = 5.0f;
        public float BillboardHeight = 1.0f;

        float respawnBuffer;
        bool entityStarted = false;

        public BestiaryTrollCorpseEntity(DaggerfallEntityBehaviour entityBehaviour, int mobileID)
            : base(entityBehaviour)
        {
            enemyID = mobileID;
        }

        public override void SetEntityDefaults()
        {
            MaxHealth = 1;
            CurrentHealth = 1;

            if(BestiaryMod.Instance.CustomEnemies.TryGetValue(enemyID, out BestiaryMod.CustomEnemy customEnemy))
            {
                if(BestiaryMod.Instance.CustomCareers.TryGetValue(customEnemy.career, out BestiaryMod.CustomCareer customCareer))
                {
                    career = customCareer.dfCareer;
                }
            }

            respawnBuffer = RespawnTime;
            entityStarted = true;
        }

        public override int SetHealth(int amount, bool restoreMode = false)
        {
            var currentHealth = base.SetHealth(amount, restoreMode);
            if(currentHealth <= 0)
            {
                // Show death message
                string deathMessage = TextManager.Instance.GetLocalizedText("thingJustDied");
                deathMessage = deathMessage.Replace("%s", TextManager.Instance.GetLocalizedEnemyName(enemyID));
                if (!DaggerfallUnity.Settings.DisableEnemyDeathAlert)
                    DaggerfallUI.Instance.PopupMessage(deathMessage);

                // Generate corpse
                GameObject corpse = GameObjectHelper.CreateDaggerfallBillboardGameObject(254, 47, GameObjectHelper.GetBestParent());
                corpse.transform.position = EntityBehaviour.transform.position + new Vector3(0.0f, -BillboardHeight / 2.0f + 0.1f, 0.0f);

                Object.Destroy(EntityBehaviour.transform.parent.gameObject);
            }

            return currentHealth;
        }

        public override void Update(DaggerfallEntityBehaviour sender)
        {
            if (!entityStarted)
                return;

            respawnBuffer -= Time.deltaTime;

            if(respawnBuffer <= 0.0f)
            {
                var enemyParent = GameObjectHelper.GetBestParent();
                var go = GameObjectHelper.CreateEnemy(
                    TextManager.Instance.GetLocalizedEnemyName(enemyID),
                    (MobileTypes)enemyID,
                    Vector3.zero,
                    MobileGender.Unspecified, enemyParent);
                go.transform.position = EntityBehaviour.transform.position;

                Object.Destroy(EntityBehaviour.transform.parent.gameObject);
            }
        }
    }
}
