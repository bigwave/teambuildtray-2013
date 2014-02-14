using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using System.Globalization;
using TeamBuildTray.Resources;
using System.Reflection;

namespace TeamBuildTray
{
    using Microsoft.TeamFoundation.Build.Client;

    public partial class MainBuildList
    {
        private static List<Uri> hiddenFields;
        private readonly List<IBuildDetail> buildContent = new List<IBuildDetail>();
        private readonly Dictionary<Uri, DateTime> buildUpdates = new Dictionary<Uri, DateTime>();
        private readonly List<string> buildIdsAlertedInProgress = new List<string>();
        private readonly Dictionary<string, Uri> buildIdUris = new Dictionary<string, Uri>();
        private readonly List<string> buildIdsAlertedQueued = new List<string>();
        private readonly List<string> buildIdsAlertedDone = new List<string>();
        private readonly Queue<StatusMessage> statusMessages = new Queue<StatusMessage>(20);
        private ObservableCollection<IBuildDetail> buildContentView;
        private bool exitButtonClicked;
        private IconColour currentIconColour = IconColour.Grey;
        private List<TeamServer> servers;
        private bool showConfiguration;
        StatusMessage lastStatusMessage = new StatusMessage { Message = string.Empty };

        internal static List<Uri> HiddenBuilds
        {
            get
            {
                if (hiddenFields == null)
                {
                    LoadHiddenBuilds();
                }

                return hiddenFields;
            }
        }

        public MainBuildList()
        {

            InitializeComponent();
            SetIcon(IconColour.Grey);

            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

            //Set the main title
            LabelMainTitle.Content = ResourcesMain.MainWindow_Title;

            ButtonConfigure.ToolTip = ResourcesMain.MainWindow_ConfigureTooltip;
            ButtonClose.ToolTip = ResourcesMain.MainWindow_CloseTooltip;

            LoadConfiguration(false);
        }



        /// <summary>
        /// A collection of StatusMessages that the main window can add to.
        /// </summary>
        public ObservableCollection<IBuildDetail> BuildContent
        {
            get
            {
                if (buildContentView == null)
                {
                    buildContentView = new ObservableCollection<IBuildDetail>();
                }
                return buildContentView;
            }
        }

        private void LoadConfiguration(bool reconfigure)
        {
            if (reconfigure)
            {
                buildUpdates.Clear();
                buildContent.Clear();
                lastStatusMessage = new StatusMessage { Message = "" };
                buildIdsAlertedInProgress.Clear();
                buildIdUris.Clear();
                buildIdsAlertedQueued.Clear();
                buildIdsAlertedDone.Clear();
                currentIconColour = IconColour.Grey;
                statusMessages.Clear();
                buildContentView.Clear();
                foreach (var server in servers)
                {
                    server.Dispose();
                }
            }

            //StatusMessage initializing = new StatusMessage { Message = "Initializing..." };
            //MessageWindow.Show(initializing, 3000);

            if (File.Exists(TeamServer.ServerConfigurationPath))
            {
                try
                {
                    servers = GetServersFromConfigurationFile();

                    if (servers == null || servers.Count == 0)
                    {
                        RunConfigurationWindow();
                        servers = GetServersFromConfigurationFile();
                    }
                }
                catch (Exception)
                {
                    servers = new List<TeamServer>();
                }
            }
            else
            {
                RunConfigurationWindow();
                servers = GetServersFromConfigurationFile();
            }


            //Add version as menu item
            if (!reconfigure)
            {
                MenuItem versionMenuItem = new MenuItem
                                               {
                                                   Header =
                                                       "Version : " + Assembly.GetExecutingAssembly().GetName().Version
                                               };
                NotifyIconMainIcon.ContextMenu.Items.Insert(0, versionMenuItem);
                NotifyIconMainIcon.ContextMenu.Items.Insert(1, new Separator());

                //Add Reconfigure option into menu
                MenuItem reconfigureMenuItem = new MenuItem { Header = "Change Servers" };
                reconfigureMenuItem.Click += reconfigureMenuItem_Click;
                NotifyIconMainIcon.ContextMenu.Items.Insert(2, reconfigureMenuItem);
            }

            //Attach to server events
            foreach (TeamServer server in servers)
            {
                server.OnProjectsUpdated += Server_OnProjectsUpdated;
            }

            //Open xml file of builds to hide
            LoadHiddenBuilds();

            InitializeServers();
        }

