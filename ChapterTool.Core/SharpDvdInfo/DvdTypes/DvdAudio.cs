// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DvdAudio.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the DVD audio formats
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.DvdTypes
{
    using System.ComponentModel;

    /// <summary>
    /// Enumerates valid formats for DVD audio streams
    /// </summary>
    public enum DvdAudioFormat
    {
        /// <summary>
        /// Format AC-3
        /// </summary>
        AC3 = 0,

        /// <summary>
        /// Format MPEG-1
        /// </summary>
        MPEG1 = 2,

        /// <summary>
        /// Format MPEG-2
        /// </summary>
        MPEG2 = 3,

        /// <summary>
        /// Format LPCM
        /// </summary>
        LPCM = 4,

        /// <summary>
        /// Format DTS
        /// </summary>
        DTS = 6,
    }

    /// <summary>
    /// The start ID list container
    /// </summary>
    public struct DvdAudioId
    {
        /// <summary>
        /// stream start ids
        /// </summary>
        public static int[] ID =
        {
            0x80, // AC3
            0,    // UNKNOWN
            0xC0, // MPEG1
            0xC0, // MPEG2
            0xA0, // LPCM
            0,    // UNKNOWN
            0x88,  // DTS
        };
    }

    /// <summary>
    /// The audio quantization types
    /// </summary>
    public enum DvdAudioQuantization
    {
        /// <summary>
        /// 16 bit Quantization
        /// </summary>
        [Description("16bit")]
        Quant16Bit,

        /// <summary>
        /// 20 bit Quantization
        /// </summary>
        [Description("20bit")]
        Quant20Bit,

        /// <summary>
        /// 24 bit Quantization
        /// </summary>
        [Description("24bit")]
        Quant24Bit,

        /// <summary>
        /// Dynamic Range Control
        /// </summary>
        [Description("DRC")]
        DRC,
    }

    /// <summary>
    /// The stream content type
    /// </summary>
    public enum DvdAudioType
    {
        /// <summary>
        /// Undefined
        /// </summary>
        [Description("Unspecified")]
        Undefined,

        /// <summary>
        /// Normal
        /// </summary>
        [Description("Normal")]
        Normal,

        /// <summary>
        /// For visually impaired
        /// </summary>
        [Description("For visually impaired")]
        Impaired,

        /// <summary>
        /// Director's comments
        /// </summary>
        [Description("Director's comments")]
        Comments1,

        /// <summary>
        /// Alternate director's comments
        /// </summary>
        [Description("Alternate director's comments")]
        Comments2,
    }
}