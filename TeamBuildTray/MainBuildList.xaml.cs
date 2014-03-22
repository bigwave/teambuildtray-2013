using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Threading;
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
	using Entities;
	using Actions;

    public partial class MainBuildList
    {
		/// <summary>The interval time in seconds.</summary>
#if DEBUG
		public static readonly int IntervalTimeInSeconds = 1;
#else
		public static readonly int IntervalTimeInSeconds = 30;
#endif
		private Timer queryTimer;
		private readonly Dictionary<Uri, TeamBuild> buildContent = new Dictionary<Uri, TeamBuild>();
        private readonly Queue<StatusMessage> statusMessages = new Queue<StatusMessage>(20);
		private ObservableCollection<TeamBuild> buildContentView;
        private bool exitButtonClicked;
        private IconColour currentIconColour = IconColour.Grey;
        internal static TeamServer server;
        private bool showConfiguration;
        StatusMessage lastStatusMessage = new StatusMessage { Message = string.Empty };
		private static Actions.ITfsApi myActions;

		public static Actions.ITfsApi ActionsInstance
		{
			get { return myActions; }
			set { myActions = value; }
		}

		SynchronizationContext uiContext = SynchronizationContext.Current;

        public MainBuildList()
        {

            InitializeComponent();
            SetIcon(IconColour.Grey);

            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

            //Set the main title
            LabelMainTitle.Content = ResourcesMain.MainWindow_Title;

            ButtonConfigure.ToolTip = ResourcesMain.MainWindow_ConfigureTooltip;

#if DEBUG
			myActions = new FakeTfsApi();
#else
			myActions = new Actions.TfsApi();
#endif
            LoadConfiguration(false);
        }



        /// <summary>
        /// A collection of StatusMessages that the main window can add to.
        /// </summary>
		public ObservableCollection<TeamBuild> BuildContent
        {
            get
            {
                if (buildContentView == null)
                {
					buildContentView = new ObservableCollection<TeamBuild>();
                }
                return buildContentView;
            }
        }

		private void LoadConfiguration(bool reconfigure)
        {
            if (reconfigure)
            {
                buildContent.Clear();
                lastStatusMessage = new StatusMessage { Message = "" };
                currentIconColour = IconColour.Grey;
                statusMessages.Clear();
                buildContentView.Clear();
            }

            //StatusMessage initializing = new StatusMessage { Message = "Initializing..." };
            //MessageWindow.Show(initializing, 3000);

			server = Actions.Config.LoadServerFromConfigurationFile(ServerConfigurationPath);

                    if (server == null )
                    {
                        RunConfigurationWindow();
						server = Actions.Config.LoadServerFromConfigurationFile(ServerConfigurationPath);
                    }

			queryTimer = new Timer(this.QueryTimerElapsed, null, new TimeSpan(0), new TimeSpan(0, 0, IntervalTimeInSeconds));


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

            InitializeServers();
			QueryTimerElapsed(server);
		}

		private void QueryTimerElapsed(object sender)
		{
			myActions.QueryBuilds(server);
			currentIconColour = server.GetServerStatus();
			uiContext.Send(x => SetIcon(currentIconColour), null);
			//Get current build item list
			foreach (TeamProject teamProject in server.Projects)
			{
				foreach (TeamBuild item in teamProject.Builds)
				{
					//If the item doesn't exist or needs updating

					if (!buildContent.ContainsKey(item.BuildDefinitionUri))
					{
						buildContent.Add(item.BuildDefinitionUri, item);

						//Add to view if not hidden
						if (!server.HiddenBuilds.Contains(item.BuildDefinitionUri))
						{
							uiContext.Send(x => buildContentView.Add(item), null);
						}
					}
					else //Update the item
					{
						uiContext.Send(x => buildContent[item.BuildDefinitionUri] = item, null);
					}

					// Send alerts for changes
					AlertChange(item);
				}
			}

		}

		internal void AlertChange(TeamBuild theBuild)
		{
			var message = new StatusMessage
			{
				EventDate = DateTime.Now,
				BuildStatus = IconColour.Amber,
				Message =
					String.Format(
									CultureInfo.CurrentUICulture,
									ResourcesMain.NotifierWindow_InProgress,
									theBuild.RequestedBy,
									theBuild.Name)
			};

			statusMessages.Enqueue(message);

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

		////private void AlertChanges(BuildQueryEventArgs buildQueryEventArgs)
		////{
		////	bool iconChanged = false;
		////	IconColour mainIconColour = IconColour.Green;

		////	//Cleanup history
		////	CleanupIds();

		////	//Find in progress builds
		////	foreach (IQueuedBuildQueryResult queueResult in buildQueryEventArgs.BuildQueueQueryResults)
		////	{
		////		//Check for cancelled builds
		////		IconColour? iconColour = CheckCancelledBuilds(queueResult);
		////		if (iconColour.HasValue)
		////		{
		////			mainIconColour = iconColour.Value;
		////			iconChanged = true;
		////		}

		////		// Loop through builds with active histories
		////		foreach (IQueuedBuild build in queueResult.QueuedBuilds.OrderBy(item => item.Id))
		////		{
		////			// Check if build is hidden
		////			if (HiddenBuilds.Contains(build.BuildDefinitionUri))
		////			{
		////				continue;
		////			}

		////			// Get the friendly names
		////			string buildName = build.BuildDefinition.Name;


		////			// Adding builds while the tray is running can cause it to fail, only builds which have atleast 1 successfull build will be displayed.
		////			if (!String.IsNullOrEmpty(buildName))
		////			{
		////				// Check if this is an "In Progress" status and has not been displayed before
		////				if ((!buildIdsAlertedInProgress.Contains(build.BuildDefinition.Name)) && (build.Status == QueueStatus.InProgress))
		////				{
		////					mainIconColour = IconColour.Amber;
		////					iconChanged = true;
		////					buildIdsAlertedInProgress.Add(build.BuildDefinition.Name);
		////					buildIdUris.Add(build.BuildDefinition.Name, build.BuildDefinitionUri);
		////					NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title + " - Building";


		////					UpdateMainWindowItem(build.BuildDefinitionUri, BuildStatus.InProgress, build.RequestedBy);
		////				} //Check if this is an "Queued" status and has not been displayed before
		////				else if ((!buildIdsAlertedQueued.Contains(build.BuildDefinition.Name)) && (build.Status == QueueStatus.Queued))
		////				{
		////					StatusMessage message = new StatusMessage
		////												{
		////													EventDate = DateTime.Now,
		////													BuildStatus = IconColour.Amber,
		////													Message =
		////														String.Format(CultureInfo.CurrentUICulture,
		////																	  ResourcesMain.NotifierWindow_Queued,
		////																	  build.RequestedBy, buildName)
		////												};
		////					statusMessages.Enqueue(message);
		////					buildIdsAlertedQueued.Add(build.BuildDefinition.Name);
		////				}//Check if this is an "Completed" status and has not been displayed before
		////				else if ((!buildIdsAlertedDone.Contains(build.BuildDefinition.Name)) && (build.Status == QueueStatus.Completed))
		////				{
		////					StatusMessage message = new StatusMessage
		////												{
		////													EventDate = DateTime.Now
		////												};

		////					buildIdsAlertedInProgress.Remove(build.BuildDefinition.Name);
		////					buildIdUris.Remove(build.BuildDefinition.Name);

		////					//Get the status from the build log
		////					foreach (IBuildDetail item in buildContent)
		////					{
		////						if (item.BuildDefinitionUri == build.BuildDefinitionUri)
		////						{
		////							switch (item.Status)
		////							{
		////								case BuildStatus.Succeeded:
		////									message.BuildStatus = IconColour.Green;
		////									message.Message = String.Format(
		////																	CultureInfo.CurrentUICulture,
		////																	ResourcesMain.NotifierWindow_BuildPassed,
		////																	buildName);
		////									break;
		////								case BuildStatus.PartiallySucceeded:
		////									message.BuildStatus = IconColour.Red;
		////									message.Message = String.Format(
		////																	CultureInfo.CurrentUICulture,
		////																	ResourcesMain.NotifierWindow_BuildPartial,
		////																	buildName);
		////									break;
		////								default:
		////									message.BuildStatus = IconColour.Red;
		////									message.Message = String.Format(CultureInfo.CurrentUICulture,
		////																	ResourcesMain.NotifierWindow_FailedBuild,
		////																	build.RequestedFor, buildName);
		////									break;
		////							}
 
		////							message.HyperlinkUri = new Uri(item.LogLocation);
		////							mainIconColour = message.BuildStatus;
		////							iconChanged = true;
		////							break;
		////						}
		////					}

		////					NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

		////					statusMessages.Enqueue(message);
		////					buildIdsAlertedDone.Add(build.BuildDefinition.Name);

		////				}
		////			}
		////		}
		////	}

		////	//Only pop up if new messages
		////	if (statusMessages.Count > 0)
		////	{
		////		lastStatusMessage = statusMessages.Dequeue();
		////		MessageWindow.Show(lastStatusMessage, 3000);
		////	}
		////	// Only update the main icon if its a valid status change
		////	if (iconChanged)
		////	{
		////		SetIcon(mainIconColour);
		////	}
		////}

        private void UpdateMainWindowItem(Uri buildDefinitionUri, TeamBuildStatus status, string requestedBy)
        {
            foreach (TeamBuild build in buildContent.Values)
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
			IconColour serverStatus = server.GetIconColour();
                if (serverStatus < iconColour)
                {
                    iconColour = serverStatus;
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
            Uri iconUri = new Uri("pack://application:,,,/Resources/" + iconColour + ".ico", UriKind.RelativeOrAbsolute);
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
			Actions.Config.SaveHiddenBuilds(BuildListConfigurationPath, server);
        }

		/// <summary>Gets the application configuration path.</summary>
		/// <value>The application configuration path.</value>
		public static string ApplicationConfigurationPath
		{
			get
			{
				string applicationDataPath =
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamBuildTray");
				if (!Directory.Exists(applicationDataPath))
				{
					Directory.CreateDirectory(applicationDataPath);
				}

				return applicationDataPath;
			}
		}

		/// <summary>Gets the server configuration path.</summary>
		/// <value>The server configuration path.</value>
		public static string ServerConfigurationPath
		{
			get
			{
				return Path.Combine(ApplicationConfigurationPath, "servers.xml");
			}
		}

		/// <summary>Gets the build list configuration path.</summary>
		/// <value>The build list configuration path.</value>
		public static string BuildListConfigurationPath
		{
			get
			{
				return Path.Combine(ApplicationConfigurationPath, "hiddenbuilds.xml");
			}
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
			//////Clear up servers
			////	server.Dispose();

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

        private void BorderMenuForceBuild_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var detail = menuItem.Tag as TeamBuild;
                if (detail != null)
                {
						////if (server.GetDefinitionByUri(detail.BuildDefinitionUri) != null)
						////{
						////	// extract the drop location
						////	server.QueueBuild(detail.BuildControllerUri, detail.BuildDefinitionUri);
						////	break;
						////}
                }
            }
        }

        private void CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                lock (server.HiddenBuilds)
                {
					TeamBuild detail = checkBox.Tag as TeamBuild;
                    if (detail != null)
                    {
                        if ((checkBox.IsChecked.HasValue) && (checkBox.IsChecked.Value))
                        {
							if (server.HiddenBuilds.Contains(detail.BuildDefinitionUri))
                            {
								server.HiddenBuilds.Remove(detail.BuildDefinitionUri);
                            }
                        }
                        else
                        {
							if (!server.HiddenBuilds.Contains(detail.BuildDefinitionUri))
                            {
								server.HiddenBuilds.Add(detail.BuildDefinitionUri);
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
				foreach (TeamBuild detail in buildContent.Values)
                {
                    buildContentView.Add(detail);
                }
            }
            else
            {
                ScrollViewerBuildList.ContentTemplate = FindResource("BuildContentTemplateIcons") as DataTemplate;

				foreach (TeamBuild detail in new List<TeamBuild>(buildContentView))
                {
                    if (server.HiddenBuilds.Contains(detail.BuildDefinitionUri))
                    {
                        buildContentView.Remove(detail);
                    }
                }
            }
        }
    }
}
