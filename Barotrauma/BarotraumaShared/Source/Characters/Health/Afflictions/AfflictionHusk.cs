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
#if CLIENT
                character.AnimController.GetLimb(LimbType.Head).EnableHuskSprite = true;
#endif
            }
        }

        private void DeactivateHusk(Character character)
        {
            character.NeedsAir = character.Params.MainElement.GetAttributeBool("needsair", false);
            if (huskAppendage != null)
            {
                huskAppendage.ForEach(l => character.AnimController.RemoveLimb(l));
                huskAppendage = null;
#if CLIENT
                character.AnimController.GetLimb(LimbType.Head).EnableHuskSprite = false;
#endif
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

            string speciesName = GetHuskedSpeciesName(character.SpeciesName, Prefab as AfflictionPrefabHusk);
            CharacterPrefab prefab = CharacterPrefab.FindBySpeciesName(speciesName);

            if (prefab == null)
            {
                DebugConsole.ThrowError("Failed to turn character \"" + character.Name + "\" into a husk - husk config file not found.");
                yield return CoroutineStatus.Success;
            }

            var husk = Character.Create(speciesName, character.WorldPosition, character.Info.Name, character.Info, isRemotePlayer: false, hasAi: true, ragdoll: character.AnimController.RagdollParams);

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
                string errorMsg = "Failed to move items from the source character's inventory into a husk's inventory (inventory sizes don't match)";
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

        public static List<Limb> AttachHuskAppendage(Character character, string afflictionIdentifier, XElement appendageDefinition = null, Ragdoll ragdoll = null)
        {
            var appendage = new List<Limb>();
            if (!(AfflictionPrefab.List.FirstOrDefault(ap => ap.Identifier == afflictionIdentifier) is AfflictionPrefabHusk matchingAffliction))
            {
                DebugConsole.ThrowError($"Could not find an affliction of type 'huskinfection' that matches the affliction '{afflictionIdentifier}'!");
                return appendage;
            }
            string nonhuskedSpeciesName = GetNonHuskedSpeciesName(character.SpeciesName, matchingAffliction);
            string huskedSpeciesName = GetHuskedSpeciesName(nonhuskedSpeciesName, matchingAffliction);
            CharacterPrefab huskPrefab = CharacterPrefab.FindBySpeciesName(huskedSpeciesName);
            if (huskPrefab?.XDocument == null)
            {
                DebugConsole.ThrowError($"Failed to find the config file for the husk infected species with the species name '{huskedSpeciesName}'!");
                return appendage;
            }
            var mainElement = huskPrefab.XDocument.Root.IsOverride() ? huskPrefab.XDocument.Root.FirstElement() : huskPrefab.XDocument.Root;
            var element = appendageDefinition;
            if (element == null)
            {
                element = mainElement.GetChildElements("huskappendage").FirstOrDefault(e => e.GetAttributeString("affliction", string.Empty).Equals(afflictionIdentifier));
            }
            if (element == null)
            {
                DebugConsole.ThrowError($"Error in '{huskPrefab.FilePath}': Failed to find a huskappendage that matches the affliction with an identifier '{afflictionIdentifier}'!");
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
                    var jointParams = new RagdollParams.JointParams(jointElement, ragdoll.RagdollParams);
                    Limb attachLimb = null;
                    if (matchingAffliction.AttachLimbId > -1)
                    {
                        attachLimb = ragdoll.Limbs.FirstOrDefault(l => l.Params.ID == matchingAffliction.AttachLimbId);
                    }
                    else if (matchingAffliction.AttachLimbName != null)
                    {
                        attachLimb = ragdoll.Limbs.FirstOrDefault(l => l.Name == matchingAffliction.AttachLimbName);
                    }
                    else if (matchingAffliction.AttachLimbType != LimbType.None)
                    {
                        attachLimb = ragdoll.Limbs.FirstOrDefault(l => l.type == matchingAffliction.AttachLimbType);
                    }
                    if (attachLimb == null)
                    {
                        DebugConsole.Log("Attachment limb not defined in the affliction prefab or no matching limb could be found. Using the appendage definition as it is.");
                        attachLimb = ragdoll.Limbs.FirstOrDefault(l => l.Params.ID == jointParams.Limb1);
                    }
                    if (attachLimb != null)
                    {
                        jointParams.Limb1 = attachLimb.Params.ID;
                        var appendageLimbParams = new RagdollParams.LimbParams(limbElement, ragdoll.RagdollParams)
                        {
                            // Ensure that we have a valid id for the new limb
                            ID = ragdoll.Limbs.Length
                        };
                        jointParams.Limb2 = appendageLimbParams.ID;
                        Limb huskAppendage = new Limb(ragdoll, character, appendageLimbParams);
                        huskAppendage.body.Submarine = character.Submarine;
                        huskAppendage.body.SetTransform(attachLimb.SimPosition, attachLimb.Rotation);
                        ragdoll.AddLimb(huskAppendage);
                        ragdoll.AddJoint(jointParams);
                        appendage.Add(huskAppendage);
                    }
                    else
                    {
                        DebugConsole.ThrowError("Attachment limb not found!");
                    }
                }
            }
            return appendage;
        }

        public static string GetHuskedSpeciesName(string speciesName, AfflictionPrefabHusk prefab)
        {
            return prefab.HuskedSpeciesName.Replace(AfflictionPrefabHusk.Tag, speciesName);
        }

        public static string GetNonHuskedSpeciesName(string huskedSpeciesName, AfflictionPrefabHusk prefab)
        {
            string nonTag = prefab.HuskedSpeciesName.Remove(AfflictionPrefabHusk.Tag);
            return huskedSpeciesName.ToLowerInvariant().Remove(nonTag);
        }
    }
}
