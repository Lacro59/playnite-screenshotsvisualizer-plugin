using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using CommonPluginsShared;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Clients
{
    public class Steam
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private string _PluginUserDataPath;
        private string InstallationPath;


        public Steam(string PluginUserDataPath)
        {
            _PluginUserDataPath = PluginUserDataPath;
            InstallationPath = GetInstallationPath();
        }


        private string GetSteamId()
        {
            try
            {
                if (File.Exists(_PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json"))
                {
                    JObject SteamConfig = JObject.Parse(File.ReadAllText(_PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json"));

                    SteamID steamID = new SteamID();
                    steamID.SetFromUInt64((ulong)SteamConfig["UserId"]);

                    return steamID.AccountID.ToString();
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
                return string.Empty;
            }
        }


        private string GetInstallationPath()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (key?.GetValueNames().Contains("SteamPath") == true)
                {
                    return key.GetValue("SteamPath")?.ToString().Replace('/', '\\') ?? string.Empty;
                }
            }

#if DEBUG
            return "H:\\Steam";
#else
            return string.Empty;
#endif
        }

        private string GetPathScreeshotsFolder()
        {
            string PathScreeshotsFolder = string.Empty;

            if (!InstallationPath.IsNullOrEmpty())
            {
                string SteamId = GetSteamId();

                if (SteamId.IsNullOrEmpty())
                {
                    logger.Warn("ScreenshotsVisualizer - No find SteamId");
                    return PathScreeshotsFolder;
                }


                PathScreeshotsFolder = Path.Combine(InstallationPath, "userdata", SteamId, "760", "remote");

                if (Directory.Exists(PathScreeshotsFolder))
                {
                    return PathScreeshotsFolder;
                }
                else
                {
                    logger.Warn("ScreenshotsVisualizer - Folder Steam userdata not find");
                }
            }

            logger.Warn("ScreenshotsVisualizer - No find Steam installation");
            return PathScreeshotsFolder;
        }

        public string GetGamePathScreenshotsFolder(Game game)
        {
            string GamePathScreenshotsFolder = GetPathScreeshotsFolder();

            if (!GamePathScreenshotsFolder.IsNullOrEmpty())
            {
                GamePathScreenshotsFolder = Path.Combine(GamePathScreenshotsFolder, game.GameId, "screenshots");

                if (Directory.Exists(GamePathScreenshotsFolder))
                {
                    return GamePathScreenshotsFolder;
                }
            }

            return GamePathScreenshotsFolder;
        }
    }
}
