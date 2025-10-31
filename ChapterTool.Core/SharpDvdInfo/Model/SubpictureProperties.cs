// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SubpictureProperties.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the DVD subpicture stream properties
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.Model
{
    using SharpDvdInfo.DvdTypes;

    /// <summary>
    /// The subpicture properties
    /// </summary>
    public class SubpictureProperties
    {
        /// <summary>
        /// Stream format
        /// </summary>
        public DvdSubpictureFormat Format { get; set; }

        /// <summary>
        /// Stream language
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Stream content type
        /// </summary>
        public DvdSubpictureType Extension { get; set; }

        /// <summary>
        /// Stream ID
        /// </summary>
        public int StreamId { get; set; }

        /// <summary>
        /// Stream Index
        /// </summary>
        public int StreamIndex { get; set; }
    }
}