        private void reconfigureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FirstRunConfiguration firstRun = new FirstRunConfiguration { Reconfigure = true };
            bool? result = firstRun.ShowDialog();

            if ((result.HasValue) && (result.Value))
            {
                LoadConfiguration(true);
            }
        }

        /// <summary>
        /// Runs the first-run configuration window which lets the user specify a server to connect to.
        /// </summary>
        private void RunConfigurationWindow()
        {
            FirstRunConfiguration firstRun = new FirstRunConfiguration();
            bool? firstRunHasRun = firstRun.ShowDialog();

            //If the user pressed cancel or closed the window, don't let them continue.
            if (firstRunHasRun.HasValue && firstRunHasRun.Value == false)
            {
                Close();
                Environment.Exit(0);
            }
        }


        /// <summary>
        /// Opens the servers.xml file and gets the team servers out.
        /// </summary>
        /// <returns></returns>
        private static List<TeamServer> GetServersFromConfigurationFile()
        {
            return TeamServer.GetTeamServerList();
        }

        private static void LoadHiddenBuilds()
        {
            //Load hidden builds
            if (File.Exists(TeamServer.BuildListConfigurationPath))
            {
                try
                {
                    hiddenFields = TeamServer.GetHiddenBuilds() ?? new List<Uri>();
                }
                catch (Exception)
                {
                    hiddenFields = new List<Uri>();
                }
            }
            else
            {
                hiddenFields = new List<Uri>();
            }
        }

