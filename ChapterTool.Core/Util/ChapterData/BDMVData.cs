namespace ChapterTool.Util.ChapterData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public static class BDMVData
    {
        public static event Action<string> OnLog;

        private static readonly Regex RDiskInfo = new Regex(@"(?<idx>\d)\) (?<mpls>\d+\.mpls), (?:(?:(?<dur>\d+:\d+:\d+)[\n\s\b]*(?<fn>.+\.m2ts))|(?:(?<fn2>.+\.m2ts), (?<dur2>\d+:\d+:\d+)))", RegexOptions.Compiled);

        public static async Task<KeyValuePair<string, BDMVGroup>> GetChapterAsync(string location)
        {
            var list = new BDMVGroup();
            var bdmvTitle = string.Empty;
            var path = Path.Combine(location, "BDMV", "PLAYLIST");
            if (!Directory.Exists(path))
            {
                throw new FileNotFoundException("Blu-Ray disc structure not found.");
            }

            var metaPath = Path.Combine(location, "BDMV", "META", "DL");
            if (Directory.Exists(metaPath))
            {
                var xmlFile = Directory.GetFiles(metaPath).FirstOrDefault(file => file.ToLower().EndsWith(".xml"));
                if (xmlFile != null)
                {
                    var xmlText = File.ReadAllText(xmlFile);
                    var title = Regex.Match(xmlText, @"<di:name>(?<title>[^<]*)</di:name>");
                    if (title.Success)
                    {
                        bdmvTitle = title.Groups["title"].Value;
                        OnLog?.Invoke($"Disc Title: {bdmvTitle}");
                    }
                }
            }

            var eac3toPath = RegistryStorage.Load(name: "eac3toPath");
            if (string.IsNullOrEmpty(eac3toPath) || !File.Exists(eac3toPath))
            {
                eac3toPath = Notification.InputBox("请输入eac3to的地址", "注意不要带上多余的引号", "C:\\eac3to\\eac3to.exe");
                if (string.IsNullOrEmpty(eac3toPath)) return new KeyValuePair<string, BDMVGroup>(bdmvTitle, list);
                RegistryStorage.Save(name: "eac3toPath", value: eac3toPath);
            }
            var workingPath = Directory.GetParent(location).FullName;
            location = location.Substring(location.LastIndexOf('\\') + 1);
            var text = (await TaskAsync.RunProcessAsync(eac3toPath, $"\"{location}\"", workingPath)).ToString();
            if (text.Contains("HD DVD / Blu-Ray disc structure not found."))
            {
                OnLog?.Invoke(text);
                throw new Exception("May be the path is too complex or directory contains nonAscii characters");
            }
            OnLog?.Invoke("\r\nDisc Info:\r\n" + text);

            foreach (Match match in RDiskInfo.Matches(text))
            {
                var index = match.Groups["idx"].Value;
                var mpls = match.Groups["mpls"].Value;
                var time = match.Groups["dur"].Value;
                if (string.IsNullOrEmpty(time)) time = match.Groups["dur2"].Value;
                var file = match.Groups["fn"].Value;
                if (string.IsNullOrEmpty(file)) file = match.Groups["fn2"].Value;
                OnLog?.Invoke($"+ {index}) {mpls} -> [{file}] - [{time}]");

                list.Add(new ChapterInfo
                {
                    Duration = TimeSpan.Parse(time),
                    SourceIndex = index,
                    SourceName = file,
                });
            }
            var toBeRemove = new List<ChapterInfo>();
            var chapterPath = Path.Combine(workingPath, "chapters.txt");
            var logPath = Path.Combine(workingPath, "chapters - Log.txt");
            foreach (var current in list)
            {
                text = (await TaskAsync.RunProcessAsync(eac3toPath, $"\"{location}\" {current.SourceIndex})", workingPath)).ToString();
                if (!text.Contains("Chapters"))
                {
                    toBeRemove.Add(current);
                    continue;
                }
                text = (await TaskAsync.RunProcessAsync(eac3toPath, $"\"{location}\" {current.SourceIndex}) chapters.txt", workingPath)).ToString();
                if (!text.Contains("Creating file \"chapters.txt\"...") && !text.Contains("Done!"))
                {
                    OnLog?.Invoke(text);
                    throw new Exception("Error creating chapters file.");
                }
                current.Chapters = OgmData.GetChapterInfo(File.ReadAllBytes(chapterPath).GetUTFString()).Chapters;
                if (current.Chapters.First().Name != string.Empty) continue;
                var chapterName = ChapterName.GetChapterName();
                current.Chapters.ForEach(chapter => chapter.Name = chapterName());
            }
            toBeRemove.ForEach(item => list.Remove(item));
            if (File.Exists(chapterPath)) File.Delete(chapterPath);
            if (File.Exists(logPath)) File.Delete(logPath);
            return new KeyValuePair<string, BDMVGroup>(bdmvTitle, list);
        }
    }
}
