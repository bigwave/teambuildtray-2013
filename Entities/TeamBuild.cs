using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Entities
{
	public class TeamBuild : INotifyPropertyChanged
	{
		private TeamBuildStatus status;
		[XmlIgnore()]
		public Uri BuildDefinitionUri { get; set; }

		/// <summary>
		///  From http://stackoverflow.com/questions/1594042/system-uri-implements-iserializable-but-gives-error
		/// </summary>
		[XmlElement("URI")]
		public string _URI // Unfortunately this has to be public to be xml serialized.
		{
			get { return BuildDefinitionUri.ToString(); }
			set { BuildDefinitionUri = new Uri(value); }
		}
	
		public string Name { get; set; }
		public string RequestedBy { get; set; }

		public TeamBuildStatus Status
		{
			get { return status; }
			set
			{
				if (status != value)
				{
					status = value;
					OnPropertyChanged("Status");
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void OnPropertyChanged(String propertyName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
