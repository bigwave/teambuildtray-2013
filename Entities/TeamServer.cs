﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Entities
{
    public class TeamServer
    {
		private bool disposed;
		private string serverName;
		private IconColour currentIconColour;

		[XmlIgnore]
		public List<Uri> HiddenBuilds { get; set; }

		/// <summary>Gets the server status.</summary>
		/// <param name="refreshServerList">if set to <see langword="true" /> [refresh server list].</param>
		/// <returns>IconColour.</returns>
		public IconColour GetServerStatus()
		{
			bool building = false;

			if (Projects.Count == 0)
			{
				return IconColour.Grey;
			}

			foreach (TeamProject teamProject in Projects)
			{
				if (teamProject.Builds.Count == 0)
				{
					return IconColour.Grey;
				}

				foreach (TeamBuild aBuild in teamProject.Builds)
				{
					if (aBuild.Status == TeamBuildStatus.Failed)
					{
						return IconColour.Red;
					}

					if (aBuild.Status == TeamBuildStatus.InProgress)
					{
						building = true;
					}
				}
			}

			if (building)
			{
				return IconColour.Amber;
			}

			return IconColour.Green;
		}

		public IconColour GetIconColour()
		{
			foreach (TeamProject teamProject in Projects)
			{
				foreach (TeamBuild teamBuild in teamProject.Builds)
				{
					//If the icon is green and a build is failing, set it to red, only if visible
					if (!HiddenBuilds.Contains(teamBuild.BuildDefinitionUri))
					{
						if ((currentIconColour != IconColour.Amber) &&
							(teamBuild.Status == TeamBuildStatus.Failed))
						{
							return IconColour.Red;
						}
						else if (currentIconColour == IconColour.Grey)
						{
							return IconColour.Green;
						}
					}
				}
			}

			return IconColour.Grey;
		}

		/// <summary>Gets or sets the name of the server.</summary>
		/// <value>The name of the server.</value>
		public string ServerName
		{
			get
			{
				return this.serverName;
			}
			set
			{
				this.serverName = value;
			}
		}

		/// <summary>Gets or sets the port.</summary>
		/// <value>The port.</value>
		public int Port { get; set; }

		/// <summary>Gets or sets the name of the collection.</summary>
		/// <value>The name of the collection.</value>
		public string CollectionName { get; set; }

		/// <summary>Gets or sets the protocol.</summary>
		/// <value>The protocol.</value>
		public string Protocol { get; set; }

		/// <summary>Gets or sets the projects.</summary>
		/// <value>The projects.</value>
		public Collection<TeamProject> Projects { get; set; }
	}
}
