﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using System.Linq;

namespace Barotrauma
{
    public static class CPRSettings
    {
        public static bool IsLoaded { get; private set; }
        public static float ReviveChancePerSkill { get; private set; }
        public static float ReviveChanceExponent { get; private set; }
        public static float ReviveChanceMin { get; private set; }
        public static float ReviveChanceMax { get; private set; }
        public static float StabilizationPerSkill { get; private set; }
        public static float StabilizationMin { get; private set; }
        public static float StabilizationMax { get; private set; }
        public static float DamageSkillThreshold { get; private set; }
        public static float DamageSkillMultiplier { get; private set; }

        public static void Load(XElement element)
        {
            ReviveChancePerSkill = Math.Max(element.GetAttributeFloat("revivechanceperskill", 0.01f), 0.0f);
            ReviveChanceExponent = Math.Max(element.GetAttributeFloat("revivechanceexponent", 2.0f), 0.0f);
            ReviveChanceMin = MathHelper.Clamp(element.GetAttributeFloat("revivechancemin", 0.05f), 0.0f, 1.0f);
            ReviveChanceMax = MathHelper.Clamp(element.GetAttributeFloat("revivechancemax", 0.9f), ReviveChanceMin, 1.0f);

            StabilizationPerSkill = Math.Max(element.GetAttributeFloat("stabilizationperskill", 0.01f), 0.0f);
            StabilizationMin = MathHelper.Max(element.GetAttributeFloat("stabilizationmin", 0.05f), 0.0f);
            StabilizationMax = MathHelper.Max(element.GetAttributeFloat("stabilizationmax", 2.0f), StabilizationMin);

            DamageSkillThreshold = MathHelper.Clamp(element.GetAttributeFloat("damageskillthreshold", 40.0f), 0.0f, 100.0f);
            DamageSkillMultiplier = MathHelper.Clamp(element.GetAttributeFloat("damageskillmultiplier", 0.1f), 0.0f, 100.0f);
            IsLoaded = true;
        }
    }

    class AfflictionPrefab
    {
        public class Effect
        {
            //this effect is applied when the strength is within this range
            public float MinStrength, MaxStrength;

            public readonly float MinVitalityDecrease = 0.0f;
            public readonly float MaxVitalityDecrease = 0.0f;
            
            //how much the strength of the affliction changes per second
            public readonly float StrengthChange = 0.0f;

            public readonly bool MultiplyByMaxVitality;

            public float MinScreenBlurStrength, MaxScreenBlurStrength;
            public float MinScreenDistortStrength, MaxScreenDistortStrength;
            public float MinRadialDistortStrength, MaxRadialDistortStrength;
            public float MinChromaticAberrationStrength, MaxChromaticAberrationStrength;
            public float MinSpeedMultiplier, MaxSpeedMultiplier;
            public float MinBuffMultiplier, MaxBuffMultiplier;

            public float MinResistance, MaxResistance;
            public string ResistanceFor;
            public string DialogFlag;

            //statuseffects applied on the character when the affliction is active
            public readonly List<StatusEffect> StatusEffects = new List<StatusEffect>();

