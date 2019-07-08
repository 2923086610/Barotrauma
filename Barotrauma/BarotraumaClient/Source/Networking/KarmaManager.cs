﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;

namespace Barotrauma
{
    partial class KarmaManager : ISerializableEntity
    {
        public void CreateSettingsFrame(GUIComponent parent)
        {
            CreateLabeledSlider(parent, 0.0f, 40.0f, 1.0f, "KickBanThreshold");
            CreateLabeledSlider(parent, 0.0f, 50.0f, 1.0f, "HerpesThreshold");

            CreateLabeledSlider(parent, 0.0f, 0.5f, 0.01f, "KarmaDecay");
            CreateLabeledSlider(parent, 50.0f, 100.0f, 1.0f, "KarmaDecayThreshold");
            CreateLabeledSlider(parent, 0.0f, 0.5f, 0.01f, "KarmaIncrease");
            CreateLabeledSlider(parent, 0.0f, 50.0f, 1.0f, "KarmaIncreaseThreshold");

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.12f), parent.RectTransform), TextManager.Get("Karma.PositiveActions"), textAlignment: Alignment.Center)
            {
                CanBeFocused = false
            };

            CreateLabeledSlider(parent, 0.0f, 1.0f, 0.01f, "StructureRepairKarmaIncrease");
            CreateLabeledSlider(parent, 0.0f, 1.0f, 0.01f, "HealFriendlyKarmaIncrease");
            CreateLabeledSlider(parent, 0.0f, 1.0f, 0.01f, "DamageEnemyKarmaIncrease");
            CreateLabeledSlider(parent, 0.0f, 1.0f, 0.01f, "ItemRepairKarmaIncrease");
            CreateLabeledSlider(parent, 0.0f, 10.0f, 0.05f, "ExtinguishFireKarmaIncrease");

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.12f), parent.RectTransform), TextManager.Get("Karma.NegativeActions"), textAlignment: Alignment.Center)
            {
                CanBeFocused = false
            };

            CreateLabeledSlider(parent, 0.0f, 1.0f, 0.01f, "StructureDamageKarmaDecrease");
            CreateLabeledSlider(parent, 0.0f, 1.0f, 0.01f, "DamageFriendlyKarmaDecrease");
            CreateLabeledSlider(parent, 0.0f, 100.0f, 1.0f, "ReactorMeltdownKarmaDecrease");
            CreateLabeledSlider(parent, 0.0f, 10.0f, 0.05f, "ReactorOverheatKarmaDecrease");
            CreateLabeledSlider(parent, 0.0f, 20.0f, 1f, "AllowedWireDisconnectionsPerMinute");
            CreateLabeledSlider(parent, 0.0f, 5.0f, 0.05f, "WireDisconnectionKarmaDecrease");
            CreateLabeledSlider(parent, 0.0f, 30.0f, 1.0f, "SpamFilterKarmaDecrease");
        }

        private void CreateLabeledSlider(GUIComponent parent, float min, float max, float step, string propertyName)
        {
            var container = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.1f), parent.RectTransform), isHorizontal: true)
            {
                Stretch = true,
                RelativeSpacing = 0.05f,
                ToolTip = TextManager.Get("Karma." + propertyName + "ToolTip")
            };

            string labelText = TextManager.Get("Karma." + propertyName);
            var label = new GUITextBlock(new RectTransform(new Vector2(0.7f, 0.8f), container.RectTransform),
                labelText, font: GUI.SmallFont)
            {
                ToolTip = TextManager.Get("Karma." + propertyName + "ToolTip")
            };

            var slider = new GUIScrollBar(new RectTransform(new Vector2(0.3f, 0.8f), container.RectTransform), barSize: 0.1f)
            {
                Step = step <= 0.0f ? 0.0f : step / (max - min),
                Range = new Vector2(min, max),
                OnMoved = (GUIScrollBar scrollBar, float barScroll) =>
                {
                    string formattedValueStr = step >= 1.0f ?
                        ((int)scrollBar.BarScrollValue).ToString() :
                        scrollBar.BarScrollValue.Format(decimalCount: step <= 0.1f ? 2 : 1);
                    label.Text = TextManager.AddPunctuation(':', labelText, formattedValueStr);
                    return true;
                }
            };
            GameMain.NetworkMember.ServerSettings.AssignGUIComponent(propertyName, slider);
            slider.OnMoved(slider, slider.BarScroll);
        }
    }
}
