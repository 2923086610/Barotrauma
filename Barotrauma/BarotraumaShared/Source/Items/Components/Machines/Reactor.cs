﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Reactor : Powered, IServerSerializable, IClientSerializable
    {
        const float NetworkUpdateInterval = 0.5f;

        //the rate at which the reactor is being run on (higher rate -> higher temperature)
        private float fissionRate;
        
        //how much of the generated steam is used to spin the turbines and generate power
        private float turbineOutput;
        
        private float temperature;
        
        //is automatic temperature control on
        //(adjusts the fission rate and turbine output automatically to keep the
        //amount of power generated balanced with the load)
        private bool autoTemp;
        
        //automatical adjustment to the power output when 
        //turbine output and temperature are in the optimal range
        private float autoAdjustAmount;
        
        private float fuelConsumptionRate;

        private float meltDownTimer, meltDownDelay;
        private float fireTimer, fireDelay;

        private float maxPowerOutput;

        private float load;
        
        private bool unsentChanges;
        private float sendUpdateTimer;

        private float degreeOfSuccess;
        
        private Vector2 optimalTemperature, allowedTemperature;
        private Vector2 optimalFissionRate, allowedFissionRate;
        private Vector2 optimalTurbineOutput, allowedTurbineOutput;

        private bool shutDown;

        const float AIUpdateInterval = 0.2f;
        private float aiUpdateTimer;

        private Character lastUser;
        private Character LastUser
        {
            get { return lastUser; }
            set
            {
                if (lastUser == value) return;
                lastUser = value;
                degreeOfSuccess = lastUser == null ? 0.0f : DegreeOfSuccess(lastUser);
            }
        }
        
        [Editable(0.0f, float.MaxValue, ToolTip = "How much power (kW) the reactor generates when operating at full capacity."), Serialize(10000.0f, true)]
        public float MaxPowerOutput
        {
            get { return maxPowerOutput; }
            set
            {
                maxPowerOutput = Math.Max(0.0f, value);
            }
        }
        
        [Editable(0.0f, float.MaxValue, ToolTip = "How long the temperature has to stay critical until a meltdown occurs."), Serialize(120.0f, true)]
        public float MeltdownDelay
        {
            get { return meltDownDelay; }
            set { meltDownDelay = Math.Max(value, 0.0f); }
        }

        [Editable(0.0f, float.MaxValue, ToolTip = "How long the temperature has to stay critical until the reactor catches fire."), Serialize(30.0f, true)]
        public float FireDelay
        {
            get { return fireDelay; }
            set { fireDelay = Math.Max(value, 0.0f); }
        }

        [Serialize(0.0f, true)]
        public float Temperature
        {
            get { return temperature; }
            set
            {
                if (!MathUtils.IsValid(value)) return;
                temperature = MathHelper.Clamp(value, 0.0f, 100.0f);
            }
        }

        [Serialize(0.0f, true)]
        public float FissionRate
        {
            get { return fissionRate; }
            set 
            {
                if (!MathUtils.IsValid(value)) return;
                fissionRate = MathHelper.Clamp(value, 0.0f, 100.0f); 
            }
        }

        [Serialize(0.0f, true)]
        public float TurbineOutput
        {
            get { return turbineOutput; }
            set 
            {
                if (!MathUtils.IsValid(value)) return;
                turbineOutput = MathHelper.Clamp(value, 0.0f, 100.0f); 
            }
        }
        
        [Serialize(0.2f, true), Editable(0.0f, 1000.0f, ToolTip = "How fast the condition of the contained fuel rods deteriorates.")]
        public float FuelConsumptionRate
        {
            get { return fuelConsumptionRate; }
            set
            {
                if (!MathUtils.IsValid(value)) return;
                fuelConsumptionRate = Math.Max(value, 0.0f);
            }
        }

        [Serialize(false, true)]
        public bool TemperatureCritical
        {
            get { return temperature > allowedTemperature.Y; }
            set { /*do nothing*/ }
        }

        private float correctTurbineOutput;

        private float targetFissionRate;
        private float targetTurbineOutput;
        
        [Serialize(false, true)]
        public bool AutoTemp
        {
            get { return autoTemp; }
            set 
            { 
                autoTemp = value;
#if CLIENT
                if (autoTempSlider != null) 
                {
                    autoTempSlider.BarScroll = value ? 
                        Math.Min(0.45f, autoTempSlider.BarScroll) : 
                        Math.Max(0.55f, autoTempSlider.BarScroll);
                }
#endif
            }
        }
        
        private float prevAvailableFuel;
        public float AvailableFuel { get; set; }
        
        public Reactor(Item item, XElement element)
            : base(item, element)
        {         
            IsActive = true;
            InitProjSpecific(element);
        }

        partial void InitProjSpecific(XElement element);
                
        public override void Update(float deltaTime, Camera cam)
        {
#if SERVER
            if (GameMain.Server != null && nextServerLogWriteTime != null)
            {
                if (Timing.TotalTime >= (float)nextServerLogWriteTime)
                {
                    GameServer.Log(lastUser.LogName + " adjusted reactor settings: " +
                            "Temperature: " + (int)(temperature * 100.0f) +
                            ", Fission rate: " + (int)targetFissionRate +
                            ", Turbine output: " + (int)targetTurbineOutput +
                            (autoTemp ? ", Autotemp ON" : ", Autotemp OFF"),
                            ServerLog.MessageType.ItemInteraction);

                    nextServerLogWriteTime = null;
                    lastServerLogWriteTime = (float)Timing.TotalTime;
                }
            }
#endif

            prevAvailableFuel = AvailableFuel;
            ApplyStatusEffects(ActionType.OnActive, deltaTime, null);

            //use a smoothed "correct output" instead of the actual correct output based on the load
            //so the player doesn't have to keep adjusting the rate impossibly fast when the load fluctuates heavily
            correctTurbineOutput += MathHelper.Clamp((load / MaxPowerOutput * 100.0f) - correctTurbineOutput, -10.0f, 10.0f) * deltaTime;

            //calculate tolerances of the meters based on the skills of the user
            //more skilled characters have larger "sweet spots", making it easier to keep the power output at a suitable level
            float tolerance = MathHelper.Lerp(2.5f, 10.0f, degreeOfSuccess);
            optimalTurbineOutput = new Vector2(correctTurbineOutput - tolerance, correctTurbineOutput + tolerance);
            tolerance = MathHelper.Lerp(5.0f, 20.0f, degreeOfSuccess);
            allowedTurbineOutput = new Vector2(correctTurbineOutput - tolerance, correctTurbineOutput + tolerance);
            
            optimalTemperature = Vector2.Lerp(new Vector2(40.0f, 60.0f), new Vector2(30.0f, 70.0f), degreeOfSuccess);
            allowedTemperature = Vector2.Lerp(new Vector2(30.0f, 70.0f), new Vector2(10.0f, 90.0f), degreeOfSuccess);
            
            optimalFissionRate = Vector2.Lerp(new Vector2(30, AvailableFuel - 20), new Vector2(20, AvailableFuel - 10), degreeOfSuccess);
            optimalFissionRate.X = Math.Min(optimalFissionRate.X, optimalFissionRate.Y - 10);
            allowedFissionRate = Vector2.Lerp(new Vector2(20, AvailableFuel), new Vector2(10, AvailableFuel), degreeOfSuccess);
            allowedFissionRate.X = Math.Min(allowedFissionRate.X, allowedFissionRate.Y - 10);

            float heatAmount = GetGeneratedHeat(fissionRate);
            float temperatureDiff = (heatAmount - turbineOutput) - Temperature;
            Temperature += MathHelper.Clamp(Math.Sign(temperatureDiff) * 10.0f * deltaTime, -Math.Abs(temperatureDiff), Math.Abs(temperatureDiff));
            //if (item.InWater && AvailableFuel < 100.0f) Temperature -= 12.0f * deltaTime;
            
            FissionRate = MathHelper.Lerp(fissionRate, Math.Min(targetFissionRate, AvailableFuel), deltaTime);
            TurbineOutput = MathHelper.Lerp(turbineOutput, targetTurbineOutput, deltaTime);

            float temperatureFactor = Math.Min(temperature / 50.0f, 1.0f);
            currPowerConsumption = -MaxPowerOutput * Math.Min(turbineOutput / 100.0f, temperatureFactor);

            //if the turbine output and coolant flow are the optimal range, 
            //make the generated power slightly adjust according to the load
            //  (-> the reactor can automatically handle small changes in load as long as the values are roughly correct)
            if (turbineOutput > optimalTurbineOutput.X && turbineOutput < optimalTurbineOutput.Y && 
                temperature > optimalTemperature.X && temperature < optimalTemperature.Y)
            {
                float maxAutoAdjust = maxPowerOutput * 0.1f;
                autoAdjustAmount = MathHelper.Lerp(
                    autoAdjustAmount, 
                    MathHelper.Clamp(-load - currPowerConsumption, -maxAutoAdjust, maxAutoAdjust), 
                    deltaTime * 10.0f);
            }
            else
            {
                autoAdjustAmount = MathHelper.Lerp(autoAdjustAmount, 0.0f, deltaTime * 10.0f);
            }
            currPowerConsumption += autoAdjustAmount;

            if (shutDown)
            {
                targetFissionRate = 0.0f;
                targetTurbineOutput = 0.0f;
            }
            else if (autoTemp)
            {
                UpdateAutoTemp(2.0f, deltaTime);
            }

            load = 0.0f;
            List<Connection> connections = item.Connections;
            if (connections != null && connections.Count > 0)
            {
                foreach (Connection connection in connections)
                {
                    if (!connection.IsPower) { continue; }
                    foreach (Connection recipient in connection.Recipients)
                    {
                        if (!(recipient.Item is Item it)) { continue; }

                        PowerTransfer pt = it.GetComponent<PowerTransfer>();
                        if (pt == null) { continue; }

                        //calculate how much external power there is in the grid 
                        //(power coming from somewhere else than this reactor, e.g. batteries)
                        float externalPower = CurrPowerConsumption - pt.CurrPowerConsumption;
                        //reduce the external power from the load to prevent overloading the grid
                        load = Math.Max(load, pt.PowerLoad - externalPower);
                    }
                }
            }

            if (fissionRate > 0.0f)
            {
                foreach (Item item in item.ContainedItems)
                {
                    if (!item.HasTag("reactorfuel")) continue;
                    item.Condition -= fissionRate / 100.0f * fuelConsumptionRate * deltaTime;
                }

                if (item.CurrentHull != null)
                {
                    //the sound can be heard from 20 000 display units away when running at full power
                    item.CurrentHull.SoundRange = Math.Max(
                        (-currPowerConsumption / MaxPowerOutput) * 20000.0f, 
                        item.CurrentHull.AiTarget.SoundRange);
                }
            }

            item.SendSignal(0, ((int)(temperature * 100.0f)).ToString(), "temperature_out", null);

            UpdateFailures(deltaTime);
#if CLIENT
            UpdateGraph(deltaTime);
#endif
            AvailableFuel = 0.0f;

            sendUpdateTimer = Math.Max(sendUpdateTimer - deltaTime, 0.0f);

            if (unsentChanges && sendUpdateTimer <= 0.0f)
            {
#if SERVER
                if (GameMain.Server != null)
                {
                    item.CreateServerEvent(this);
                }
#endif
#if CLIENT
                if (GameMain.Client != null)
                {
                    item.CreateClientEvent(this);
                }
#endif
                sendUpdateTimer = NetworkUpdateInterval;
                unsentChanges = false;
            }
        }

        private float GetGeneratedHeat(float fissionRate)
        {
            return fissionRate * (prevAvailableFuel / 100.0f) * 2.0f;
        }

        /// <summary>
        /// Do we need more fuel to generate enough power to match the current load.
        /// </summary>
        /// <param name="minimumOutputRatio">How low we allow the output/load ratio to go before loading more fuel. 
        /// 1.0 = always load more fuel when maximum output is too low, 0.5 = load more if max output is 50% of the load</param>
        private bool NeedMoreFuel(float minimumOutputRatio)
        {
            if (prevAvailableFuel <= 0.0f && load > 0.0f)
            {
                return true;
            }

            //fission rate is clamped to the amount of available fuel
            float maxFissionRate = Math.Min(prevAvailableFuel, 100.0f);
            float maxTurbineOutput = 100.0f;

            //calculate the maximum output if the fission rate is cranked as high as it goes and turbine output is at max
            float theoreticalMaxHeat = GetGeneratedHeat(fissionRate: maxFissionRate);             
            float temperatureFactor = Math.Min(theoreticalMaxHeat / 50.0f, 1.0f);
            float theoreticalMaxOutput = Math.Min(maxTurbineOutput / 100.0f, temperatureFactor) * MaxPowerOutput;

            //maximum output not enough, we need more fuel
            return theoreticalMaxOutput < load * minimumOutputRatio;
        }

        private bool TooMuchFuel()
        {
            var containedItems = item.ContainedItems;
            if (containedItems != null && containedItems.Count() <= 1) { return false; }

            //get the amount of heat we'd generate if the fission rate was at the low end of the optimal range
            float minimumHeat = GetGeneratedHeat(optimalFissionRate.X);

            //if we need a very high turbine output to keep the engine from overheating, there's too much fuel
            return minimumHeat > Math.Min(correctTurbineOutput * 1.5f, 90);
        }

        private void UpdateFailures(float deltaTime)
        {
            if (temperature > allowedTemperature.Y)
            {
                item.SendSignal(0, "1", "meltdown_warning", null);
                //faster meltdown if the item is in a bad condition
                meltDownTimer += MathHelper.Lerp(deltaTime * 2.0f, deltaTime, item.Condition / item.MaxCondition);

                if (meltDownTimer > MeltdownDelay)
                {
                    MeltDown();
                    return;
                }
            }
            else
            {
                item.SendSignal(0, "0", "meltdown_warning", null);
                meltDownTimer = Math.Max(0.0f, meltDownTimer - deltaTime);
            }

            if (temperature > optimalTemperature.Y)
            {
                float prevFireTimer = fireTimer;
                fireTimer += MathHelper.Lerp(deltaTime * 2.0f, deltaTime, item.Condition / item.MaxCondition);


#if SERVER
                if (fireTimer > Math.Min(5.0f, FireDelay / 2) && blameOnBroken?.Character?.SelectedConstruction == item)
                {
                    GameMain.Server.KarmaManager.OnReactorOverHeating(blameOnBroken.Character, deltaTime);
                }
#endif

                if (fireTimer >= FireDelay && prevFireTimer < fireDelay)
                {
                    new FireSource(item.WorldPosition);
                }
            }
            else
            {
                fireTimer = Math.Max(0.0f, fireTimer - deltaTime);
            }
        }

        private void UpdateAutoTemp(float speed, float deltaTime)
        {
            float desiredTurbineOutput = (optimalTurbineOutput.X + optimalTurbineOutput.Y) / 2.0f;
            targetTurbineOutput += MathHelper.Clamp(desiredTurbineOutput - targetTurbineOutput, -speed, speed) * deltaTime;
            targetTurbineOutput = MathHelper.Clamp(targetTurbineOutput, 0.0f, 100.0f);

            float desiredFissionRate = (optimalFissionRate.X + optimalFissionRate.Y) / 2.0f;
            targetFissionRate += MathHelper.Clamp(desiredFissionRate - targetFissionRate, -speed, speed) * deltaTime;

            if (temperature > (optimalTemperature.X + optimalTemperature.Y) / 2.0f)
            {
                targetFissionRate = Math.Min(targetFissionRate - speed * 2 * deltaTime, allowedFissionRate.Y);
            }
            else if (-currPowerConsumption < load)
            {
                targetFissionRate = Math.Min(targetFissionRate + speed * 2 * deltaTime, 100.0f);
            }
            targetFissionRate = MathHelper.Clamp(targetFissionRate, 0.0f, 100.0f);

            //don't push the target too far from the current fission rate
            //otherwise we may "overshoot", cranking the target fission rate all the way up because it takes a while
            //for the actual fission rate and temperature to follow
            targetFissionRate = MathHelper.Clamp(targetFissionRate, FissionRate - 5, FissionRate + 5);
        }
        
        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            base.UpdateBroken(deltaTime, cam);

            currPowerConsumption = 0.0f;
            Temperature -= deltaTime * 1000.0f;
            targetFissionRate = Math.Max(targetFissionRate - deltaTime * 10.0f, 0.0f);
            targetTurbineOutput = Math.Max(targetTurbineOutput - deltaTime * 10.0f, 0.0f);
#if CLIENT
            fissionRateScrollBar.BarScroll = 1.0f - FissionRate / 100.0f;
            turbineOutputScrollBar.BarScroll = 1.0f - TurbineOutput / 100.0f;
            UpdateGraph(deltaTime);
#endif
        }

        private void MeltDown()
        {
            if (item.Condition <= 0.0f) { return; }
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return; }

            item.Condition = 0.0f;
            fireTimer = 0.0f;
            meltDownTimer = 0.0f;

            var containedItems = item.ContainedItems;
            if (containedItems != null)
            {
                foreach (Item containedItem in containedItems)
                {
                    if (containedItem == null) continue;
                    containedItem.Condition = 0.0f;
                }
            }

