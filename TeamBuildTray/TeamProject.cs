using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using System.Globalization;

namespace TeamBuildTray
{
    using Microsoft.TeamFoundation.Build.Client;

    /// <summary>Class TeamProject.</summary>
    public class TeamProject
    {
        private readonly Collection<IBuildAgent> buildAgents = new Collection<IBuildAgent>();
        private readonly Dictionary<Uri, IBuildDefinition> buildDefinitions = new Dictionary<Uri, IBuildDefinition>();

        /// <summary>Gets or sets the name of the project.</summary>
        /// <value>The name of the project.</value>
        public string ProjectName 
        { 
            get; 
            set; 
        }

        /// <summary>Gets the build definitions.</summary>
        /// <value>The build definitions.</value>
        [XmlIgnore]
        public Dictionary<Uri, IBuildDefinition> BuildDefinitions
        {
            get 
            { 
                return buildDefinitions; 
            }
        }

        /// <summary>Gets the build agents.</summary>
        /// <value>The build agents.</value>
        [XmlIgnore]
        public Collection<IBuildAgent> BuildAgents
        {
            get 
            { 
                return buildAgents; 
            }
        }

        /// <summary>Gets the agent messages.</summary>
        /// <param name="since">The since.</param>
        /// <returns>The agents messages.</returns>
        public Dictionary<DateTime, StatusMessage> GetAgentMessages(DateTime since)
        {
            var messages = new Dictionary<DateTime, StatusMessage>();
            foreach (IBuildAgent agent in buildAgents)
            {
                int splitLocation = agent.StatusMessage.LastIndexOf(" on ", StringComparison.OrdinalIgnoreCase);
                string message = agent.StatusMessage.Substring(0, splitLocation);
                DateTime date = DateTime.Parse(agent.StatusMessage.Substring(splitLocation + 4), CultureInfo.CurrentCulture);

                if (date >= since)
                {
                    messages.Add(date, new StatusMessage {EventDate = date, Message = message});
                }
            }

            return messages;
        }
    }
}