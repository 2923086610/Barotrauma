﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class Traitor
    {
        public class GoalFindItem : HumanoidGoal
        {
            private readonly string identifier;
            private readonly bool preferNew;
            private readonly bool allowNew;
            private readonly bool allowExisting;
            private readonly HashSet<string> allowedContainerIdentifiers = new HashSet<string>();

            private ItemPrefab targetPrefab;
            private Item targetContainer;
            private Item target;
            private HashSet<Item> existingItems = new HashSet<Item>();
            private string targetNameText;
            private string targetContainerNameText;
            private string targetHullNameText;

            public override IEnumerable<string> InfoTextKeys => base.InfoTextKeys.Concat(new string[] { "[identifier]", "[target]", "[targethullname]" });
            public override IEnumerable<string> InfoTextValues(Traitor traitor) => base.InfoTextValues(traitor).Concat(new string[] { targetNameText ?? "", targetContainerNameText ?? "", targetHullNameText ?? "" });

            public override bool IsCompleted => target != null && Traitors.Any(traitor => traitor.Character.HasItem(target));
            public override bool CanBeCompleted
            {
                get
                {
                    if (!base.CanBeCompleted)
                    {
                        return false;
                    }
                    if (target == null)
                    {
                        return true;
                    }
                    if (target.Removed)
                    {
                        return false;
                    }
                    if (target.Submarine == null)
                    {
                        if (!(target.ParentInventory?.Owner is Character))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (Traitors.All(traitor => target.Submarine.TeamID != traitor.Character.TeamID))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            public override bool IsEnemy(Character character) => base.IsEnemy(character) || (target != null && target.FindParentInventory(inventory => inventory == character.Inventory) != null);

            protected ItemPrefab FindItemPrefab(string identifier)
            {
                return (ItemPrefab)MapEntityPrefab.List.Find(prefab => prefab is ItemPrefab && prefab.Identifier == identifier);
            }

            protected Item FindRandomContainer(bool includeNew, bool includeExisting)
            {
                List<Item> suitableItems = new List<Item>();
                foreach (Item item in Item.ItemList)
                {
                    if (item.Submarine == null || Traitors.All(traitor => item.Submarine.TeamID != traitor.Character.TeamID))
                    {
                        continue;
                    }
                    if (item.GetComponent<ItemContainer>() != null && allowedContainerIdentifiers.Contains(item.prefab.Identifier))
                    {
                        if ((includeNew && !item.OwnInventory.IsFull()) || (includeExisting && item.OwnInventory.FindItemByIdentifier(targetPrefab.Identifier) != null))
                        {
                            suitableItems.Add(item);
                        }
                    }
                }

                if (suitableItems.Count == 0) { return null; }
                return suitableItems[TraitorMission.Random(suitableItems.Count)];
            }

            public override bool Start(Traitor traitor)
            {
                if (!base.Start(traitor))
                {
                    return false;
                }
                if (targetPrefab != null)
                {
                    return true;
                }
                targetPrefab = FindItemPrefab(identifier);
                if (targetPrefab == null)
                {
                    return false;
                }
                var targetPrefabTextId = targetPrefab.GetItemNameTextId();
                targetNameText = targetPrefabTextId != null ? TextManager.FormatServerMessage(targetPrefabTextId) : targetPrefab.Name;
                targetContainer = null;
                if (preferNew)
                {
                    targetContainer = FindRandomContainer(true, false);
                }
                if (targetContainer == null)
                {
                    targetContainer = FindRandomContainer(allowNew, allowExisting);
                }
                if (targetContainer == null)
                {
                    return false;
                }
                var containerPrefabTextId = targetContainer.Prefab.GetItemNameTextId();
                targetContainerNameText = containerPrefabTextId != null ? TextManager.FormatServerMessage(containerPrefabTextId) : targetContainer.Prefab.Name;
                var targetHullTextId = targetContainer.CurrentHull != null ? targetContainer.CurrentHull.prefab.GetHullNameTextId() : null;
                targetHullNameText = targetHullTextId != null ? TextManager.FormatServerMessage(targetHullTextId) : targetContainer?.CurrentHull?.DisplayName ?? "";
                if (allowNew && !targetContainer.OwnInventory.IsFull())
                {
                    existingItems.Clear();
                    foreach (var item in targetContainer.OwnInventory.Items)
                    {
                        existingItems.Add(item);
                    }
                    Entity.Spawner.AddToSpawnQueue(targetPrefab, targetContainer.OwnInventory);
                    target = null;
                }
                else if (allowExisting)
                {
                    target = targetContainer.OwnInventory.FindItemByIdentifier(targetPrefab.Identifier);
                }
                return true;
            }

            public override void Update(float deltaTime)
            {
                base.Update(deltaTime);
                if (target == null)
                {
                    target = targetContainer.OwnInventory.Items.FirstOrDefault(item => item != null && item.Prefab.Identifier == identifier && !existingItems.Contains(item));
                    if (target != null)
                    {
                        existingItems.Clear();
                    }
                }
            }

            public GoalFindItem(string identifier, bool preferNew, bool allowNew, bool allowExisting, params string[] allowedContainerIdentifiers)
            {
                this.identifier = identifier;
                this.preferNew = preferNew;
                this.allowNew = allowNew;
                this.allowExisting = allowExisting;
                this.allowedContainerIdentifiers.UnionWith(allowedContainerIdentifiers);
            }
        }
    }
}
