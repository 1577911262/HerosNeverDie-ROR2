using System;
using System.Linq;
using UnityEngine.Networking;
using BepInEx;
using RoR2;
using UnityEngine;
using RoR2.CharacterAI;
using UnityEngine.Events;
using System.Reflection;
using System.Collections.Generic;

namespace HerosNeverDie
{
    [BepInPlugin("HerosNeverDie", "Respawn", "0.1")]
    public class Respawn : BaseUnityPlugin
    {
        private Vector3 deathFootPosition;
        private CharacterMaster characterMaster;
        private readonly int[] itemStacks = new int[78];
        private readonly List<ItemIndex> list = new List<ItemIndex>();
        private readonly Dictionary<string, int> userPlayerRespawnNum = new Dictionary<string, int>();
        private readonly int RemoveItemNum = 3;
        private readonly int RespawnNum = 3;
        //private bool preventRespawnUntilNextStageServer;
        public Respawn()
        {

            On.RoR2.CharacterMaster.OnBodyDeath += CharacterMaster_OnBodyDeath;
        }

        private void CharacterMaster_OnBodyDeath(On.RoR2.CharacterMaster.orig_OnBodyDeath orig, CharacterMaster self)
        {
            characterMaster = self;
            if (NetworkServer.active)
            {
                deathFootPosition = self.GetBody().footPosition;
                BaseAI[] components = self.GetComponents<BaseAI>();
                for (int i = 0; i < components.Length; i++)
                {
                    components[i].OnBodyDeath();
                }
                PlayerCharacterMasterController component = self.GetComponent<PlayerCharacterMasterController>();
                if (component)
                {
                    component.OnBodyDeath();
                }
                int itemAllNum = 0;
                list.Clear();
                if (self.teamIndex == TeamIndex.Player)
                {
                    for (int i = 0; i < Enum.GetValues(typeof(ItemIndex)).Length; i++)
                    {
                        ItemIndex itemIndex = (ItemIndex)i;
                        if (itemIndex <= ItemIndex.None || itemIndex >= ItemIndex.Count)
                        {
                            continue;
                        }

                        int num = self.inventory.GetItemCount(itemIndex);
                        if (num > 0)
                        {
                            list.Add(itemIndex);
                        }

                        itemAllNum += num;

                        itemStacks[i] = num;
                    }
                }
                string usename = Util.GetBestMasterName(self);
                bool tempHerosNeverDie = itemAllNum >= RemoveItemNum;
                if (tempHerosNeverDie)
                {
                    
                    int tempNum = 0;
                    if (userPlayerRespawnNum.ContainsKey(usename) == false)
                        userPlayerRespawnNum[usename] = 0;
                    else
                        tempNum = userPlayerRespawnNum[usename];
                    if (tempNum >= RespawnNum)
                    {
                        tempHerosNeverDie = false;
                    }
                }
               
                if (self.inventory.GetItemCount(ItemIndex.ExtraLife) > 0)
                {
                    self.inventory.RemoveItem(ItemIndex.ExtraLife, 1);
                    self.Invoke("RespawnExtraLife", 2f);
                    self.Invoke("PlayExtraLifeSFX", 1f);
                }
                else if (tempHerosNeverDie)
                {
                    for (int i = 0; i < RemoveItemNum; i++)
                    {
                        RandomRemoveItemOne();
                    }
                    userPlayerRespawnNum[usename] += 1;

                    this.Invoke("RespawnExtraLife", 2f);
                    self.Invoke("PlayExtraLifeSFX", 1f);
                }
                else
                {
                    if (self.destroyOnBodyDeath)
                    {
                        UnityEngine.Object.Destroy(self.gameObject);
                    }
                    self.preventGameOver = false;
                    FieldInfo field = self.GetType().GetField("preventRespawnUntilNextStageServer", BindingFlags.NonPublic | BindingFlags.Instance);
                    field.SetValue(self, true);
                }
                MethodInfo methodInfo = self.GetType().GetMethod("ResetLifeStopwatch", BindingFlags.NonPublic | BindingFlags.Instance);
                methodInfo.Invoke(self, null);
            }
            UnityEvent unityEvent = self.onBodyDeath;
            if (unityEvent == null)
            {
                return;
            }
            unityEvent.Invoke();
        }

        public void RespawnExtraLife()
        {
            Chat.AddMessage("Heros Never Die");
            characterMaster.Respawn(this.deathFootPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), false);
            characterMaster.GetBody().AddTimedBuff(BuffIndex.Immune, 3f);
            GameObject gameObject = Resources.Load<GameObject>("Prefabs/Effects/HippoRezEffect");
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
            var prop_bodyInstanceObject = characterMaster.GetType().GetProperty("bodyInstanceObject", flags);
            GameObject bodyInstanceObject = prop_bodyInstanceObject.GetValue(characterMaster) as GameObject;
            if (bodyInstanceObject != null)
            {
                foreach (EntityStateMachine entityStateMachine in bodyInstanceObject.GetComponents<EntityStateMachine>())
                {
                    entityStateMachine.initialStateType = entityStateMachine.mainStateType;
                }
                if (gameObject)
                {
                    EffectManager.instance.SpawnEffect(gameObject, new EffectData
                    {
                        origin = this.deathFootPosition,
                        rotation = bodyInstanceObject.transform.rotation
                    }, true);
                }
            }
        }

        private void RandomRemoveItemOne()
        {
            List<ItemIndex> list = new List<ItemIndex>();
            for (int i = 0; i < itemStacks.Length; i++)
            {
                int num = itemStacks[i];
                if (num > 0)
                {
                    list.Add((ItemIndex)i);
                }
            }
            ItemIndex itemIndex = (list.Count == 0) ? ItemIndex.None : list[GetRandom(0, list.Count)];
            characterMaster.inventory.RemoveItem(itemIndex, 1);
            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            Chat.AddMessage("Remove One Item:" + Language.GetString(itemDef.nameToken));
            itemStacks[(int)itemIndex] -= 1;
        }

        public int GetRandom(int varStart, int varEnd)
        {
            System.Random rd = new System.Random();
            return rd.Next(varStart, varEnd);
        }
    }
}
