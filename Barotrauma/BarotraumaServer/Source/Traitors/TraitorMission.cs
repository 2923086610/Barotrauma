﻿//#define SERVER_IS_TRAITOR
//#define ALLOW_SOLO_TRAITOR

using System;
using Barotrauma.Networking;
using Lidgren.Network;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Barotrauma
{
    partial class Traitor
    {
        public class TraitorMission
        {
            private static System.Random random = null;
            
            public static void InitializeRandom() => random = new System.Random((int)DateTime.UtcNow.Ticks);
            
            // All traitor related functionality should use the following interface for generating random values
            public static int Random(int n) => random.Next(n);

            private static string wordsTxt = Path.Combine("Content", "CodeWords.txt");

            private readonly List<Objective> allObjectives = new List<Objective>();
            private readonly List<Objective> pendingObjectives = new List<Objective>();
            private readonly List<Objective> completedObjectives = new List<Objective>();

            public virtual bool IsCompleted => pendingObjectives.Count <= 0;

            public readonly Dictionary<string, Traitor> Traitors = new Dictionary<string, Traitor>();

            public string StartText { get; private set; }
            public string CodeWords { get; private set; }
            public string CodeResponse { get; private set; }
            public string EndMessage {
                get
                {
                    var traitor = Traitors["traitor"];
                    if (pendingObjectives.Count <= 0)
                    {
                        if (completedObjectives.Count <= 0) return "";
                        return completedObjectives[completedObjectives.Count - 1].EndMessageText;
                    }
                    else
                    {
                        return pendingObjectives[0].EndMessageText;
                    }
                }
            }

            public string GlobalEndMessageSuccessTextId { get; private set; }
            public string GlobalEndMessageSuccessDeadTextId { get; private set; }
            public string GlobalEndMessageSuccessDetainedTextId { get; private set; }
            public string GlobalEndMessageFailureTextId { get; private set; }
            public string GlobalEndMessageFailureDeadTextId { get; private set; }
            public string GlobalEndMessageFailureDetainedTextId { get; private set; }

            private readonly string objectiveGoalInfoFormat = "[index]. [goalinfos]\n";

            public virtual IEnumerable<string> GlobalEndMessageKeys => new string[] { "[traitorname]", "[traitorgoalinfos]" };
            public virtual IEnumerable<string> GlobalEndMessageValues {
                get {
                    var isSuccess = completedObjectives.Count >= allObjectives.Count;
                    return new string[] {
                        (Traitors.TryGetValue("traitor", out var traitor) ? traitor.Character?.Name : null) ?? "(unknown)",
                        (isSuccess ? completedObjectives.LastOrDefault() : pendingObjectives.FirstOrDefault())?.GoalInfos ?? ""
                    };
                }
            }

            public string GlobalEndMessage
            {
                get
                {
                    var traitor = Traitors["traitor"];
                    if (allObjectives.Count > 0)
                    {
                        var isSuccess = completedObjectives.Count >= allObjectives.Count;
                        var traitorIsDead = traitor.Character.IsDead;
                        var traitorIsDetained = traitor.Character.LockHands;
                        var messageId = isSuccess
                            ? (traitorIsDead ? GlobalEndMessageSuccessDeadTextId : traitorIsDetained ? GlobalEndMessageSuccessDetainedTextId : GlobalEndMessageSuccessTextId)
                            : (traitorIsDead ? GlobalEndMessageFailureDeadTextId : traitorIsDetained ? GlobalEndMessageFailureDetainedTextId : GlobalEndMessageFailureTextId);
                        return TextManager.FormatServerMessageWithGenderPronouns(traitor.Character?.Info?.Gender ?? Gender.None, messageId, GlobalEndMessageKeys.ToArray(), GlobalEndMessageValues.ToArray()); 
                    }
                    return "";
                }
            }

            public Objective GetCurrentObjective(Traitor traitor)
            {
                return pendingObjectives.Count > 0 ? pendingObjectives[0] : null;
            }

            public virtual void Start(GameServer server, params string[] traitorRoles)
            {
                List<Character> characters = new List<Character>(); //ANYONE can be a target.
                List<Character> traitorCandidates = new List<Character>(); //Keep this to not re-pick traitors twice

                foreach (var character in Character.CharacterList)
                {
                    characters.Add(character);
                }
#if SERVER_IS_TRAITOR
                if (server.Character != null)
                {
                    traitorCandidates.Add(server.Character);
                }
                else
#endif
                {
                    traitorCandidates.AddRange(server.ConnectedClients.FindAll(c => c.Character != null).ConvertAll(client => client.Character));
                }
#if !ALLOW_SOLO_TRAITOR
                if (characters.Count < 2)
                {
                    return;
                }
#endif
                CodeWords = ToolBox.GetRandomLine(wordsTxt) + ", " + ToolBox.GetRandomLine(wordsTxt);
                CodeResponse = ToolBox.GetRandomLine(wordsTxt) + ", " + ToolBox.GetRandomLine(wordsTxt);
                Traitors.Clear();
                foreach (var role in traitorRoles)
                {

                    int traitorIndex = Random(traitorCandidates.Count);
                    Character traitorCharacter = traitorCandidates[traitorIndex];
                    traitorCandidates.Remove(traitorCharacter);

                    var traitor = new Traitor(this, role, traitorCharacter);
                    Traitors.Add(role, traitor);
                }
                Update(0.0f);
                foreach (var traitor in Traitors.Values)
                {
                    traitor.Greet(server, CodeWords, CodeResponse);
                }
#if SERVER
                foreach (var traitor in Traitors.Values)
                {
                    GameServer.Log(string.Format("{0} is the traitor and the current goals are:\n{1}", traitor.Character.Name, traitor.CurrentObjective?.GoalInfos != null ? TextManager.GetServerMessage(traitor.CurrentObjective?.GoalInfos) : "(empty)"), ServerLog.MessageType.ServerMessage);
                }
#endif
            }

            public virtual void Update(float deltaTime)
            {
                if (pendingObjectives.Count <= 0 || Traitors.Count <= 0)
                {
                    return;
                }
                int previousCompletedCount = completedObjectives.Count;
                int startedCount = 0;
                while (pendingObjectives.Count > 0)
                {
                    var objective = pendingObjectives[0];
                    if (!objective.IsStarted)
                    {
                        if (!objective.Start(Traitors["traitor"]))
                        {
                            pendingObjectives.RemoveAt(0);
                            completedObjectives.Add(objective);
                            if (pendingObjectives.Count > 0)
                            {
                                objective.EndMessage();
                            }
                            continue;
                        }
                        ++startedCount;
                    }
                    objective.Update(deltaTime);
                    if (objective.IsCompleted)
                    {
                        pendingObjectives.RemoveAt(0);
                        completedObjectives.Add(objective);
                        if (pendingObjectives.Count > 0)
                        {
                            objective.EndMessage();
                        }
                        continue;
                    }
                    if (!objective.CanBeCompleted)
                    {
                        objective.EndMessage();
                        objective.End(true);
                        pendingObjectives.Clear();
                    }
                    break;
                }
                int completedMax = completedObjectives.Count - 1;
                for (int i = previousCompletedCount; i <= completedMax; ++i)
                {
                    var objective = completedObjectives[i];
                    objective.End(i < completedMax || pendingObjectives.Count > 0);
                }
                if (pendingObjectives.Count > 0)
                {
                    if (startedCount > 0)
                    {
                        pendingObjectives[0].StartMessage();
                    }
                }
                else if (completedObjectives.Count >= allObjectives.Count)
                {
                    foreach (var traitor in Traitors)
                    {
                        SteamAchievementManager.OnTraitorWin(traitor.Value.Character);
                    }
                    GameMain.Server.EndGame();
                }
            }

            public delegate bool CharacterFilter(Character character);
            public Character FindKillTarget(Character traitor, CharacterFilter filter)
            {
                if (traitor == null) { return null; }

                List<Character> validCharacters = Character.CharacterList.FindAll(c => 
                    c.TeamID == traitor.TeamID && 
                    !c.IsDead && 
                    (filter == null || filter(c)));

                if (validCharacters.Count > 0)
                {
                    return validCharacters[Random(validCharacters.Count)];
                }

#if ALLOW_SOLO_TRAITOR
                return traitor;
#else
                return null;
#endif
            }

            public TraitorMission(string startText, string globalEndMessageSuccessTextId, string globalEndMessageSuccessDeadTextId, string globalEndMessageSuccessDetainedTextId, string globalEndMessageFailureTextId, string globalEndMessageFailureDeadTextId, string globalEndMessageFailureDetainedTextId, params Objective[] objectives)
            {
                StartText = startText;
                GlobalEndMessageSuccessTextId = globalEndMessageSuccessTextId;
                GlobalEndMessageSuccessDeadTextId = globalEndMessageSuccessDeadTextId;
                GlobalEndMessageSuccessDetainedTextId = globalEndMessageSuccessDetainedTextId;
                GlobalEndMessageFailureTextId = globalEndMessageFailureTextId;
                GlobalEndMessageFailureDeadTextId = globalEndMessageFailureDeadTextId;
                GlobalEndMessageFailureDetainedTextId = globalEndMessageFailureDetainedTextId;
                allObjectives.AddRange(objectives);
                pendingObjectives.AddRange(objectives);
            }
        }
    }
}
