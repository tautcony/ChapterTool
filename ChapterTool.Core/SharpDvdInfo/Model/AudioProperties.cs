// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AudioProperties.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the DVD audio stream properties
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.Model
{
    using SharpDvdInfo.DvdTypes;

    /// <summary>
    /// The audio stream properties
    /// </summary>
    public class AudioProperties
    {
        /// <summary>
        /// The stream coding mode
        /// </summary>
        public DvdAudioFormat CodingMode { get; set; }

        /// <summary>
        /// The Channel count
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Stream samplerate
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Stream quantization
        /// </summary>
        public DvdAudioQuantization Quantization { get; set; }

        /// <summary>
        /// Stream language
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Stream content type
        /// </summary>
        public DvdAudioType Extension { get; set; }

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