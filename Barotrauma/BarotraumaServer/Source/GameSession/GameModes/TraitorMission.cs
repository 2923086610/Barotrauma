﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Barotrauma.Networking;

namespace Barotrauma {

    class TraitorMissionPrefab
    {
        public static readonly List<TraitorMissionPrefab> List = new List<TraitorMissionPrefab>();

        public static void Init()
        {
            var files = GameMain.Instance.GetFilesOfType(ContentType.TraitorMissions);
            foreach (string file in files)
            {
                XDocument doc = XMLExtensions.TryLoadXml(file);
                if (doc?.Root == null) continue;

                foreach (XElement element in doc.Root.Elements())
                {
                    List.Add(new TraitorMissionPrefab(element));
                }
            }
        }

        public static TraitorMissionPrefab RandomPrefab()
        {
            // TODO(xxx): Use MTRandom here? Add weighted selection support.
            return List.Count > 0 ? List[Rand.Int(List.Count)] : null;
        }

        public class Context
        {
            public List<Character> Characters;
        }

        public class Goal
        {
            public readonly string Type;
            public readonly XElement Config;

            public Goal(string type, XElement config)
            {
                this.Type = type;
                this.Config = config;
            }

            public void SelectTarget(GameServer server)
            {
            }

            private delegate bool TargetFilter(string value, Character character);
            private static Dictionary<string, TargetFilter> targetFilters = new Dictionary<string, TargetFilter>()
            {
                { "job", (value, character) => value.Equals(character.Info.Job.Name, StringComparison.OrdinalIgnoreCase) },
            };

            public Traitor.Goal Instantiate(GameServer server)
            {
                Traitor.Goal goal = null;
                switch (Config.GetAttributeString("type", "").ToLower(System.Globalization.CultureInfo.InvariantCulture)) {
                    case "killtarget":
                        {
                            List<Traitor.TraitorMission.CharacterFilter> filters = new List<Traitor.TraitorMission.CharacterFilter>();
                            foreach (var attribute in Config.Attributes())
                            {
                                if (targetFilters.TryGetValue(attribute.Name.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture), out var filter))
                                {
                                    filters.Add((character) => filter(attribute.Value, character));
                                }
                            }
                            goal = new Traitor.GoalKillTarget((character) => filters.All(f => f(character)));
                        }
                        break;
                    case "destroyitems":
                        goal = new Traitor.GoalDestroyItemsWithTag(
                            Config.GetAttributeString("tag", ""),
                            Config.GetAttributeFloat("percentage", 100.0f),
                            Config.GetAttributeBool("matchIdentifier", true),
                            Config.GetAttributeBool("matchTag", true),
                            Config.GetAttributeBool("matchInventory", false));
                        break;
                    case "sabotage":
                        // return new Traitor.GoalItemConditionLessThan();
                        // TODO(xxX)
                        break;
                    case "floodsub":
                        goal = new Traitor.GoalFloodPercentOfSub(Config.GetAttributeFloat("percentage", 100.0f));
                        break;
                }
                if (goal == null)
                {
                    return goal;
                }
                foreach (var element in Config.Elements())
                {
                    switch (element.Name.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture))
                    {
                        case "infotext":
                            {
                                var id = element.GetAttributeString("id", null);
                                if (id != null)
                                {
                                    goal.InfoTextId = id;
                                }
                            }
                            break;
                        case "completedtext":
                            {
                                var id = element.GetAttributeString("id", null);
                                if (id != null)
                                {
                                    goal.CompletedTextId = id;
                                }
                            }
                            break;
                    }
                }
                foreach (var element in Config.Elements())
                {
                    switch (element.Name.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture))
                    {
                        case "modifier":
                            {
                                var modifierType = element.GetAttributeString("type", "");
                                switch (modifierType)
                                {
                                    case "duration":
                                        goal = new Traitor.GoalWithDuration(goal, Config.GetAttributeFloat("duration", 5.0f), Config.GetAttributeBool("cumulative", false));
                                        break;
                                }
                            }
                            break;
                    }
                }
                return goal;
            }
        }

        public class Objective
        {
            public string InfoText { get; private set; }
            public readonly List<Goal> Goals;

            public Traitor.Objective Instantiate(GameServer server)
            {
                return new Traitor.Objective(InfoText, Goals.ConvertAll(goal => goal.Instantiate(server)).ToArray());
            }

            public Objective(string infoText, List<Goal> goals)
            {
                InfoText = infoText;
                Goals = goals;
            }
        }

        public readonly string Identifier;
        public readonly string StartText;
        public readonly List<Objective> Objectives = new List<Objective>();

        public Traitor.TraitorMission Instantiate(GameServer server, int traitorCount)
        {
            return new Traitor.TraitorMission(
                StartText, 
                Objectives.ConvertAll(objective => objective.Instantiate(server)).ToArray());
        }

        protected Goal LoadGoal(XElement goalRoot)
        {
            var goalType = goalRoot.GetAttributeString("type", "");
            return new Goal(goalType, goalRoot);
        }

        protected Objective LoadObjective(XElement objectiveRoot)
        {
            var goals = new List<Goal>();
            string infoText = null;
            foreach (var element in objectiveRoot.Elements())
            {
                switch(element.Name.ToString().ToLowerInvariant())
                {
                    case "infotext":
                        infoText = element.GetAttributeString("id", "");
                        break;
                    case "goal":
                        {
                            var goal = LoadGoal(element);
                            if (goal != null)
                            {
                                goals.Add(goal);
                            }
                        }
                        break;
                }
            }
            return new Objective(infoText, goals);
        }

        public TraitorMissionPrefab(XElement missionRoot)
        {
            Identifier = missionRoot.GetAttributeString("identifier", "");
            foreach (var element in missionRoot.Elements())
            {
                switch (element.Name.ToString().ToLowerInvariant())
                {
                    case "startinfotext":
                        StartText = element.GetAttributeString("id", "");
                        break;
                    case "objective":
                        {
                            var objective = LoadObjective(element);
                            if (objective != null)
                            {
                                Objectives.Add(objective);
                            }
                        }
                        break;
                }
            }
        }
    }
}
