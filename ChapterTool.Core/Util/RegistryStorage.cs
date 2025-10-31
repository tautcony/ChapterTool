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

namespace ChapterTool.Util
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Cross-platform settings storage using JSON file
    /// Replaces Registry-based storage from WinForms version
    /// </summary>
    public static class RegistryStorage
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChapterTool",
            "settings.json");

        private static Dictionary<string, string> _settings = new();
        private static bool _loaded = false;

        static RegistryStorage()
        {
            EnsureSettingsDirectory();
        }

        private static void EnsureSettingsDirectory()
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void LoadSettings()
        {
            if (_loaded) return;

            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                _settings = new Dictionary<string, string>();
            }

            _loaded = true;
        }

        private static void SaveSettings()
        {
            try
            {
                EnsureSettingsDirectory();
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail if we can't save settings
            }
        }

        public static string? Load(string subkey, string name)
        {
            // Legacy compatibility - combine subkey and name
            return Load($"{subkey}_{name}");
        }

        public static string? Load(string name)
        {
            LoadSettings();
            return _settings.TryGetValue(name, out var value) ? value : null;
        }

        public static void Save(string value, string subkey, string name)
        {
            // Legacy compatibility - combine subkey and name
            Save($"{subkey}_{name}", value);
        }

        public static void Save(string name, string value)
        {
            LoadSettings();
            _settings[name] = value;
            SaveSettings();
        }

        public static void Delete(string name)
        {
            LoadSettings();
            if (_settings.ContainsKey(name))
            {
                _settings.Remove(name);
                SaveSettings();
            }
        }
    }
}
