﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamBuildTray
{
    [ValueConversion(typeof (string), typeof (bool))]
    public class BuildVisibilityConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            Uri buildDefinitionUri = value as Uri;
            if ((buildDefinitionUri != null) && (MainBuildList.HiddenBuilds != null))
            {
                if (MainBuildList.HiddenBuilds.Contains(buildDefinitionUri))
                {
                    return false;
                }
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            // we don't intend this to ever be called
            return null;
        }

        #endregion
    }
}