            public Effect(XElement element, string parentDebugName)
            {
                MinStrength =  element.GetAttributeFloat("minstrength", 0);
                MaxStrength =  element.GetAttributeFloat("maxstrength", 0);

                MultiplyByMaxVitality = element.GetAttributeBool("multiplybymaxvitality", false);

                MinVitalityDecrease = element.GetAttributeFloat("minvitalitydecrease", 0.0f);
                MaxVitalityDecrease = element.GetAttributeFloat("maxvitalitydecrease", 0.0f);
                MaxVitalityDecrease = Math.Max(MinVitalityDecrease, MaxVitalityDecrease);

                MinScreenDistortStrength = element.GetAttributeFloat("minscreendistort", 0.0f);
                MaxScreenDistortStrength = element.GetAttributeFloat("maxscreendistort", 0.0f);
                MaxScreenDistortStrength = Math.Max(MinScreenDistortStrength, MaxScreenDistortStrength);

                MinRadialDistortStrength = element.GetAttributeFloat("minradialdistort", 0.0f);
                MaxRadialDistortStrength = element.GetAttributeFloat("maxradialdistort", 0.0f);
                MaxRadialDistortStrength = Math.Max(MinRadialDistortStrength, MaxRadialDistortStrength);

                MinChromaticAberrationStrength = element.GetAttributeFloat("minchromaticaberration", 0.0f);
                MaxChromaticAberrationStrength = element.GetAttributeFloat("maxchromaticaberration", 0.0f);
                MaxChromaticAberrationStrength = Math.Max(MinChromaticAberrationStrength, MaxChromaticAberrationStrength);

                MinScreenBlurStrength = element.GetAttributeFloat("minscreenblur", 0.0f);
                MaxScreenBlurStrength = element.GetAttributeFloat("maxscreenblur", 0.0f);
                MaxScreenBlurStrength = Math.Max(MinScreenBlurStrength, MaxScreenBlurStrength);

                ResistanceFor = element.GetAttributeString("resistancefor", "");
                MinResistance = element.GetAttributeFloat("minresistance", 0.0f);
                MaxResistance = element.GetAttributeFloat("maxresistance", 0.0f);
                MaxResistance = Math.Max(MinResistance, MaxResistance);

                MinSpeedMultiplier = element.GetAttributeFloat("minspeedmultiplier", 1.0f);
                MaxSpeedMultiplier = element.GetAttributeFloat("maxspeedmultiplier", 1.0f);
                MaxSpeedMultiplier = Math.Max(MinSpeedMultiplier, MaxSpeedMultiplier);

                MinBuffMultiplier = element.GetAttributeFloat("minmultiplier", 1.0f);
                MaxBuffMultiplier = element.GetAttributeFloat("maxmultiplier", 1.0f);
                MaxBuffMultiplier = Math.Max(MinBuffMultiplier, MaxBuffMultiplier);

                DialogFlag = element.GetAttributeString("dialogflag", "");

                StrengthChange = element.GetAttributeFloat("strengthchange", 0.0f);

                foreach (XElement subElement in element.Elements())
                {
                    switch (subElement.Name.ToString().ToLowerInvariant())
                    {
                        case "statuseffect":
                            StatusEffects.Add(StatusEffect.Load(subElement, parentDebugName));
                            break;
                    }
                }
            }
        }

        public static AfflictionPrefab InternalDamage;
        public static AfflictionPrefab Bleeding;
        public static AfflictionPrefab Burn;
        public static AfflictionPrefab OxygenLow;
        public static AfflictionPrefab Bloodloss;
        public static AfflictionPrefab Pressure;
        public static AfflictionPrefab Stun;
        public static AfflictionPrefab Husk;

        public static List<AfflictionPrefab> List = new List<AfflictionPrefab>();

        //Arbitrary string that is used to identify the type of the affliction.
        //Afflictions with the same type stack up, and items may be configured to cure specific types of afflictions.
        public readonly string AfflictionType;

        //Does the affliction affect a specific limb or the whole character
        public readonly bool LimbSpecific;

        //If not a limb-specific affliction, which limb is the indicator shown on in the health menu
        //(e.g. mental health problems on head, lack of oxygen on torso...)
        public readonly LimbType IndicatorLimb;

        public readonly string Identifier;

        public readonly string Name, Description;
        public readonly bool IsBuff;

        public readonly string CauseOfDeathDescription, SelfCauseOfDeathDescription;

        //how high the strength has to be for the affliction to take affect
        public readonly float ActivationThreshold = 0.0f;
        //how high the strength has to be for the affliction icon to be shown in the UI
        public readonly float ShowIconThreshold = 0.05f;
        public readonly float MaxStrength = 100.0f;

        //how high the strength has to be for the affliction icon to be shown with a health scanner
        public readonly float ShowInHealthScannerThreshold = 0.05f;

        //how much karma changes when a player applies this affliction to someone (per strength of the affliction)
        public float KarmaChangeOnApplied;

        public float BurnOverlayAlpha;
        public float DamageOverlayAlpha;

        //steam achievement given when the affliction is removed from the controlled character
        public readonly string AchievementOnRemoved;

        public readonly Sprite Icon;
        public readonly Color IconColor;

        private List<Effect> effects = new List<Effect>();

        private Dictionary<string, float> treatmentSuitability = new Dictionary<string, float>();

        private readonly string typeName;

        private readonly ConstructorInfo constructor;

        public Dictionary<string, float> TreatmentSuitability
        {
            get { return treatmentSuitability; }
        }

