// ****************************************************************************
//
// Copyright (C) 2014-2016 TautCony (TautCony@vcb-s.com)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// ****************************************************************************

namespace ChapterTool.Util.ChapterData
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using Microsoft.Win32;

    internal class MatroskaData
    {
        private readonly XmlDocument _result = new XmlDocument();

        private readonly string _mkvextractPath;

        public static event Action<string> OnLog;

        public MatroskaData()
        {
            var mkvToolnixPath = RegistryStorage.Load(@"Software\ChapterTool", "mkvToolnixPath");

            // saved path not found.
            if (string.IsNullOrEmpty(mkvToolnixPath))
            {
                try
                {
                    mkvToolnixPath = GetMkvToolnixPathViaRegistry();
                    RegistryStorage.Save(mkvToolnixPath, @"Software\ChapterTool", "mkvToolnixPath");
                }
                catch (Exception exception)
                {
                    // no valid path found in Registry
                    OnLog?.Invoke($"Warning: {exception.Message}");
                }

                // Installed path not found.
                if (string.IsNullOrEmpty(mkvToolnixPath))
                {
                    mkvToolnixPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
            }
            if (mkvToolnixPath != null)
                _mkvextractPath = Path.Combine(mkvToolnixPath, "mkvextract.exe");
            if (!File.Exists(_mkvextractPath))
            {
                OnLog?.Invoke($"Mkvextract Path: {_mkvextractPath}");
                throw new Exception("无可用 MkvExtract, 安装个呗~");
            }
        }

        public XmlDocument GetXml(string path)
        {
            string arg = $"chapters \"{path}\"";
            var xmlresult = RunMkvextract(arg, _mkvextractPath);
            if (string.IsNullOrEmpty(xmlresult)) throw new Exception("No Chapter Found");
            _result.LoadXml(xmlresult);
            return _result;
        }

        private static string RunMkvextract(string arguments, string program)
        {
            var process = new Process
            {
                StartInfo = { FileName = program, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = System.Text.Encoding.UTF8 },
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            process.Close();
            return output;
        }

        /// <summary>
        /// Returns the path from MKVToolnix.
        /// It tries to find it via the registry keys.
        /// If it doesn't find it, it throws an exception.
        /// </summary>
        /// <returns></returns>
        private static string GetMkvToolnixPathViaRegistry()
        {
            RegistryKey regMkvToolnix = null;
            var valuePath = string.Empty;
            var subKeyFound = false;
            var valueFound = false;

            // First check for Installed MkvToolnix
            // First check Win32 registry
            var regUninstall = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (regUninstall == null)
            {
                throw new Exception("Failed to create a RegistryKey variable");
            }

            if (regUninstall.GetSubKeyNames().Any(subKeyName => subKeyName.ToLower().Equals("MKVToolNix".ToLower())))
            {
                subKeyFound = true;
                regMkvToolnix = regUninstall.OpenSubKey("MKVToolNix");
            }

            // if sub key was found, try to get the executable path
            if (subKeyFound)
            {
                if (regMkvToolnix == null) throw new Exception($"Failed to open key {nameof(regMkvToolnix)}");
                foreach (var valueName in regMkvToolnix.GetValueNames().Where(valueName => valueName.ToLower().Equals("DisplayIcon".ToLower())))
                {
                    valueFound = true;
                    valuePath = (string)regMkvToolnix.GetValue(valueName);
                    break;
                }
            }

            // if value was not found, let's Win64 registry
            if (!valueFound)
            {
                subKeyFound = false;
                regUninstall = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
                if (regUninstall == null) throw new Exception($"Failed to open key {nameof(regUninstall)}");
                if (regUninstall.GetSubKeyNames().Any(subKeyName => subKeyName.ToLower().Equals("MKVToolNix".ToLower())))
                {
                    subKeyFound = true;
                    regMkvToolnix = regUninstall.OpenSubKey("MKVToolNix");
                }

                // if sub key was found, try to get the executable path
                if (subKeyFound)
                {
                    if (regMkvToolnix == null) throw new Exception($"Failed to open key {nameof(regMkvToolnix)}");
                    foreach (var valueName in regMkvToolnix.GetValueNames().Where(valueName => valueName.ToLower().Equals("DisplayIcon".ToLower())))
                    {
                        valueFound = true;
                        valuePath = (string)regMkvToolnix.GetValue(valueName);
                        break;
                    }
                }
            }

            // if value was still not found, we may have portable installation
            // let's try the CURRENT_USER registry
            if (!valueFound)
            {
                var regSoftware = Registry.CurrentUser.OpenSubKey("Software");
                subKeyFound = false;
                if (regSoftware != null && regSoftware.GetSubKeyNames().Any(subKey => subKey.ToLower().Equals("mkvmergeGUI".ToLower())))
                {
                    subKeyFound = true;
                    regMkvToolnix = regSoftware.OpenSubKey("mkvmergeGUI");
                }

                // if we didn't find the MkvMergeGUI key, all hope is lost
                if (!subKeyFound)
                {
                    throw new Exception("Couldn't find MKVToolNix in your system!\r\nPlease download and install it or provide a manual path!");
                }
                RegistryKey regGui = null;
                var foundGuiKey = false;
                if (regMkvToolnix != null && regMkvToolnix.GetSubKeyNames().Any(subKey => subKey.ToLower().Equals("GUI".ToLower())))
                {
                    foundGuiKey = true;
                    regGui = regMkvToolnix.OpenSubKey("GUI");
                }

                // if we didn't find the GUI key, all hope is lost
                if (!foundGuiKey)
                {
                    throw new Exception("Found MKVToolNix in your system but not the registry Key GUI!");
                }

                if (regGui != null && regGui.GetValueNames().Any(valueName => valueName.ToLower().Equals("mkvmerge_executable".ToLower())))
                {
                    valueFound = true;
                    valuePath = (string)regGui.GetValue("mkvmerge_executable");
                }

                // if we didn't find the mkvmerge_executable value, all hope is lost
                if (!valueFound)
                {
                    throw new Exception("Found MKVToolNix in your system but not the registry value mkvmerge_executable!");
                }
            }

            // Now that we found a value (otherwise we would not be here, an exception would have been thrown)
            // let's check if it's valid
            if (!File.Exists(valuePath))
            {
                throw new Exception($"Found a registry value ({valuePath}) for MKVToolNix in your system but it is not valid!");
            }

            // Everything is A-OK! Return the valid Directory value! :)
            return Path.GetDirectoryName(valuePath);
        }
    }
}