#if SERVER
            GameServer.Log("Reactor meltdown!", ServerLog.MessageType.ItemInteraction);
            if (GameMain.Server != null)
            {
                GameMain.Server.KarmaManager.OnReactorMeltdown(blameOnBroken?.Character);
            }
#endif
        }

        public override bool Pick(Character picker)
        {
            return picker != null;
        }

        public override bool AIOperate(float deltaTime, Character character, AIObjectiveOperateItem objective)
        {
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return false; }

            IsActive = true;

            float degreeOfSuccess = DegreeOfSuccess(character);

            //characters with insufficient skill levels don't refuel the reactor
            if (degreeOfSuccess > 0.2f)
            {
                //remove used-up fuel from the reactor
                var containedItems = item.ContainedItems;
                foreach (Item item in containedItems)
                {
                    if (item != null && item.Condition <= 0.0f)
                    {
                        item.Drop(character);
                    }
                }

                if (aiUpdateTimer > 0.0f)
                {
                    aiUpdateTimer -= deltaTime;
                    return false;
                }

                //load more fuel if the current maximum output is only 50% of the current load
                if (NeedMoreFuel(minimumOutputRatio: 0.5f))
                {
                    var containFuelObjective = new AIObjectiveContainItem(character, new string[] { "fuelrod", "reactorfuel" }, item.GetComponent<ItemContainer>(), objective.objectiveManager)
                    {
                        targetItemCount = item.ContainedItems.Count(i => i != null && i.Prefab.Identifier == "fuelrod" || i.HasTag("reactorfuel")) + 1,
                        GetItemPriority = (Item fuelItem) =>
                        {
                            if (fuelItem.ParentInventory?.Owner is Item)
                            {
                                //don't take fuel from other reactors
                                if (((Item)fuelItem.ParentInventory.Owner).GetComponent<Reactor>() != null) return 0.0f;
                            }
                            return 1.0f;
                        }
                    };
                    objective.AddSubObjective(containFuelObjective);

                    character?.Speak(TextManager.Get("DialogReactorFuel"), null, 0.0f, "reactorfuel", 30.0f);

                    aiUpdateTimer = AIUpdateInterval;
                    return false;
                }
                else if (TooMuchFuel())
                {
                    foreach (Item item in item.ContainedItems)
                    {
                        if (item != null && item.HasTag("reactorfuel"))
                        {
                            if (!character.Inventory.TryPutItem(item, character, allowedSlots: item.AllowedSlots))
                            {
                                item.Drop(character);
                            }
                            break;
                        }
                    }
                }
            }

            if (lastUser != character && lastUser != null && lastUser.SelectedConstruction == item)
            {
                character.Speak(TextManager.Get("DialogReactorTaken"), null, 0.0f, "reactortaken", 10.0f);
            }

            LastUser = character;
            
            switch (objective.Option.ToLowerInvariant())
            {
                case "powerup":
                    shutDown = false;
                    //characters with insufficient skill levels simply set the autotemp on instead of trying to adjust the temperature manually
                    if (degreeOfSuccess < 0.5f)
                    {
                        if (!autoTemp) unsentChanges = true;
                        AutoTemp = true;
                    }
                    else
                    {
                        AutoTemp = false;
                        unsentChanges = true;
                        UpdateAutoTemp(MathHelper.Lerp(0.5f, 2.0f, degreeOfSuccess), 1.0f);
                    }
#if CLIENT
                    onOffSwitch.BarScroll = 0.0f;
                    fissionRateScrollBar.BarScroll = FissionRate / 100.0f;
                    turbineOutputScrollBar.BarScroll = TurbineOutput / 100.0f;
#endif
                    break;
                case "shutdown":
#if CLIENT
                    onOffSwitch.BarScroll = 1.0f;
#endif
                    if (AutoTemp || !shutDown || targetFissionRate > 0.0f || targetTurbineOutput > 0.0f)
                    {
                        unsentChanges = true;
                    }

                    AutoTemp = false;
                    shutDown = true;
                    targetFissionRate = 0.0f;
                    targetTurbineOutput = 0.0f;
                    break;
            }

            aiUpdateTimer = AIUpdateInterval;

            return false;
        }

        public override void ReceiveSignal(int stepsTaken, string signal, Connection connection, Item source, Character sender, float power, float signalStrength = 1.0f)
        {
            switch (connection.Name)
            {
                case "shutdown":
                    if (targetFissionRate > 0.0f || targetTurbineOutput > 0.0f)
                    {
                        shutDown = true;
                        AutoTemp = false;
                        targetFissionRate = 0.0f;
                        targetTurbineOutput = 0.0f;
                        unsentChanges = true;
#if CLIENT
                        onOffSwitch.BarScroll = 1.0f;
#endif
                    }
                    break;
            }
        }

    }
}
