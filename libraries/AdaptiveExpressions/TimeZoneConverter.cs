// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AdaptiveExpressions
{
    /// <summary>
    /// Time zone converter.
    /// (1) From Windows (.NET) timezone to iana timezone.
    /// (2) From iana timezone to windows (.NET) timezone.
    /// windows ref: https://support.microsoft.com/en-us/help/22803/daylight-saving-time.
    /// iana ref: https://www.iana.org/time-zones.
    /// See database dictionary in file WindowsIanaMapping.
    /// </summary>
    public static class TimeZoneConverter
    {
        private static IDictionary<string, string> ianaToWindowsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static IDictionary<string, string> windowsToIanaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// convert IANA timezone format to windows timezone format.
        /// </summary>
        /// <param name="ianaTimeZoneId">IANA timezone format.</param>
        /// <returns>windows timezone format.</returns>
        public static string IanaToWindows(string ianaTimeZoneId)
        {
            LoadData();
            if (ianaToWindowsMap.ContainsKey(ianaTimeZoneId))
            {
                return ianaToWindowsMap[ianaTimeZoneId];
            }

            return ianaTimeZoneId;
        }

        /// <summary>
        /// Convert windows timezone to iana timezone.
        /// </summary>
        /// <param name="windowsTimeZoneId">Windows timezone format.</param>
        /// <returns>Iana timezone format.</returns>
        public static string WindowsToIana(string windowsTimeZoneId)
        {
            LoadData();
            if (windowsToIanaMap.ContainsKey($"001|{windowsTimeZoneId}"))
            {
                return windowsToIanaMap[$"001|{windowsTimeZoneId}"];
            }

            return windowsTimeZoneId;
        }

        private static void LoadData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();

            using (var sr = new StreamReader(assembly.GetManifestResourceStream("AdaptiveExpressions.WindowsIanaMapping")))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var table = line.Split(',');
                    var windowsId = table[0];
                    var territory = table[1];
                    var ianaIdList = table[2].Split(' ');
                    if (!windowsToIanaMap.ContainsKey($"{territory}|{windowsId}"))
                    {
                        windowsToIanaMap.Add($"{territory}|{windowsId}", ianaIdList[0]);
                    }

                    foreach (var ianaId in ianaIdList)
                    {
                        if (!ianaToWindowsMap.ContainsKey(ianaId))
                        {
                            ianaToWindowsMap.Add(ianaId, windowsId);
                        }
                    }
                }
            }
        }
    }
}
