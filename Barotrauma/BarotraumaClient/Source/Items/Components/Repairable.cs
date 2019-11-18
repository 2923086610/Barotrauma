﻿using Barotrauma.Networking;
using Barotrauma.Particles;
using Barotrauma.Sounds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    partial class Repairable : ItemComponent, IDrawableComponent
    {
        public GUIButton RepairButton { get; private set; }

        public GUIButton SabotageButton { get; private set; }

        private GUIProgressBar progressBar;

        private List<ParticleEmitter> particleEmitters = new List<ParticleEmitter>();
        //the corresponding particle emitter is active when the condition is within this range
        private List<Vector2> particleEmitterConditionRanges = new List<Vector2>();

        private SoundChannel repairSoundChannel;

        private string repairButtonText, repairingText;
        private string sabotageButtonText, sabotagingText;

        private FixActions requestStartFixAction;

        [Serialize("", false, description: "An optional description of the needed repairs displayed in the repair interface.")]
        public string Description
        {
            get;
            set;
        }

        public Vector2 DrawSize
        {
            //use the extents of the item as the draw size
            get { return Vector2.Zero; }
        }

        public override bool ShouldDrawHUD(Character character)
        {
            if (!HasRequiredItems(character, false) || character.SelectedConstruction != item) return false;
            return item.ConditionPercentage < ShowRepairUIThreshold || character.IsTraitor && item.ConditionPercentage > MinSabotageCondition || (CurrentFixer == character && (!item.IsFullCondition || (character.IsTraitor && item.ConditionPercentage > MinSabotageCondition)));
        }

        partial void InitProjSpecific(XElement element)
        {
            var paddedFrame = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.85f), GuiFrame.RectTransform, Anchor.Center), childAnchor: Anchor.TopCenter)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                header, textAlignment: Alignment.TopCenter, font: GUI.LargeFont);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), paddedFrame.RectTransform),
                Description, font: GUI.SmallFont, wrap: true);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                TextManager.Get("RequiredRepairSkills"));
            for (int i = 0; i < requiredSkills.Count; i++)
            {
                var skillText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                    "   - " + TextManager.AddPunctuation(':', TextManager.Get("SkillName." + requiredSkills[i].Identifier), ((int) requiredSkills[i].Level).ToString()),
                    font: GUI.SmallFont)
                {
                    UserData = requiredSkills[i]
                };
            }

            progressBar = new GUIProgressBar(new RectTransform(new Vector2(1.0f, 0.15f), paddedFrame.RectTransform),
                color: Color.Green, barSize: 0.0f);

            repairButtonText = TextManager.Get("RepairButton");
            repairingText = TextManager.Get("Repairing");
            RepairButton = new GUIButton(new RectTransform(new Vector2(0.8f, 0.15f), paddedFrame.RectTransform, Anchor.TopCenter), repairButtonText)
            {
                OnClicked = (btn, obj) =>
                {
                    requestStartFixAction = FixActions.Repair;
                    item.CreateClientEvent(this);
                    return true;
                }
            };
            sabotageButtonText = TextManager.Get("SabotageButton");
            sabotagingText = TextManager.Get("Sabotaging");
            SabotageButton = new GUIButton(new RectTransform(new Vector2(0.8f, 0.15f), paddedFrame.RectTransform, Anchor.BottomCenter), sabotageButtonText)
            {
                OnClicked = (btn, obj) =>
                {
                    requestStartFixAction = FixActions.Sabotage;
                    item.CreateClientEvent(this);
                    return true;
                }
            };

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "emitter":
                    case "particleemitter":
                        particleEmitters.Add(new ParticleEmitter(subElement));
                        particleEmitterConditionRanges.Add(new Vector2(
                            subElement.GetAttributeFloat("mincondition", 0.0f), 
                            subElement.GetAttributeFloat("maxcondition", 100.0f)));
                        break;
                }
            }
        }

        partial void UpdateProjSpecific(float deltaTime)
        {
            if (!GameMain.IsMultiplayer)
            {
                switch (requestStartFixAction)
                {
                    case FixActions.Repair:
                    case FixActions.Sabotage:
                        StartRepairing(Character.Controlled, requestStartFixAction);
                        requestStartFixAction = FixActions.None;
                        break;
                    default:
                        requestStartFixAction = FixActions.None;
                        break;
                }
            }
            
            for (int i = 0; i < particleEmitters.Count; i++)
            {
                if (item.ConditionPercentage >= particleEmitterConditionRanges[i].X && item.ConditionPercentage <= particleEmitterConditionRanges[i].Y)
                {
                    particleEmitters[i].Emit(deltaTime, item.WorldPosition, item.CurrentHull);
                }
            }

            if (CurrentFixer != null && CurrentFixer.SelectedConstruction == item)
            {
                if (repairSoundChannel == null || !repairSoundChannel.IsPlaying)
                {
                    repairSoundChannel = SoundPlayer.PlaySound("repair", item.WorldPosition, hullGuess: item.CurrentHull);
                }
            }
            else
            {
                repairSoundChannel?.FadeOutAndDispose();
                repairSoundChannel = null;
            }
        }
        
        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            IsActive = true;

            progressBar.BarSize = item.Condition / item.MaxCondition;
            progressBar.Color = ToolBox.GradientLerp(progressBar.BarSize, Color.Red, Color.Orange, Color.Green);

            RepairButton.Enabled = (currentFixerAction == FixActions.None || (CurrentFixer == character && currentFixerAction != FixActions.Repair)) && item.ConditionPercentage <= ShowRepairUIThreshold;
            RepairButton.Text = (currentFixerAction == FixActions.None || CurrentFixer != character || currentFixerAction != FixActions.Repair) ? 
                repairButtonText : 
                repairingText + new string('.', ((int)(Timing.TotalTime * 2.0f) % 3) + 1);

            SabotageButton.Visible = character.IsTraitor;
            SabotageButton.Enabled = (currentFixerAction == FixActions.None || (CurrentFixer == character && currentFixerAction != FixActions.Sabotage)) && character.IsTraitor && item.ConditionPercentage > MinSabotageCondition;
            SabotageButton.Text = (currentFixerAction == FixActions.None || CurrentFixer != character || currentFixerAction != FixActions.Sabotage || !character.IsTraitor) ?
                sabotageButtonText :
                sabotagingText + new string('.', ((int)(Timing.TotalTime * 2.0f) % 3) + 1);

            System.Diagnostics.Debug.Assert(GuiFrame.GetChild(0) is GUILayoutGroup, "Repair UI hierarchy has changed, could not find skill texts");
            foreach (GUIComponent c in GuiFrame.GetChild(0).Children)
            {
                if (!(c.UserData is Skill skill)) continue;

                GUITextBlock textBlock = (GUITextBlock)c;
                if (character.GetSkillLevel(skill.Identifier) < skill.Level)
                {
                    textBlock.TextColor = Color.Red;
                }
                else
                {
                    textBlock.TextColor = Color.White;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, bool editing, float itemDepth = -1)
        {
            if (GameMain.DebugDraw && Character.Controlled?.FocusedItem == item)
            {
                bool paused = !ShouldDeteriorate();
                if (deteriorationTimer > 0.0f)
                {
                    GUI.DrawString(spriteBatch,
                        new Vector2(item.WorldPosition.X, -item.WorldPosition.Y), "Deterioration delay " + ((int)deteriorationTimer) + (paused ? " [PAUSED]" : ""),
                        paused ? Color.Cyan : Color.Lime, Color.Black * 0.5f);
                }
                else
                {
                    GUI.DrawString(spriteBatch,
                        new Vector2(item.WorldPosition.X, -item.WorldPosition.Y), "Deteriorating at " + (int)(DeteriorationSpeed * 60.0f) + " units/min" + (paused ? " [PAUSED]" : ""),
                        paused ? Color.Cyan : Color.Red, Color.Black * 0.5f);
                }
                GUI.DrawString(spriteBatch,
                    new Vector2(item.WorldPosition.X, -item.WorldPosition.Y + 20), "Condition: " + (int)item.Condition + "/" + (int)item.MaxCondition,
                    Color.Orange);
            }
        }

        protected override void RemoveComponentSpecific()
        {
            repairSoundChannel?.FadeOutAndDispose();
            repairSoundChannel = null;
        }

        public void ClientRead(ServerNetObject type, IReadMessage msg, float sendingTime)
        {
            deteriorationTimer = msg.ReadSingle();
            deteriorateAlwaysResetTimer = msg.ReadSingle();
            DeteriorateAlways = msg.ReadBoolean();
            ushort currentFixerID = msg.ReadUInt16();
            currentFixerAction = (FixActions)msg.ReadRangedInteger(0, 2);

            if (currentFixerID == 0)
            {
                CurrentFixer = null;
            }
            else
            {
                CurrentFixer = Entity.FindEntityByID(currentFixerID) as Character;
            }
        }

        public void ClientWrite(IWriteMessage msg, object[] extraData = null)
        {
            msg.WriteRangedInteger((int)requestStartFixAction, 0, 2);
        }
    }
}