        public static void LoadAll(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths)
            {
                XDocument doc = XMLExtensions.TryLoadXml(filePath);
                if (doc == null || doc.Root == null) continue;
                var mainElement = doc.Root.IsOverride() ? doc.Root.FirstElement() : doc.Root;
                if (doc.Root.IsOverride())
                {
                    DebugConsole.ThrowError("Cannot override all afflictions, because many of them are required by the main game! Please try overriding them one by one.");
                }
                foreach (XElement element in mainElement.Elements())
                {
                    bool isOverride = element.IsOverride();
                    XElement sourceElement = isOverride ? element.FirstElement() : element;
                    string elementName = sourceElement.Name.ToString().ToLowerInvariant();
                    string identifier = sourceElement.GetAttributeString("identifier", null);
                    if (!elementName.Equals("cprsettings", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(identifier))
                        {
                            DebugConsole.ThrowError($"No identifier defined for the affliction '{elementName}' in file '{filePath}'");
                            continue;
                        }
                        var duplicate = List.FirstOrDefault(a => a.Identifier == identifier);
                        if (duplicate != null)
                        {
                            if (isOverride)
                            {
                                DebugConsole.NewMessage($"Overriding an affliction or a buff with the identifier '{identifier}' using the file '{filePath}'", Color.Yellow);
                                List.Remove(duplicate);
                            }
                            else
                            {
                                DebugConsole.ThrowError($"Duplicate affliction: '{identifier}' defined in {elementName} of '{filePath}'");
                                continue;
                            }
                        }
                    }
                    switch (elementName)
                    {
                        case "internaldamage":
                            List.Add(InternalDamage = new AfflictionPrefab(sourceElement, typeof(Affliction)));
                            break;
                        case "bleeding":
                            List.Add(Bleeding = new AfflictionPrefab(sourceElement, typeof(AfflictionBleeding)));
                            break;
                        case "burn":
                            List.Add(Burn = new AfflictionPrefab(sourceElement, typeof(Affliction)));
                            break;
                        case "oxygenlow":
                            List.Add(OxygenLow = new AfflictionPrefab(sourceElement, typeof(Affliction)));
                            break;
                        case "bloodloss":
                            List.Add(Bloodloss = new AfflictionPrefab(sourceElement, typeof(Affliction)));
                            break;
                        case "pressure":
                            List.Add(Pressure = new AfflictionPrefab(sourceElement, typeof(Affliction)));
                            break;
                        case "stun":
                            List.Add(Stun = new AfflictionPrefab(sourceElement, typeof(Affliction)));
                            break;
                        case "husk":
                        case "afflictionhusk":
                            List.Add(Husk = new AfflictionPrefab(sourceElement, typeof(AfflictionHusk)));
                            break;
                        case "cprsettings":
                            if (CPRSettings.IsLoaded)
                            {
                                if (isOverride)
                                {
                                    DebugConsole.NewMessage($"Overriding the CPR settings with '{filePath}'", Color.Yellow);
                                }
                                else
                                {
                                    DebugConsole.ThrowError($"Error in '{filePath}': CPR settings already loaded. Add <override></override> tags as the parent of the custom CPRSettings to allow overriding the vanilla values.");
                                    break;
                                }
                            }
                            CPRSettings.Load(sourceElement);
                            break;
                        default:
                            List.Add(new AfflictionPrefab(sourceElement));
                            break;
                    }
                }
            }

            if (InternalDamage == null) DebugConsole.ThrowError("Affliction \"Internal Damage\" not defined in the affliction prefabs.");
            if (Bleeding == null) DebugConsole.ThrowError("Affliction \"Bleeding\" not defined in the affliction prefabs.");
            if (Burn == null) DebugConsole.ThrowError("Affliction \"Burn\" not defined in the affliction prefabs.");
            if (OxygenLow == null) DebugConsole.ThrowError("Affliction \"OxygenLow\" not defined in the affliction prefabs.");
            if (Bloodloss == null) DebugConsole.ThrowError("Affliction \"Bloodloss\" not defined in the affliction prefabs.");
            if (Pressure == null) DebugConsole.ThrowError("Affliction \"Pressure\" not defined in the affliction prefabs.");
            if (Stun == null) DebugConsole.ThrowError("Affliction \"Stun\" not defined in the affliction prefabs.");
            if (Husk == null) DebugConsole.ThrowError("Affliction \"Husk\" not defined in the affliction prefabs.");
        }

