﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class AfflictionHusk : Affliction
    {
        public enum InfectionState
        {
            Dormant, Transition, Active
        }

        private bool subscribedToDeathEvent;

        private InfectionState state;

        private List<Limb> huskAppendage;
        
        public InfectionState State
        {
            get { return state; }
        }

        public AfflictionHusk(AfflictionPrefab prefab, float strength) : 
            base(prefab, strength)
        {
        }

        public override void Update(CharacterHealth characterHealth, Limb targetLimb, float deltaTime)
        {
            float prevStrength = Strength;
            base.Update(characterHealth, targetLimb, deltaTime);

            if (!subscribedToDeathEvent)
            {
                characterHealth.Character.OnDeath += CharacterDead;
                subscribedToDeathEvent = true;
            }

            if (characterHealth.Character == Character.Controlled) UpdateMessages(prevStrength, characterHealth.Character);
            if (Strength < Prefab.MaxStrength * 0.5f)
            {
                UpdateDormantState(deltaTime, characterHealth.Character);
            }
            else if (Strength < Prefab.MaxStrength)
            {
                characterHealth.Character.SpeechImpediment = 100.0f;
                UpdateTransitionState(deltaTime, characterHealth.Character);
            }
            else
            {
                characterHealth.Character.SpeechImpediment = 100.0f;
                UpdateActiveState(deltaTime, characterHealth.Character);
            }
        }

        partial void UpdateMessages(float prevStrength, Character character);

        private void UpdateDormantState(float deltaTime, Character character)
        {
            if (state != InfectionState.Dormant)
            {
                DeactivateHusk(character);
            }
            
            state = InfectionState.Dormant;
        }

        private void UpdateTransitionState(float deltaTime, Character character)
        {
            if (state != InfectionState.Transition)
            {
                DeactivateHusk(character);                
            }

            state = InfectionState.Transition;
        }

        private void UpdateActiveState(float deltaTime, Character character)
        {
            if (state != InfectionState.Active)
            {
                ActivateHusk(character);
                state = InfectionState.Active;
            }

            foreach (Limb limb in character.AnimController.Limbs)
            {
                character.LastDamageSource = null;
                character.DamageLimb(
                    limb.WorldPosition, limb,
                    new List<Affliction>() { AfflictionPrefab.InternalDamage.Instantiate(0.5f * deltaTime / character.AnimController.Limbs.Length) },
                    0.0f, false, 0.0f);
            }
        }

        public void ActivateHusk(Character character)
        {
            if (huskAppendage == null)
            {
                huskAppendage = AttachHuskAppendage(character, Prefab.Identifier);
                if (huskAppendage != null)
                {
                    character.NeedsAir = false;
                    character.SetStun(0.5f);
                }
            }
        }

        public static List<Limb> AttachHuskAppendage(Character character, string afflictionIdentifier, Ragdoll ragdoll = null)
        {
            string infectedSpeciesName = character.SpeciesName.ToLowerInvariant();
            if (!infectedSpeciesName.Contains("husk"))
            {
                infectedSpeciesName += "husk";
            }
            string filePath = Character.GetConfigFile(infectedSpeciesName);
            var appendage = new List<Limb>();
            if (!Character.TryGetConfigFile(filePath, out XDocument huskDoc))
            {
                DebugConsole.ThrowError($"Error in '{filePath}': Failed to load the config file for the husk infected species with the species name '{infectedSpeciesName}'!");
                return appendage;
            }
            var mainElement = huskDoc.Root.IsOverride() ? huskDoc.Root.FirstElement() : huskDoc.Root;
            var element = mainElement.GetChildElements("huskappendage").FirstOrDefault(e => e.GetAttributeString("identifier", string.Empty).Equals(afflictionIdentifier));
            if (element == null)
            {
                DebugConsole.ThrowError($"Error in '{filePath}': Failed to find a huskappendage that matches the affliction with an identifier '{afflictionIdentifier}'!");
                return appendage;
            }
            string pathToAppendage = element.GetAttributeString("path", string.Empty);
            XDocument doc = XMLExtensions.TryLoadXml(pathToAppendage);
            if (doc == null) { return appendage; }
            if (ragdoll == null)
            {
                ragdoll = character.AnimController;
            }
            if (ragdoll.Dir < 1.0f)
            {
                ragdoll.Flip();
            }
            var limbElements = doc.Root.Elements("limb").ToDictionary(e => e.GetAttributeString("id", null), e => e);
            foreach (var jointElement in doc.Root.Elements("joint"))
            {
                if (limbElements.TryGetValue(jointElement.GetAttributeString("limb2", null), out XElement limbElement))
                {
                    JointParams jointParams = new JointParams(jointElement, ragdoll.RagdollParams);
                    Limb attachLimb = ragdoll.Limbs[jointParams.Limb1];
                    Limb huskAppendage = new Limb(ragdoll, character, new LimbParams(limbElement, ragdoll.RagdollParams));
                    huskAppendage.body.Submarine = character.Submarine;
                    huskAppendage.body.SetTransform(attachLimb.SimPosition, attachLimb.Rotation);
                    ragdoll.AddLimb(huskAppendage);
                    ragdoll.AddJoint(jointParams);
                    appendage.Add(huskAppendage);
                }
            }
            return appendage;
        }

        private void DeactivateHusk(Character character)
        {
            if (Character.TryGetConfigFile(character.ConfigPath, out XDocument configDoc))
            {
                var mainElement = configDoc.Root.IsOverride() ? configDoc.Root.FirstElement() : configDoc.Root;
                character.NeedsAir = mainElement.GetAttributeBool("needsair", false);
            }
            if (huskAppendage != null)
            {
                huskAppendage.ForEach(l => character.AnimController.RemoveLimb(l));
                huskAppendage = null;
            }
        }

        public void Remove(Character character)
        {
            DeactivateHusk(character);
            if (character != null) character.OnDeath -= CharacterDead;
            subscribedToDeathEvent = false;
        }

        private void CharacterDead(Character character, CauseOfDeath causeOfDeath)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }
            if (Strength < Prefab.MaxStrength * 0.5f || character.Removed) { return; }

            //don't turn the character into a husk if any of its limbs are severed
            if (character.AnimController?.LimbJoints != null)
            {
                foreach (var limbJoint in character.AnimController.LimbJoints)
                {
                    if (limbJoint.IsSevered) return;
                }
            }

            //create the AI husk in a coroutine to ensure that we don't modify the character list while enumerating it
            CoroutineManager.StartCoroutine(CreateAIHusk(character));
        }

        private IEnumerable<object> CreateAIHusk(Character character)
        {
            character.Enabled = false;
            Entity.Spawner.AddToRemoveQueue(character);

            string infectedSpeciesName = character.SpeciesName.ToLowerInvariant();
            if (!infectedSpeciesName.Contains("husk"))
            {
                infectedSpeciesName += "husk";
            }
            var configFile = Character.GetConfigFile(infectedSpeciesName);

            if (string.IsNullOrEmpty(configFile))
            {
                DebugConsole.ThrowError("Failed to turn character \"" + character.Name + "\" into a husk - husk config file not found.");
                yield return CoroutineStatus.Success;
            }

            var husk = Character.Create(configFile, character.WorldPosition, character.Info.Name, character.Info, isRemotePlayer: false, hasAi: true);

            foreach (Limb limb in husk.AnimController.Limbs)
            {
                if (limb.type == LimbType.None)
                {
                    limb.body.SetTransform(character.SimPosition, 0.0f);
                    continue;
                }

                var matchingLimb = character.AnimController.GetLimb(limb.type);
                if (matchingLimb?.body != null)
                {
                    limb.body.SetTransform(matchingLimb.SimPosition, matchingLimb.Rotation);
                    limb.body.LinearVelocity = matchingLimb.LinearVelocity;
                    limb.body.AngularVelocity = matchingLimb.body.AngularVelocity;
                }
            }

            if (character.Inventory.Items.Length != husk.Inventory.Items.Length)
            {
                string errorMsg = "Failed to move items from a human's inventory into a humanhusk's inventory (inventory sizes don't match)";
                DebugConsole.ThrowError(errorMsg);
                GameAnalyticsManager.AddErrorEventOnce("AfflictionHusk.CreateAIHusk:InventoryMismatch", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                yield return CoroutineStatus.Success;
            }

            for (int i = 0; i < character.Inventory.Items.Length && i < husk.Inventory.Items.Length; i++)
            {
                if (character.Inventory.Items[i] == null) continue;
                husk.Inventory.TryPutItem(character.Inventory.Items[i], i, true, false, null);
            }

            yield return CoroutineStatus.Success;
        }
    }
}
