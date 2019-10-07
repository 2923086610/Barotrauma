﻿#region Using Statements

using System;
using System.IO;
using System.Linq;
using System.Text;
using GameAnalyticsSDK.Net;
using Barotrauma.Steam;
using System.Diagnostics;

#if WINDOWS
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
#endif

#endregion

namespace Barotrauma
{
#if WINDOWS || LINUX || OSX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        private static int restartAttempts;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            GameMain game = null;
#if !DEBUG
            try
            {
#endif
                SteamManager.Initialize();
                game = new GameMain(args);
                game.Run();
                game.Dispose();
#if !DEBUG
            }
            catch (Exception e)
            {
                try
                {
                    CrashDump(game, "crashreport.log", e);
                }
                catch (Exception e2)
                {
#if WINDOWS
                    CrashMessageBox("Barotrauma seems to have crashed, and failed to generate a crash report: "
                        + e2.Message + "\n" + e2.StackTrace.ToString(),
                        "Failed to generate crash report!");
#endif
                }
                game?.Dispose();
                return;
            }
#endif
        }

        public static void CrashMessageBox(string message, string filePath)
        {
#if WINDOWS
            MessageBox.Show(message, "Oops! Barotrauma just crashed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif

            // Open the crash log.
            Process.Start(filePath);
        }

        static void CrashDump(GameMain game, string filePath, Exception exception)
        {
            int existingFiles = 0;
            string originalFilePath = filePath;
            while (File.Exists(filePath))
            {
                existingFiles++;
                filePath = Path.GetFileNameWithoutExtension(originalFilePath) + " (" + (existingFiles + 1) + ")" + Path.GetExtension(originalFilePath);
            }

            DebugConsole.DequeueMessages();

            string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
            var md5 = System.Security.Cryptography.MD5.Create();
            Md5Hash exeHash = null;
            try
            {
                using (var stream = File.OpenRead(exePath))
                {
                    exeHash = new Md5Hash(stream);
                }
            }
            catch
            {
                //gotta catch them all, we don't want to throw an exception while writing a crash report
            }

            StreamWriter sw = new StreamWriter(filePath);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Barotrauma Client crash report (generated on " + DateTime.Now + ")");
            sb.AppendLine("\n");
            sb.AppendLine("Barotrauma seems to have crashed. Sorry for the inconvenience! ");
            sb.AppendLine("\n");
            if (exeHash?.Hash != null)
            {
                sb.AppendLine(exeHash.Hash);
            }
            sb.AppendLine("\n");
#if DEBUG
            sb.AppendLine("Game version " + GameMain.Version + " (debug build)");
#else
            sb.AppendLine("Game version " + GameMain.Version);
#endif
            if (GameMain.Config != null)
            {
                sb.AppendLine("Graphics mode: " + GameMain.Config.GraphicsWidth + "x" + GameMain.Config.GraphicsHeight + " (" + GameMain.Config.WindowMode.ToString() + ")");
            }
            if (GameMain.SelectedPackages != null)
            {
                sb.AppendLine("Selected content packages: " + (!GameMain.SelectedPackages.Any() ? "None" : string.Join(", ", GameMain.SelectedPackages.Select(c => c.Name))));
            }
            sb.AppendLine("Level seed: " + ((Level.Loaded == null) ? "no level loaded" : Level.Loaded.Seed));
            sb.AppendLine("Loaded submarine: " + ((Submarine.MainSub == null) ? "None" : Submarine.MainSub.Name + " (" + Submarine.MainSub.MD5Hash + ")"));
            sb.AppendLine("Selected screen: " + (Screen.Selected == null ? "None" : Screen.Selected.ToString()));
            if (SteamManager.IsInitialized)
            {
                sb.AppendLine("SteamManager initialized");
            }

            if (GameMain.Client != null)
            {
                sb.AppendLine("Client (" + (GameMain.Client.GameStarted ? "Round had started)" : "Round hadn't been started)"));
            }

            sb.AppendLine("\n");
            sb.AppendLine("System info:");
            sb.AppendLine("    Operating system: " + System.Environment.OSVersion + (System.Environment.Is64BitOperatingSystem ? " 64 bit" : " x86"));

            if (game == null)
            {
                sb.AppendLine("    Game not initialized");
            }
            else
            {
                if (game.GraphicsDevice == null)
                {
                    sb.AppendLine("    Graphics device not set");
                }
                else
                {
                    if (game.GraphicsDevice.Adapter == null)
                    {
                        sb.AppendLine("    Graphics adapter not set");
                    }
                    else
                    {
                        sb.AppendLine("    GPU name: " + game.GraphicsDevice.Adapter.Description);
                        sb.AppendLine("    Display mode: " + game.GraphicsDevice.Adapter.CurrentDisplayMode);
                    }

                    sb.AppendLine("    GPU status: " + game.GraphicsDevice.GraphicsDeviceStatus);
                }
            }

            sb.AppendLine("\n");
            sb.AppendLine("Exception: " + exception.Message);
            if (exception.TargetSite != null)
            {
                sb.AppendLine("Target site: " + exception.TargetSite.ToString());
            }
            sb.AppendLine("Stack trace: ");
            sb.AppendLine(exception.StackTrace);
            sb.AppendLine("\n");

            if (exception.InnerException != null)
            {
                sb.AppendLine("InnerException: " + exception.InnerException.Message);
                if (exception.InnerException.TargetSite != null)
                {
                    sb.AppendLine("Target site: " + exception.InnerException.TargetSite.ToString());
                }
                sb.AppendLine("Stack trace: ");
                sb.AppendLine(exception.InnerException.StackTrace);
            }

            sb.AppendLine("Last debug messages:");
            for (int i = DebugConsole.Messages.Count - 1; i >= 0; i--)
            {
                sb.AppendLine("[" + DebugConsole.Messages[i].Time + "] " + DebugConsole.Messages[i].Text);
            }

            string crashReport = sb.ToString();

            sw.WriteLine(crashReport);
            sw.Close();

            if (GameSettings.SaveDebugConsoleLogs) DebugConsole.SaveLogs();

            if (GameSettings.SendUserStatistics)
            {
                CrashMessageBox("A crash report (\"" + filePath + "\") was saved in the root folder of the game and sent to the developers.", filePath);
                GameAnalytics.AddErrorEvent(EGAErrorSeverity.Critical, crashReport);
                GameAnalytics.OnQuit();
            }
            else
            {
                CrashMessageBox("A crash report (\"" + filePath + "\") was saved in the root folder of the game. The error was not sent to the developers because user statistics have been disabled, but" +
                    " if you'd like to help fix this bug, you may post it on Barotrauma's GitHub issue tracker: https://github.com/Regalis11/Barotrauma/issues/", filePath);
            }
        }
    }
#endif
}
