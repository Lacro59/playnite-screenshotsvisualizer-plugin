using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Views;
using ScreenshotsVisualizer.Views.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerUI : PlayniteUiHelper
    {
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        public override string _PluginUserDataPath { get; set; } = string.Empty;

        public override bool IsFirstLoad { get; set; } = true;

        public override string BtActionBarName { get; set; } = string.Empty;
        public override FrameworkElement PART_BtActionBar { get; set; }

        public override string SpDescriptionName { get; set; } = string.Empty;
        public override FrameworkElement PART_SpDescription { get; set; }


        public override string SpInfoBarFSName { get; set; } = string.Empty;
        public override FrameworkElement PART_SpInfoBarFS { get; set; }

        public override string BtActionBarFSName { get; set; } = string.Empty;
        public override FrameworkElement PART_BtActionBarFS { get; set; }


        public override List<CustomElement> ListCustomElements { get; set; } = new List<CustomElement>();


        public ScreenshotsVisualizerUI(IPlayniteAPI PlayniteApi, ScreenshotsVisualizerSettings Settings, string PluginUserDataPath) : base(PlayniteApi, PluginUserDataPath)
        {
            _PluginUserDataPath = PluginUserDataPath;

            BtActionBarName = "PART_SsvButton";
            SpDescriptionName = "PART_SsvDescriptionIntegration";
        }



        public override void Initial()
        {

        }

        public override DispatcherOperation AddElements()
        {
            if (_PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                if (IsFirstLoad)
                {
#if DEBUG
                    logger.Debug($"ScreenshotsVisualizer - IsFirstLoad");
#endif
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                    {
                        System.Threading.SpinWait.SpinUntil(() => IntegrationUI.SearchElementByName("PART_HtmlDescription") != null, 5000);
                    })).Wait();
                    IsFirstLoad = false;
                }

                return Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    CheckTypeView();

                    if (PluginDatabase.PluginSettings.EnableIntegrationButton)
                    {
#if DEBUG
                        logger.Debug($"ScreenshotsVisualizer - AddBtActionBar()");
#endif
                        AddBtActionBar();
                    }

                    if (PluginDatabase.PluginSettings.EnableIntegrationInDescription)
                    {
#if DEBUG
                        logger.Debug($"ScreenshotsVisualizer - AddSpDescription()");
#endif
                        AddSpDescription();
                    }

                    if (PluginDatabase.PluginSettings.EnableIntegrationInCustomTheme)
                    {
#if DEBUG
                        logger.Debug($"ScreenshotsVisualizer - AddCustomElements()");
#endif
                        AddCustomElements();
                    }
                }));
            }

            return null;
        }

        public override void RefreshElements(Game GameSelected, bool force = false)
        {
#if DEBUG
            logger.Debug($"ScreenshotsVisualizer - RefreshElements({GameSelected.Name})");
#endif
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;

            Task TaskRefresh = Task.Run(() =>
            {
                try
                {
                    Initial();

                    // Reset resources
                    List<ResourcesList> resourcesLists = new List<ResourcesList>();
                    resourcesLists.Add(new ResourcesList { Key = "Ssv_HasData", Value = false });
                    resourcesLists.Add(new ResourcesList { Key = "Ssv_Count", Value = 0 });
                    resourcesLists.Add(new ResourcesList { Key = "Ssv_ListScreenshots", Value = new List<Screenshot>() });
                    resourcesLists.Add(new ResourcesList { Key = "Ssv_EnableIntegrationInCustomTheme", Value = PluginDatabase.PluginSettings.EnableIntegrationInCustomTheme });
                    ui.AddResources(resourcesLists);


                    // Load data
                    if (!PluginDatabase.IsLoaded)
                    {
                        return;
                    }
                    GameScreenshots gameScreenshots = PluginDatabase.Get(GameSelected);

                    if (gameScreenshots.HasData)
                    {
                        resourcesLists = new List<ResourcesList>();
                        resourcesLists.Add(new ResourcesList { Key = "Ssv_HasData", Value = true });
                        resourcesLists.Add(new ResourcesList { Key = "Ssv_Count", Value = gameScreenshots.Items.Count });
                        resourcesLists.Add(new ResourcesList { Key = "Ssv_ListScreenshots", Value = gameScreenshots.Items });
                    }
                    else
                    {
                        logger.Warn($"ScreenshotsVisualizer - No data for {GameSelected.Name}");
                    }

                    // If not cancel, show
                    if (!ct.IsCancellationRequested && GameSelected.Id == ScreenshotsVisualizer.GameSelected.Id)
                    {
                        ui.AddResources(resourcesLists);

                        if (_PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
                        {
                            PluginDatabase.SetCurrent(gameScreenshots);

                            if (PluginDatabase.PluginSettings.EnableTag)
                            {
                                PluginDatabase.RemoveTag(GameSelected);
                                PluginDatabase.AddTag(GameSelected);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer");
                }
            }, ct);

            taskHelper.Add(TaskRefresh, tokenSource);
        }


        #region BtActionBar
        public override void InitialBtActionBar()
        {

        }

        public override void AddBtActionBar()
        {
            if (PART_BtActionBar != null)
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_BtActionBar allready insert");
#endif
                return;
            }

            FrameworkElement BtActionBar;

            if (PluginDatabase.PluginSettings.EnableIntegrationButtonDetails)
            {
                BtActionBar = new SsvButtonDetails();
            }
            else
            {
                BtActionBar = new SsvButton();
            }

            ((Button)BtActionBar).Click += OnBtActionBarClick;

            if (!PluginDatabase.PluginSettings.EnableIntegrationInDescriptionOnlyIcon)
            {
                BtActionBar.MinWidth = 150;
            }

            BtActionBar.Name = BtActionBarName;

            try
            {
                ui.AddButtonInGameSelectedActionBarButtonOrToggleButton(BtActionBar);
                PART_BtActionBar = IntegrationUI.SearchElementByName(BtActionBarName);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
            }
        }

        public override void RefreshBtActionBar()
        {

        }


        public void OnBtActionBarClick(object sender, RoutedEventArgs e)
        {
#if DEBUG
            logger.Debug($"ScreenshotsVisualizer - OnBtActionBarClick()");
#endif
            
            var ViewExtension = new SsvScreenshotsView(_PlayniteApi, ScreenshotsVisualizer.GameSelected);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(_PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension);
            windowExtension.ShowDialog();

            if (PluginDatabase.PluginSettings.EnableIntegrationInCustomTheme || PluginDatabase.PluginSettings.EnableIntegrationInDescription)
            {
                var TaskIntegrationUI = Task.Run(() =>
                {
                    ScreenshotsVisualizer.screenshotsVisualizerUI.Initial();
                    ScreenshotsVisualizer.screenshotsVisualizerUI.taskHelper.Check();
                    var dispatcherOp = ScreenshotsVisualizer.screenshotsVisualizerUI.AddElements();
                    if (dispatcherOp != null)
                    {
                        dispatcherOp.Completed += (s, ev) => { ScreenshotsVisualizer.screenshotsVisualizerUI.RefreshElements(ScreenshotsVisualizer.GameSelected); };
                    }
                });
            }
        }

        public void OnCustomThemeButtonClick(object sender, RoutedEventArgs e)
        {
            if (PluginDatabase.PluginSettings.EnableIntegrationInCustomTheme)
            {
                string ButtonName = string.Empty;
                try
                {
                    ButtonName = ((Button)sender).Name;
                    if (ButtonName == "PART_SsvCustomButton")
                    {
#if DEBUG
                        logger.Debug($"ScreenshotsVisualizer - OnCustomThemeButtonClick()");
#endif
                        OnBtActionBarClick(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer", "OnCustomThemeButtonClick() error");
                }
            }
        }
        #endregion


        #region SpDescription
        public override void InitialSpDescription()
        {

        }

        public override void AddSpDescription()
        {
            if (PART_SpDescription != null)
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SpDescription allready insert");
#endif
                return;
            }

            try
            {
                SsvDescriptionIntegration SpDescription = new SsvDescriptionIntegration(_PlayniteApi);
                SpDescription.Name = SpDescriptionName;

                ui.AddElementInGameSelectedDescription(SpDescription, PluginDatabase.PluginSettings.IntegrationTopGameDetails, PluginDatabase.PluginSettings.IntegrationShowTitle);
                PART_SpDescription = IntegrationUI.SearchElementByName(SpDescriptionName);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
            }
        }

        public override void RefreshSpDescription()
        {

        }
        #endregion


        #region CustomElements
        public override void InitialCustomElements()
        {

        }

        public override void AddCustomElements()
        {
            if (ListCustomElements.Count > 0)
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - CustomElements allready insert - {ListCustomElements.Count}");
#endif
                return;
            }


            FrameworkElement PART_SsvButtonWithJustIcon = null;
            FrameworkElement PART_SsvButtonWithTitle = null;
            FrameworkElement PART_SsvButtonWithTitleAndDetails = null;

            FrameworkElement PART_SsvSinglePicture = null;
            FrameworkElement PART_SsvListScreenshots = null;
            FrameworkElement PART_SsvWrapScreenshots = null;

            try
            {
                PART_SsvButtonWithJustIcon = IntegrationUI.SearchElementByName("PART_SsvButtonWithJustIcon", false, true);
                PART_SsvButtonWithTitle = IntegrationUI.SearchElementByName("PART_SsvButtonWithTitle", false, true);
                PART_SsvButtonWithTitleAndDetails = IntegrationUI.SearchElementByName("PART_SsvButtonWithTitleAndDetails", false, true);

                PART_SsvSinglePicture = IntegrationUI.SearchElementByName("PART_SsvSinglePicture", false, true);
                PART_SsvListScreenshots = IntegrationUI.SearchElementByName("PART_SsvListScreenshots", false, true);
                PART_SsvWrapScreenshots = IntegrationUI.SearchElementByName("PART_SsvWrapScreenshots", false, true);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer", $"Error on find custom element");
            }

            if (PART_SsvButtonWithJustIcon != null)
            {
                PART_SsvButtonWithJustIcon = new SsvButton(true);
                ((Button)PART_SsvButtonWithJustIcon).Click += OnBtActionBarClick;
                try
                {
                    ui.AddElementInCustomTheme(PART_SsvButtonWithJustIcon, "PART_SsvButtonWithJustIcon");
                    ListCustomElements.Add(new CustomElement { ParentElementName = "PART_SsvButtonWithJustIcon", Element = PART_SsvButtonWithJustIcon });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer", "Error on AddCustomElements()");
                }
            }
            else
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SsvButtonWithJustIcon not find");
#endif
            }

            if (PART_SsvButtonWithTitle != null)
            {
                PART_SsvButtonWithTitle = new SsvButton(false);
                ((Button)PART_SsvButtonWithTitle).Click += OnBtActionBarClick;
                try
                {
                    ui.AddElementInCustomTheme(PART_SsvButtonWithTitle, "PART_SsvButtonWithTitle");
                    ListCustomElements.Add(new CustomElement { ParentElementName = "PART_SsvButtonWithTitle", Element = PART_SsvButtonWithTitle });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer", "Error on AddCustomElements()");
                }
            }
            else
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SsvButtonWithTitle not find");
#endif
            }

            if (PART_SsvButtonWithTitleAndDetails != null)
            {
                PART_SsvButtonWithTitleAndDetails = new SsvButtonDetails();
                ((Button)PART_SsvButtonWithTitleAndDetails).Click += OnBtActionBarClick;
                try
                {
                    ui.AddElementInCustomTheme(PART_SsvButtonWithTitleAndDetails, "PART_SsvButtonWithTitleAndDetails");
                    ListCustomElements.Add(new CustomElement { ParentElementName = "PART_SsvButtonWithTitleAndDetails", Element = PART_SsvButtonWithTitleAndDetails });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer", "Error on AddCustomElements()");
                }
            }
            else
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SsvButtonWithTitleAndDetails not find");
#endif
            }

            if (PART_SsvSinglePicture != null)
            {
                PART_SsvSinglePicture = new SsvSinglePicture(_PlayniteApi);
                try
                {
                    ui.AddElementInCustomTheme(PART_SsvSinglePicture, "PART_SsvSinglePicture");
                    ListCustomElements.Add(new CustomElement { ParentElementName = "PART_SsvSinglePicture", Element = PART_SsvSinglePicture });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer");
                }
            }
            else
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SsvSinglePicture not find");
#endif
            }

            if (PART_SsvListScreenshots != null)
            {
                PART_SsvListScreenshots = new SsvListScreenshots(_PlayniteApi);
                try
                {
                    ui.AddElementInCustomTheme(PART_SsvListScreenshots, "PART_SsvListScreenshots");
                    ListCustomElements.Add(new CustomElement { ParentElementName = "PART_SsvListScreenshots", Element = PART_SsvListScreenshots });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer");
                }
            }
            else
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SsvListScreenshots not find");
#endif
            }

            if (PART_SsvWrapScreenshots != null)
            {
                PART_SsvWrapScreenshots = new Ssv_WrapScreenshots(_PlayniteApi);
                try
                {
                    ui.AddElementInCustomTheme(PART_SsvWrapScreenshots, "PART_SsvWrapScreenshots");
                    ListCustomElements.Add(new CustomElement { ParentElementName = "PART_SsvWrapScreenshots", Element = PART_SsvWrapScreenshots });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer");
                }
            }
            else
            {
#if DEBUG
                logger.Debug($"ScreenshotsVisualizer - PART_SsvListScreenshots not find");
#endif
            }
        }

        public override void RefreshCustomElements()
        {

        }
        #endregion




        public override DispatcherOperation AddElementsFS()
        {
            if (_PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                if (IsFirstLoad)
                {
#if DEBUG
                    logger.Debug($"ScreenshotsVisualizer - IsFirstLoad");
#endif
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                    {
                        System.Threading.SpinWait.SpinUntil(() => IntegrationUI.SearchElementByName("PART_ButtonContext") != null, 5000);
                    })).Wait();
                    IsFirstLoad = false;
                }

                return Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    if (PluginDatabase.PluginSettings.EnableIntegrationFS)
                    {
#if DEBUG
                        logger.Debug($"ScreenshotsVisualizer - AddBtInfoBarFS()");
#endif
                        AddSpInfoBarFS();
                    }
                }));
            }

            return null;
        }


        #region SpInfoBarFS
        public override void InitialSpInfoBarFS()
        {

        }

        public override void AddSpInfoBarFS()
        {

        }

        public override void RefreshSpInfoBarFS()
        {

        }
        #endregion


        #region BtActionBarFS
        public override void InitialBtActionBarFS()
        {

        }

        public override void AddBtActionBarFS()
        {

        }

        public override void RefreshBtActionBarFS()
        {

        }
        #endregion
    }
}
