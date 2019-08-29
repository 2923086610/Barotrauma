﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Repairable : ItemComponent, IServerSerializable, IClientSerializable
    {
        public static float SkillIncreasePerRepair = 5.0f;
        public static float SkillIncreasePerSabotage = 3.0f;

        private string header;

        private float deteriorationTimer;
        private float deteriorateAlwaysResetTimer;

        bool wasBroken;
        bool wasGoodCondition;

        public float LastActiveTime;

        [Serialize(0.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, DecimalCount = 2, ToolTip = "How fast the condition of the item deteriorates per second.")]
        public float DeteriorationSpeed
        {
            get;
            set;
        }

        [Serialize(0.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 1000.0f, DecimalCount = 2, ToolTip = "Minimum initial delay before the item starts to deteriorate.")]
        public float MinDeteriorationDelay
        {
            get;
            set;
        }

        [Serialize(0.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 1000.0f, DecimalCount = 2, ToolTip = "Maximum initial delay before the item starts to deteriorate.")]
        public float MaxDeteriorationDelay
        {
            get;
            set;
        }

        [Serialize(50.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, ToolTip = "The item won't deteriorate spontaneously if the condition is below this value. For example, if set to 10, the condition will spontaneously drop to 10 and then stop dropping (unless the item is damaged further by external factors). Percentages of max condition.")]
        public float MinDeteriorationCondition
        {
            get;
            set;
        }

        [Serialize(0f, true)]
        public float MinSabotageCondition
        {
            get;
            set;
        }

        [Serialize(80.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, ToolTip = "The condition of the item has to be below this before the repair UI becomes usable. Percentages of max condition.")]
        public float ShowRepairUIThreshold
        {
            get;
            set;
        }

        [Serialize(100.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, ToolTip = "The amount of time it takes to fix the item with insufficient skill levels.")]
        public float FixDurationLowSkill
        {
            get;
            set;
        }

        [Serialize(10.0f, true), Editable(MinValueFloat = 0.0f, MaxValueFloat = 100.0f, ToolTip = "The amount of time it takes to fix the item with sufficient skill levels.")]
        public float FixDurationHighSkill
        {
            get;
            set;
        }

        //if enabled, the deterioration timer will always run regardless if the item is being used or not
        [Serialize(false, false)]
        public bool DeteriorateAlways
        {
            get;
            set;
        }

        public Character CurrentFixer { get; private set; }

        public enum FixActions : int
        {
            None = 0,
            Repair = 1,
            Sabotage = 2
        }

        private FixActions currentFixerAction = FixActions.None;
        public FixActions CurrentFixerAction
        {
            get => currentFixerAction;
            private set { currentFixerAction = value; }
        }

        public Repairable(Item item, XElement element)
            : base(item, element)
        {
            IsActive = true;
            canBeSelected = true;

            this.item = item;
            header = 
                TextManager.Get(element.GetAttributeString("header", ""), returnNull: true) ??
                TextManager.Get(item.Prefab.ConfigElement.GetAttributeString("header", ""), returnNull: true) ??
                element.GetAttributeString("name", "");
            InitProjSpecific(element);
        }

        public override void OnItemLoaded()
        {
            deteriorationTimer = Rand.Range(MinDeteriorationDelay, MaxDeteriorationDelay);
        }

        partial void InitProjSpecific(XElement element);
        
        public bool StartRepairing(Character character, FixActions action)
        {
            if (character == null || character.IsDead || action == FixActions.None)
            {
                DebugConsole.ThrowError("Invalid repair command!");
                return false;
            }
            else
            {
                CurrentFixer = character;
                CurrentFixerAction = action;
                return true;
            }
        }

        public bool StopRepairing(Character character)
        {
            if (CurrentFixer == character)
            {
                CurrentFixer.AnimController.Anim = AnimController.Animation.None;
                CurrentFixer = null;
                currentFixerAction = FixActions.None;
#if SERVER
                item.CreateServerEvent(this);
#endif
#if CLIENT
                repairSoundChannel?.FadeOutAndDispose();
                repairSoundChannel = null;                
#endif
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            Update(deltaTime, cam);
        }

        public void ResetDeterioration()
        {
            deteriorationTimer = Rand.Range(MinDeteriorationDelay, MaxDeteriorationDelay);
            item.Condition = item.Prefab.Health;
#if SERVER
            //let the clients know the deterioration delay
            item.CreateServerEvent(this);
#endif
        }

        public override void Update(float deltaTime, Camera cam)
        {
            UpdateProjSpecific(deltaTime);
            
            if (CurrentFixer == null)
            {
                if (deteriorateAlwaysResetTimer > 0.0f)
                {
                    deteriorateAlwaysResetTimer -= deltaTime;
                    if (deteriorateAlwaysResetTimer <= 0.0f)
                    {
                        DeteriorateAlways = false;
#if SERVER
                        //let the clients know the deterioration delay
                        item.CreateServerEvent(this);
#endif
                    }
                }
                if (!ShouldDeteriorate()) { return; }
                if (item.Condition > 0.0f)
                {
                    if (deteriorationTimer > 0.0f)
                    {
                        if (GameMain.NetworkMember == null || !GameMain.NetworkMember.IsClient)
                        {
                            deteriorationTimer -= deltaTime;
#if SERVER
                            if (deteriorationTimer <= 0.0f) { item.CreateServerEvent(this); }
#endif
                        }
                        return;
                    }

                    if (item.ConditionPercentage > MinDeteriorationCondition)
                    {
                        item.Condition -= DeteriorationSpeed * deltaTime;
                    }
                }
                return;
            }

            if (CurrentFixer != null && (CurrentFixer.SelectedConstruction != item || !CurrentFixer.CanInteractWith(item) || CurrentFixer.IsDead))
            {
                StopRepairing(CurrentFixer);
                return;
            }

            UpdateFixAnimation(CurrentFixer);

            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }

            float successFactor = requiredSkills.Count == 0 ? 1.0f : DegreeOfSuccess(CurrentFixer, requiredSkills);

            //item must have been below the repair threshold for the player to get an achievement or XP for repairing it
            if (item.ConditionPercentage < ShowRepairUIThreshold)
            {
                wasBroken = true;
            }
            if (item.ConditionPercentage > MinSabotageCondition)
            {
                wasGoodCondition = true;
            }

            float fixDuration = MathHelper.Lerp(FixDurationLowSkill, FixDurationHighSkill, successFactor);
            if (currentFixerAction == FixActions.Repair)
            {
                if (fixDuration <= 0.0f)
                {
                    item.Condition = item.MaxCondition;
                }
                else
                {
                    float conditionIncrease = deltaTime / (fixDuration / item.MaxCondition);
                    item.Condition += conditionIncrease;
#if SERVER
                    GameMain.Server.KarmaManager.OnItemRepaired(CurrentFixer, this, conditionIncrease);
#endif
                }

                if (item.IsFullCondition)
                {
                    if (wasBroken)
                    {
                        foreach (Skill skill in requiredSkills)
                        {
                            float characterSkillLevel = CurrentFixer.GetSkillLevel(skill.Identifier);
                            CurrentFixer.Info.IncreaseSkillLevel(skill.Identifier,
                                SkillIncreasePerRepair / Math.Max(characterSkillLevel, 1.0f),
                                CurrentFixer.WorldPosition + Vector2.UnitY * 100.0f);
                        }

                        SteamAchievementManager.OnItemRepaired(item, CurrentFixer);
                        deteriorationTimer = Rand.Range(MinDeteriorationDelay, MaxDeteriorationDelay);
                        wasBroken = false;
                    }
                    StopRepairing(CurrentFixer);
                }
            }
            else if (currentFixerAction == FixActions.Sabotage)
            {
                if (fixDuration <= 0.0f)
                {
                    item.Condition = item.MaxCondition * (MinSabotageCondition / 100);
                }
                else
                {
                    float conditionDecrease = deltaTime / (fixDuration / item.MaxCondition);
                    item.Condition -= conditionDecrease;
                }

                if (item.ConditionPercentage <= MinSabotageCondition)
                {
                    if (wasGoodCondition)
                    {
                        foreach (Skill skill in requiredSkills)
                        {
                            float characterSkillLevel = CurrentFixer.GetSkillLevel(skill.Identifier);
                            CurrentFixer.Info.IncreaseSkillLevel(skill.Identifier,
                                SkillIncreasePerSabotage / Math.Max(characterSkillLevel, 1.0f),
                                CurrentFixer.WorldPosition + Vector2.UnitY * 100.0f);
                        }

                        deteriorationTimer = 0.0f;
                        deteriorateAlwaysResetTimer = item.Condition / DeteriorationSpeed;
                        DeteriorateAlways = true;
                        item.Condition = item.MaxCondition * (MinSabotageCondition / 100);
                        wasGoodCondition = false;
                    }
                    StopRepairing(CurrentFixer);
                }
            }
            else
            {
                throw new NotImplementedException(currentFixerAction.ToString());
            }
        }

        partial void UpdateProjSpecific(float deltaTime);

        private bool ShouldDeteriorate()
        {
            if (LastActiveTime > Timing.TotalTime) { return true; }
            foreach (ItemComponent ic in item.Components)
            {
                if (ic is Fabricator || ic is Deconstructor)
                {
                    //fabricators and deconstructors rely on LastActiveTime
                    return false;
                }
                else if (ic is PowerTransfer pt)
                {
                    //power transfer items (junction boxes, relays) don't deteriorate if they're no carrying any power 
                    if (Math.Abs(pt.CurrPowerConsumption) > 0.1f) { return true; }
                }
                else if (ic is Engine engine)
                {
                    //engines don't deteriorate if they're not running
                    if (Math.Abs(engine.Force) > 1.0f) { return true; }
                }
                else if (ic is Pump pump)
                {
                    //pumps don't deteriorate if they're not running
                    if (Math.Abs(pump.FlowPercentage) > 1.0f) { return true; }
                }
                else if (ic is Reactor reactor)
                {
                    //reactors don't deteriorate if they're not powered up
                    if (reactor.Temperature > 0.1f) { return true; }
                }
                else if (ic is OxygenGenerator oxyGenerator)
                {
                    //oxygen generators don't deteriorate if they're not running
                    if (oxyGenerator.CurrFlow > 0.1f) { return true; }
                }
                else if (ic is Powered powered)
                {
                    if (powered.Voltage >= powered.MinVoltage) { return true; }
                }
            }

            return DeteriorateAlways;
        }

        private void UpdateFixAnimation(Character character)
        {
            character.AnimController.UpdateUseItem(false, item.WorldPosition + new Vector2(0.0f, 100.0f) * ((item.Condition / item.MaxCondition) % 0.1f));
        }

        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power = 0, float signalStrength = 1)
        {
            //do nothing
            //Repairables should always stay active, so we don't want to use the default behavior 
            //where set_active/set_state signals can disable the component
        }
    }
}
