// -----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="Knuckleball Project">
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
    using System.Runtime.InteropServices;

    /// <summary>
    /// Contains methods used for interfacing with the native code MP4V2 library.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Represents the known types used for chapters.
        /// </summary>
        /// <remarks>
        /// These values are taken from the MP4V2 header files, documented thus:
        /// <para>
        /// <code>
        /// typedef enum {
        ///     MP4ChapterTypeNone = 0,
        ///     MP4ChapterTypeAny  = 1,
        ///     MP4ChapterTypeQt   = 2,
        ///     MP4ChapterTypeNero = 4
        /// } MP4ChapterType;
        /// </code>
        /// </para>
        /// </remarks>
        internal enum MP4ChapterType
        {
            /// <summary>
            /// No chapters found return value
            /// </summary>
            None = 0,

            /// <summary>
            /// Any or all known chapter types
            /// </summary>
            Any = 1,

            /// <summary>
            /// QuickTime chapter type
            /// </summary>
            Qt = 2,

            /// <summary>
            /// Nero chapter type
            /// </summary>
            Nero = 4,
        }

        [DllImport("libMP4V2.dll", CharSet = CharSet.Auto, ExactSpelling = true, BestFitMapping = false, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr MP4Read([MarshalAs(UnmanagedType.LPStr)]string fileName);

        [DllImport("libMP4V2.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void MP4Close(IntPtr file);

        [DllImport("libMP4V2.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void MP4Free(IntPtr pointer);

        [DllImport("libMP4V2.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I4)]
        internal static extern MP4ChapterType MP4GetChapters(IntPtr hFile, ref IntPtr chapterList, ref int chapterCount, MP4ChapterType chapterType);

        [DllImport("libMP4V2.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long MP4GetDuration(IntPtr hFile);

        [DllImport("libMP4V2.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int MP4GetTimeScale(IntPtr hFile);

        /// <summary>
        /// Represents information for a chapter in this file.
        /// </summary>
        /// <remarks>
        /// This structure definition is taken from the MP4V2 header files, documented thus:
        /// <para>
        /// <code>
        /// #define MP4V2_CHAPTER_TITLE_MAX 1023
        ///
        /// typedef struct MP4Chapter_s {
        ///     MP4Duration duration;
        ///     char title[MP4V2_CHAPTER_TITLE_MAX+1];
        /// } MP4Chapter_t;
        /// </code>
        /// </para>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        internal struct MP4Chapter
        {
            /// <summary>
            /// Duration of chapter in milliseconds
            /// </summary>
            internal long duration;

            /// <summary>
            /// Title of chapter
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            internal byte[] title;
        }
    }
}
