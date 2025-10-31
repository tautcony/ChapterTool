// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DvdVideo.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the DVD video standard
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.DvdTypes
{
    using System.ComponentModel;

    public enum DvdVideoStandard
    {
        [Description("NTSC")]
        NTSC,

        [Description("PAL")]
        PAL,
    }

    public enum DvdVideoResolution
    {
        /// <summary>
        /// NTSC 720x480
        /// </summary>
        [Description("720x480")]
        Res720By480 = 0,

        /// <summary>
        /// NTSC 704x480
        /// </summary>
        [Description("704x480")]
        Res704By480 = 1,

        /// <summary>
        /// NTSC 352x480
        /// </summary>
        [Description("352x480")]
        Res352By480 = 2,

        /// <summary>
        /// NTSC 352x240
        /// </summary>
        [Description("352x240")]
        Res352By240 = 3,

        /// <summary>
        /// PAL 720x576
        /// </summary>
        [Description("720x576")]
        Res720By576 = 8,

        /// <summary>
        /// PAL 704x576
        /// </summary>
        [Description("704x576")]
        Res704By576 = 9,

        /// <summary>
        /// PAL 352x576
        /// </summary>
        [Description("352x576")]
        Res352By576 = 10,

        /// <summary>
        /// PAL 352x288
        /// </summary>
        [Description("352x288")]
        Res352By288 = 11,
    }

    public enum DvdVideoPermittedDisplayFormat
    {
        [Description("Pan & Scan + Letterbox")]
        PanScanLetterbox,

        [Description("Pan & Scan")]
        PanScan,

        [Description("Letterbox")]
        Letterbox,

        [Description("None")]
        None,
    }

    public enum DvdVideoMpegVersion
    {
        [Description("MPEG-1")]
        Mpeg1,

        [Description("MPEG-2")]
        Mpeg2,
    }

    public enum DvdVideoAspectRatio
    {
        [Description("4/3")]
        Aspect4By3,

        /// <summary>
        /// Not specified, some DVD's use this index for signaling 16/9 aspect ratio, though
        /// </summary>
        [Description("16/9")]
        Aspect16By9NotSpecified,

        [Description("Reserved")]
        AspectUnknown,

        [Description("16/9")]
        Aspect16By9,
    }
}