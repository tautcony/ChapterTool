// --------------------------------------------------------------------------------------------------------------------
// <copyright file="VideoProperties.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the DVD video stream properties
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.Model
{
    using System;
    using SharpDvdInfo.DvdTypes;

    /// <summary>
    /// The video stream properties
    /// </summary>
    public class VideoProperties
    {
        /// <summary>
        /// Permitted display format
        /// </summary>
        public DvdVideoPermittedDisplayFormat DisplayFormat { get; set; }

        /// <summary>
        /// Stream aspect ratio
        /// </summary>
        public DvdVideoAspectRatio AspectRatio { get; set; }

        /// <summary>
        /// Video standard
        /// </summary>
        public DvdVideoStandard VideoStandard { get; set; }

        /// <summary>
        /// Stream coding mode
        /// </summary>
        public DvdVideoMpegVersion CodingMode { get; set; }

        /// <summary>
        /// Stream resolution
        /// </summary>
        public DvdVideoResolution VideoResolution { get; set; }

        /// <summary>
        /// Stream runtime
        /// </summary>
        public TimeSpan Runtime { get; set; }

        /// <summary>
        /// Stream framerate
        /// </summary>
        public float Framerate { get; set; }

        /// <summary>
        /// Stream framerate numerator
        /// </summary>
        public int FrameRateNumerator { get; set; }

        /// <summary>
        /// Stream framerate demominator
        /// </summary>
        public int FrameRateDenominator { get; set; }
    }
}