        private void Server_OnProjectsUpdated(object sender, BuildQueryEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    TeamProject project = sender as TeamProject;
                    if (project != null)
                    {
                        lock (buildContent)
                        {
                            //Get current build item list
                            Dictionary<Uri, IBuildDetail> currentBuilds = new Dictionary<Uri, IBuildDetail>();
                            foreach (IBuildDetail item in buildContent)
                            {
                                currentBuilds.Add(item.BuildDefinitionUri, item);
                            }
                            //Get updated builds
                            var query = from buildQueryItem in e.BuildQueryResults
                                        where buildQueryItem.Builds.Count() > 0
                                        orderby buildQueryItem.Builds[0].BuildNumber
                                        select buildQueryItem;

                            foreach (IBuildQueryResult buildQuery in query)
                            {
                                foreach (IBuildDetail item in buildQuery.Builds)
                                {
                                    //If the item doesnt exist or needs updating
                                    if ((currentBuilds.ContainsKey(item.BuildDefinitionUri) && (buildUpdates[item.BuildDefinitionUri] < item.LastChangedOn))
                                        || (!currentBuilds.ContainsKey(item.BuildDefinitionUri)))
                                    {
                                        //Update the last time
                                        if (buildUpdates.ContainsKey(item.BuildDefinitionUri))
                                        {
                                            buildUpdates[item.BuildDefinitionUri] = item.LastChangedOn;
                                        }
                                        else
                                        {
                                            buildUpdates.Add(item.BuildDefinitionUri, item.LastChangedOn);
                                        }

                                        //Add if doesn't exist
                                        if (!currentBuilds.ContainsKey(item.BuildDefinitionUri))
                                        {
                                            currentBuilds.Add(item.BuildDefinitionUri, item);
                                            buildContent.Add(item);

                                            //Add to view if not hidden
                                            if (!HiddenBuilds.Contains(item.BuildDefinitionUri))
                                            {
                                                buildContentView.Add(item);
                                            }
                                        }
                                        else //Update the item
                                        {
                                            currentBuilds[item.BuildDefinitionUri].Status = item.Status;
                                            ////currentBuilds[item.BuildDefinitionUri].RequestedFor = item.RequestedFor;
                                            currentBuilds[item.BuildDefinitionUri].LogLocation = item.LogLocation;
                                        }

                                        //If the icon is green and a build is failing, set it to red, only if visible
                                        if (!HiddenBuilds.Contains(item.BuildDefinitionUri))
                                        {
                                            if ((currentIconColour != IconColour.Amber) &&
                                                (item.Status == BuildStatus.Failed))
                                            {
                                                SetIcon(IconColour.Red);
                                            }
                                            else if (currentIconColour == IconColour.Grey)
                                            {
                                                SetIcon(IconColour.Green);
                                            }
                                        }

                                        // Update the name to a friendly name
                                        foreach (TeamServer server in servers)
                                        {
                                            string buildName = server.GetDefinitionByUri(item.BuildDefinitionUri).Name;
                                            if (!String.IsNullOrEmpty(buildName))
                                            {
                                                currentBuilds[item.BuildDefinitionUri].BuildNumber = buildName;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Send alerts for changes
                        AlertChanges(e);
                    }
                }));
        }

        private void AlertChanges(BuildQueryEventArgs buildQueryEventArgs)
        {
            bool iconChanged = false;
            IconColour mainIconColour = IconColour.Green;

            //Cleanup history
            CleanupIds();

            //Find in progress builds
            foreach (IQueuedBuildQueryResult queueResult in buildQueryEventArgs.BuildQueueQueryResults)
            {
                //Check for cancelled builds
                IconColour? iconColour = CheckCancelledBuilds(queueResult);
                if (iconColour.HasValue)
                {
                    mainIconColour = iconColour.Value;
                    iconChanged = true;
                }

                // Loop through builds with active histories
                foreach (IQueuedBuild build in queueResult.QueuedBuilds.OrderBy(item => item.Id))
                {
                    // Check if build is hidden
                    if (HiddenBuilds.Contains(build.BuildDefinitionUri))
                    {
                        continue;
                    }

                    // Get the friendly names
                    string buildName = build.BuildDefinition.Name;


                    // Adding builds while the tray is running can cause it to fail, only builds which have atleast 1 successfull build will be displayed.
                    if (!String.IsNullOrEmpty(buildName))
                    {
                        // Check if this is an "In Progress" status and has not been displayed before
                        if ((!buildIdsAlertedInProgress.Contains(build.BuildDefinition.Name)) && (build.Status == QueueStatus.InProgress))
                        {
                            var message = new StatusMessage
                                                        {
                                                            EventDate = DateTime.Now,
                                                            BuildStatus = IconColour.Amber,
                                                            Message =
                                                                String.Format(
                                                                              CultureInfo.CurrentUICulture,
                                                                              ResourcesMain.NotifierWindow_InProgress,
                                                                              build.RequestedBy, 
                                                                              buildName)
                                                        };

                            statusMessages.Enqueue(message);
                            mainIconColour = IconColour.Amber;
                            iconChanged = true;
                            buildIdsAlertedInProgress.Add(build.BuildDefinition.Name);
                            buildIdUris.Add(build.BuildDefinition.Name, build.BuildDefinitionUri);
                            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title + " - Building";


                            UpdateMainWindowItem(build.BuildDefinitionUri, BuildStatus.InProgress, build.RequestedBy);
                        } //Check if this is an "Queued" status and has not been displayed before
                        else if ((!buildIdsAlertedQueued.Contains(build.BuildDefinition.Name)) && (build.Status == QueueStatus.Queued))
                        {
                            StatusMessage message = new StatusMessage
                                                        {
                                                            EventDate = DateTime.Now,
                                                            BuildStatus = IconColour.Amber,
                                                            Message =
                                                                String.Format(CultureInfo.CurrentUICulture,
                                                                              ResourcesMain.NotifierWindow_Queued,
                                                                              build.RequestedBy, buildName)
                                                        };
                            statusMessages.Enqueue(message);
                            buildIdsAlertedQueued.Add(build.BuildDefinition.Name);
                        }//Check if this is an "Completed" status and has not been displayed before
                        else if ((!buildIdsAlertedDone.Contains(build.BuildDefinition.Name)) && (build.Status == QueueStatus.Completed))
                        {
                            StatusMessage message = new StatusMessage
                                                        {
                                                            EventDate = DateTime.Now
                                                        };

                            buildIdsAlertedInProgress.Remove(build.BuildDefinition.Name);
                            buildIdUris.Remove(build.BuildDefinition.Name);

                            //Get the status from the build log
                            foreach (IBuildDetail item in buildContent)
                            {
                                if (item.BuildDefinitionUri == build.BuildDefinitionUri)
                                {
                                    switch (item.Status)
                                    {
                                        case BuildStatus.Succeeded:
                                            message.BuildStatus = IconColour.Green;
                                            message.Message = String.Format(
                                                                            CultureInfo.CurrentUICulture,
                                                                            ResourcesMain.NotifierWindow_BuildPassed,
                                                                            buildName);
                                            break;
                                        case BuildStatus.PartiallySucceeded:
                                            message.BuildStatus = IconColour.Red;
                                            message.Message = String.Format(
                                                                            CultureInfo.CurrentUICulture,
                                                                            ResourcesMain.NotifierWindow_BuildPartial,
                                                                            buildName);
                                            break;
                                        default:
                                            message.BuildStatus = IconColour.Red;
                                            message.Message = String.Format(CultureInfo.CurrentUICulture,
                                                                            ResourcesMain.NotifierWindow_FailedBuild,
                                                                            build.RequestedFor, buildName);
                                            break;
                                    }
 
                                    message.HyperlinkUri = new Uri(item.LogLocation);
                                    mainIconColour = message.BuildStatus;
                                    iconChanged = true;
                                    break;
                                }
                            }

                            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

                            statusMessages.Enqueue(message);
                            buildIdsAlertedDone.Add(build.BuildDefinition.Name);

                        }
                    }
                }
            }

            //Only pop up if new messages
            if (statusMessages.Count > 0)
            {
                lastStatusMessage = statusMessages.Dequeue();
                MessageWindow.Show(lastStatusMessage, 3000);
            }
            // Only update the main icon if its a valid status change
            if (iconChanged)
            {
                SetIcon(mainIconColour);
            }
        }

        private string GetFriendlyName(Uri buildDefinitionUri)
        {
            string buildName = String.Empty;
            foreach (TeamServer server in servers)
            {
                IBuildDefinition definition = server.GetDefinitionByUri(buildDefinitionUri);
                if (definition != null)
                {
                    buildName = definition.Name;
                    if (!String.IsNullOrEmpty(buildName))
                    {
                        break;
                    }
                }
            }

            return buildName;
        }

        /// <summary>Checks the Cancelled builds.</summary>
        /// <param name="result">The result.</param>
        /// <returns>The colour for the icon.</returns>
        private IconColour? CheckCancelledBuilds(IQueuedBuildQueryResult result)
        {
            IconColour? returnColour = null;

            IEnumerable<string> buildIdHistories = from build in result.QueuedBuilds select build.BuildDefinition.Name;
            List<string> cancelledBuilds = new List<string>();
            foreach (string buildId in buildIdsAlertedInProgress)
            {
                if (!buildIdHistories.Contains(buildId))
                {
                    //Build was cancelled
                    cancelledBuilds.Add(buildId);
                }
            }

            foreach (string buildId in cancelledBuilds)
            {
                buildIdsAlertedInProgress.Remove(buildId);
                Uri uri = buildIdUris[buildId];
                buildIdUris.Remove(buildId);

                StatusMessage message = new StatusMessage
                {
                    EventDate = DateTime.Now,
                    BuildStatus = IconColour.Green,
                    Message = String.Format(CultureInfo.CurrentUICulture, ResourcesMain.NotifierWindow_Stopped, GetFriendlyName(uri))
                };

                statusMessages.Enqueue(message);
                returnColour = IconColour.Red;
                NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

                UpdateMainWindowItem(uri, BuildStatus.Failed, String.Empty);
            }

            return returnColour;
        }

        /// <summary>
        /// Cleans up the already done queues to save memory
        /// </summary>
        private void CleanupIds()
        {
            lock (buildIdsAlertedDone)
            {
                while (buildIdsAlertedDone.Count > 50)
                {
                    buildIdsAlertedDone.RemoveAt(0);
                }
            }
            lock (buildIdsAlertedInProgress)
            {
                while (buildIdsAlertedInProgress.Count > 50)
                {
                    buildIdsAlertedInProgress.RemoveAt(0);
                }
            }
            lock (buildIdsAlertedQueued)
            {
                while (buildIdsAlertedQueued.Count > 50)
                {
                    buildIdsAlertedQueued.RemoveAt(0);
                }
            }
        }

        private void UpdateMainWindowItem(Uri buildDefinitionUri, BuildStatus status, string requestedBy)
        {
            foreach (IBuildDetail build in buildContent)
            {
                if (build.BuildDefinitionUri == buildDefinitionUri)
                {
                    build.Status = status;
                    if (!String.IsNullOrEmpty(requestedBy))
                    {
                        ////build.RequestedFor = requestedBy;
                    }

                    break;
                }
            }
        }

        private void InitializeServers()
        {
            IconColour iconColour = IconColour.Grey;

            foreach (TeamServer server in servers)
            {
                IconColour serverStatus = server.GetServerStatus(true);
                if (serverStatus < iconColour)
                {
                    iconColour = serverStatus;
                }
            }

            switch (iconColour)
            {
                case IconColour.Grey:
                    MessageWindow.Show(new StatusMessage { BuildStatus = IconColour.Grey, Message = ResourcesMain.NotifierWindow_NoDefinitions }, 3000);
                    //notifierWindow.Notify();
                    break;
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        internal void SetIcon(IconColour iconColour)
        {
            currentIconColour = iconColour;
            Uri iconUri = new Uri("pack://application:,,,/" + iconColour + ".ico", UriKind.RelativeOrAbsolute);
            NotifyIconMainIcon.Icon = BitmapFrame.Create(iconUri);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!exitButtonClicked)
            {
                // Don't close, just Hide.
                e.Cancel = true;
                // Trying to hide
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                                           (DispatcherOperationCallback)delegate
                                                                                             {
                                                                                                 Hide();
                                                                                                 return null;
                                                                                             }, null);
                return;
            }

            // Actually closing window.
            NotifyIconMainIcon.Visibility = Visibility.Collapsed;

            // Save the hidden builds list
            List<string> stringUris = HiddenBuilds.Select(build => build.ToString()).ToList<string>();
            var serializer = new XmlSerializer(typeof(List<string>));
            FileStream fs = File.Open(
                                        TeamServer.BuildListConfigurationPath, 
                                        FileMode.Create, 
                                        FileAccess.Write,
                                        FileShare.ReadWrite);
            serializer.Serialize(fs, stringUris);
            fs.Close();
        }

        private void NotifyIconMainIcon_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Show();
                Activate();
            }
        }

        private void NotifyIconOpen_Click(object sender, RoutedEventArgs e)
        {
            Show();
            Activate();
        }

        private void NotifyIconOpenNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(lastStatusMessage.Message))
            {
                MessageWindow.Show(lastStatusMessage, 3000);
            }

        }

        private void NotifyIconExit_Click(object sender, RoutedEventArgs e)
        {
            //Cleare up servers
            foreach (TeamServer server in servers)
            {
                server.Dispose();
            }

            // Close this window.
            exitButtonClicked = true;
            Close();
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;

            if (border != null) border.Background = new SolidColorBrush(Color.FromArgb(31, 0, 0, 0));
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;

            if (border != null) border.Background = null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BorderMenuForceBuild_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var detail = menuItem.Tag as IBuildDetail;
                if (detail != null)
                {
                    foreach (TeamServer server in servers)
                    {
                        if (server.GetDefinitionByUri(detail.BuildDefinitionUri) != null)
                        {
                            // extract the drop location
                            server.QueueBuild(detail.BuildControllerUri, detail.BuildDefinitionUri);
                            break;
                        }
                    }
                }
            }
        }

        private void CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                lock (HiddenBuilds)
                {
                    IBuildDetail detail = checkBox.Tag as IBuildDetail;
                    if (detail != null)
                    {
                        if ((checkBox.IsChecked.HasValue) && (checkBox.IsChecked.Value))
                        {
                            if (HiddenBuilds.Contains(detail.BuildDefinitionUri))
                            {
                                HiddenBuilds.Remove(detail.BuildDefinitionUri);
                            }
                        }
                        else
                        {
                            if (!HiddenBuilds.Contains(detail.BuildDefinitionUri))
                            {
                                HiddenBuilds.Add(detail.BuildDefinitionUri);
                            }
                        }
                    }
                }
            }
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            showConfiguration = !showConfiguration;
            if (showConfiguration)
            {
                ScrollViewerBuildList.ContentTemplate = FindResource("BuildContentTemplateConfigure") as DataTemplate;

                buildContentView.Clear();
                foreach (IBuildDetail detail in buildContent)
                {
                    buildContentView.Add(detail);
                }
            }
            else
            {
                ScrollViewerBuildList.ContentTemplate = FindResource("BuildContentTemplate") as DataTemplate;

                foreach (IBuildDetail detail in new List<IBuildDetail>(buildContentView))
                {
                    if (HiddenBuilds.Contains(detail.BuildDefinitionUri))
                    {
                        buildContentView.Remove(detail);
                    }
                }
            }
        }
    }
}
