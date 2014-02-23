namespace TeamBuildTray
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    using Microsoft.TeamFoundation.Build.Client;

    /// <summary>The build status converter.</summary>
    [ValueConversion(typeof(BuildStatus), typeof(Uri))]
    public class BuildStatusConverter : IValueConverter
    {
        #region IValueConverter Members

        /// <summary>Converts a value.</summary>
        /// <param name="value">The value produced by the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A converted value. If the method returns null, the valid null value is used.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var buildStatus = value as BuildStatus?;
            if (buildStatus.HasValue)
            {
                switch (buildStatus.Value)
                {
                    case BuildStatus.Succeeded:
						return new Uri("pack://application:,,,/Resources/Green.ico", UriKind.RelativeOrAbsolute);
                    case BuildStatus.Failed:
						return new Uri("pack://application:,,,/Resources/Red.ico", UriKind.RelativeOrAbsolute);
                    case BuildStatus.InProgress:
						return new Uri("pack://application:,,,/Resources/Amber.ico", UriKind.RelativeOrAbsolute);
                    default:
						return new Uri("pack://application:,,,/Resources/Grey.ico", UriKind.RelativeOrAbsolute);
                }
            }

            return null;
        }

        /// <summary>Converts a value.</summary>
        /// <param name="value">The value that is produced by the binding target.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A converted value. If the method returns null, the valid null value is used.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // we don't intend this to ever be called
            return null;
        }

        #endregion
    }
}