        public AfflictionPrefab(XElement element, Type type = null)
        {
            typeName = type == null ? element.Name.ToString() : type.Name;

            Identifier = element.GetAttributeString("identifier", "");

            AfflictionType = element.GetAttributeString("type", "");
            Name = TextManager.Get("AfflictionName." + Identifier, true) ?? element.GetAttributeString("name", "");
            Description = TextManager.Get("AfflictionDescription." + Identifier, true) ?? element.GetAttributeString("description", "");
            IsBuff = element.GetAttributeBool("isbuff", false);

            LimbSpecific = element.GetAttributeBool("limbspecific", false);
            if (!LimbSpecific)
            {
                string indicatorLimbName = element.GetAttributeString("indicatorlimb", "Torso");
                if (!Enum.TryParse(indicatorLimbName, out IndicatorLimb))
                {
                    DebugConsole.ThrowError("Error in affliction prefab " + Name + " - limb type \"" + indicatorLimbName + "\" not found.");
                }
            }

            ActivationThreshold = element.GetAttributeFloat("activationthreshold", 0.0f);
            ShowIconThreshold   = element.GetAttributeFloat("showiconthreshold", Math.Max(ActivationThreshold, 0.05f));
            MaxStrength         = element.GetAttributeFloat("maxstrength", 100.0f);

            ShowInHealthScannerThreshold = element.GetAttributeFloat("showinhealthscannerthreshold", Math.Max(ActivationThreshold, 0.05f));

            DamageOverlayAlpha  = element.GetAttributeFloat("damageoverlayalpha", 0.0f);
            BurnOverlayAlpha    = element.GetAttributeFloat("burnoverlayalpha", 0.0f);

            KarmaChangeOnApplied = element.GetAttributeFloat("karmachangeonapplied", 0.0f);

            CauseOfDeathDescription     = TextManager.Get("AfflictionCauseOfDeath." + Identifier, true) ?? element.GetAttributeString("causeofdeathdescription", "");
            SelfCauseOfDeathDescription = TextManager.Get("AfflictionCauseOfDeathSelf." + Identifier, true) ?? element.GetAttributeString("selfcauseofdeathdescription", "");


            AchievementOnRemoved = element.GetAttributeString("achievementonremoved", "");
            
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "icon":
                        Icon = new Sprite(subElement);
                        IconColor = subElement.GetAttributeColor("color", Color.White);
                        break;
                    case "effect":
                        effects.Add(new Effect(subElement, Name));
                        break;
                }
            }

            try
            {
                if (type == null)
                {
                    type = Type.GetType("Barotrauma." + typeName, true, true);
                    if (type == null)
                    {
                        DebugConsole.ThrowError("Could not find an affliction class of the type \"" + typeName + "\".");
                        return;
                    }
                }
            }
            catch
            {
                DebugConsole.ThrowError("Could not find an affliction class of the type \"" + typeName + "\".");
                return;
            }

            constructor = type.GetConstructor(new[] { typeof(AfflictionPrefab), typeof(float) });
        }

        public override string ToString()
        {
            return "AfflictionPrefab (" + Name + ")";
        }

        public Affliction Instantiate(float strength, Character source = null)
        {
            object instance = null;
            try
            {
                instance = constructor.Invoke(new object[] { this, strength });
            }
            catch (Exception ex)
            {
                DebugConsole.ThrowError(ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString());
            }
            Affliction affliction = instance as Affliction;
            affliction.Source = source;
            return affliction;
        }

        public Effect GetActiveEffect(float currentStrength)
        {
            foreach (Effect effect in effects)
            {
                if (currentStrength > effect.MinStrength && currentStrength <= effect.MaxStrength) return effect;
            }

            //if above the strength range of all effects, use the highest strength effect
            Effect strongestEffect = null;
            float largestStrength = currentStrength;
            foreach (Effect effect in effects)
            {
                if (currentStrength > effect.MaxStrength && 
                    (strongestEffect == null || effect.MaxStrength > largestStrength))
                {
                    strongestEffect = effect;
                    largestStrength = effect.MaxStrength;
                }
            }
            return strongestEffect;
        }

        public float GetTreatmentSuitability(Item item)
        {
            if (item == null || !treatmentSuitability.ContainsKey(item.Prefab.Identifier.ToLowerInvariant()))
            {
                return 0.0f;
            }
            return treatmentSuitability[item.Prefab.Identifier.ToLowerInvariant()];
        }
    }
}
