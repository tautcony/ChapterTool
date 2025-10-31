// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TitleInfo.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the TitleInfo container which represents 1 DVD title
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.Model
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The Title container
    /// </summary>
    public class TitleInfo
    {
        /// <summary>
        /// Title type
        /// </summary>
        public byte TitleType { get; set; }

        /// <summary>
        /// Number of Angles
        /// </summary>
        public byte NumAngles { get; set; }

        /// <summary>
        /// Number of chapters
        /// </summary>
        public short NumChapters { get; set; }

        /// <summary>
        /// Title parental mask
        /// </summary>
        public short ParentalMask { get; set; }

        /// <summary>
        /// Title Number
        /// </summary>
        public byte TitleNumber { get; set; }

        /// <summary>
        /// Number of titleset
        /// </summary>
        public byte TitleSetNumber { get; set; }

        /// <summary>
        /// position in the titleset
        /// </summary>
        public byte TitleNumberInSet { get; set; }

        /// <summary>
        /// title startsector
        /// </summary>
        public int StartSector { get; set; }

        /// <summary>
        /// The video stream
        /// </summary>
        public VideoProperties VideoStream { get; set; }

        /// <summary>
        /// List of audio streams
        /// </summary>
        public List<AudioProperties> AudioStreams { get; set; }

        /// <summary>
        /// List of subpicture streams
        /// </summary>
        public List<SubpictureProperties> SubtitleStreams { get; set; }

        /// <summary>
        /// List of chapters
        /// </summary>
        public List<TimeSpan> Chapters { get; set; }
    }
}
