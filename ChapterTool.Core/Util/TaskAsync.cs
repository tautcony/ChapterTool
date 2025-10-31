namespace ChapterTool.Util
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;

    public static class TaskAsync
    {
        public static async Task<StringBuilder> RunProcessAsync(string fileName, string args, string workingDirectory = "")
        {
            using (var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName, Arguments = args,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                },
                EnableRaisingEvents = true,
            })
            {
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    process.StartInfo.WorkingDirectory = workingDirectory;
                }
                return await RunProcessAsync(process).ConfigureAwait(false);
            }
        }

        private static Task<StringBuilder> RunProcessAsync(Process process)
        {
            var tcs = new TaskCompletionSource<StringBuilder>();
            var ret = new StringBuilder();
            process.Exited += (sender, args) => tcs.SetResult(ret);
            process.OutputDataReceived += (sender, args) => ret.AppendLine(args.Data?.Trim('\b', ' '));

            // process.ErrorDataReceived += (s, ea) => Debug.WriteLine("ERR: " + ea.Data);
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start process: " + process);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }
    }
}