// -----------------------------------------------------------------------
// <copyright file="MP4File.cs" company="Knuckleball Project">
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Portions created by Jim Evans are Copyright © 2012.
// All Rights Reserved.
//
// Contributors:
//     Jim Evans, james.h.evans.jr@@gmail.com
//
// </copyright>
// -----------------------------------------------------------------------
namespace Knuckleball
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Represents an instance of an MP4 file.
    /// </summary>
    public class MP4File
    {
        private readonly string _fileName;

        public static event Action<string> OnLog;

        /// <summary>
        /// Prevents a default instance of the <see cref="MP4File"/> class from being created.
        /// </summary>
        private MP4File()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MP4File"/> class.
        /// </summary>
        /// <param name="fileName">The full path and file name of the file to use.</param>
        private MP4File(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
            {
                throw new ArgumentException("Must specify a valid file name", nameof(fileName));
            }

            _fileName = fileName;
        }

        /// <summary>
        /// Gets the list of chapters for this file.
        /// </summary>
        public List<Chapter> Chapters { get; private set; }

        /// <summary>
        /// Opens and reads the data for the specified file.
        /// </summary>
        /// <param name="fileName">The full path and file name of the MP4 file to open.</param>
        /// <returns>An <see cref="MP4File"/> object you can use to manipulate file.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the specified file name is <see langword="null"/> or the empty string.
        /// </exception>
        public static MP4File Open(string fileName)
        {
            var file = new MP4File(fileName);
            file.Load();
            return file;
        }

        /// <summary>
        /// Loads the metadata for this file.
        /// </summary>
        public void Load()
        {
            var fileHandle = NativeMethods.MP4Read(_fileName);
            if (fileHandle == IntPtr.Zero) return;
            try
            {
                Chapters = ReadFromFile(fileHandle);
            }
            finally
            {
                NativeMethods.MP4Close(fileHandle);
            }
        }

        /// <summary>
        /// Reads the chapter information from the specified file.
        /// </summary>
        /// <param name="fileHandle">The handle to the file from which to read the chapter information.</param>
        /// <returns>A new instance of a <see cref="List{Chapter}"/> object containing the information
        /// about the chapters for the file.</returns>
        internal static List<Chapter> ReadFromFile(IntPtr fileHandle)
        {
            var list = new List<Chapter>();
            var chapterListPointer = IntPtr.Zero;
            var chapterCount = 0;
            var chapterType = NativeMethods.MP4GetChapters(fileHandle, ref chapterListPointer, ref chapterCount, NativeMethods.MP4ChapterType.Any);
            OnLog?.Invoke($"Chapter type: {chapterType}");
            if (chapterType != NativeMethods.MP4ChapterType.None && chapterCount != 0)
            {
                var currentChapterPointer = chapterListPointer;
                for (var i = 0; i < chapterCount; ++i)
                {
                    var currentChapter = currentChapterPointer.ReadStructure<NativeMethods.MP4Chapter>();
                    var duration = TimeSpan.FromMilliseconds(currentChapter.duration);
                    var title = GetString(currentChapter.title);
                    OnLog?.Invoke($"{title} {duration}");
                    list.Add(new Chapter { Duration = duration, Title = title });
                    currentChapterPointer = IntPtr.Add(currentChapterPointer, Marshal.SizeOf(currentChapter));
                }
            }
            else
            {
                var timeScale = NativeMethods.MP4GetTimeScale(fileHandle);
                var duration = NativeMethods.MP4GetDuration(fileHandle);
                list.Add(new Chapter { Duration = TimeSpan.FromSeconds(duration / (double)timeScale), Title = "Chapter 1" });
            }
            if (chapterListPointer != IntPtr.Zero)
            {
                NativeMethods.MP4Free(chapterListPointer);
            }
            return list;
        }

        /// <summary>
        /// Decodes a C-Style string into a string, can handle UTF-8 or UTF-16 encoding.
        /// </summary>
        /// <param name="bytes">C-Style string</param>
        /// <returns></returns>
        private static string GetString(byte[] bytes)
        {
            if (bytes == null) return null;
            string title = null;
            if (bytes.Length <= 3) title = Encoding.UTF8.GetString(bytes);
            if (bytes[0] == 0xFF && bytes[1] == 0xFE) title = Encoding.Unicode.GetString(bytes);
            if (bytes[0] == 0xFE && bytes[1] == 0xFF) title = Encoding.BigEndianUnicode.GetString(bytes);
            if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                title = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            else if (title == null) title = Encoding.UTF8.GetString(bytes);

            return title.Substring(0, title.IndexOf('\0'));
        }
    }
}
