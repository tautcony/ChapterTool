// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DvdSubpictureFormat.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the subpicture format
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.DvdTypes
{
    using System.ComponentModel;

    /// <summary>
    /// The DVD subpicture format
    /// </summary>
    public enum DvdSubpictureFormat
    {
        /// <summary>
        /// 2-bit RLE
        /// </summary>
        [Description("2-bit RLE")]
        RLE,

        /// <summary>
        /// Unknown format
        /// </summary>
        [Description("Unknown")]
        Unknown,
    }

    /// <summary>
    /// The subpicture content type
    /// </summary>
    public enum DvdSubpictureType
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        [Description("Unspecified")]
        Undefined,

        /// <summary>
        /// Normal
        /// </summary>
        [Description("Normal")]
        Normal,

        /// <summary>
        /// Large
        /// </summary>
        [Description("Large")]
        Large,

        /// <summary>
        /// Children
        /// </summary>
        [Description("Children")]
        Children,

        /// <summary>
        /// Reserved, do not use
        /// </summary>
        [Description("Reserved")]
        Reserved1,

        /// <summary>
        /// Normal captions
        /// </summary>
        [Description("Normal captions")]
        NormalCC,

        /// <summary>
        /// Large captions
        /// </summary>
        [Description("Large captions")]
        LargeCC,

        /// <summary>
        /// Children captions
        /// </summary>
        [Description("Children captions")]
        ChildrenCC,

        /// <summary>
        /// Reserved, do not use
        /// </summary>
        [Description("Reserved")]
        Reserved2,

        /// <summary>
        /// Forced
        /// </summary>
        [Description("Forced")]
        Forced,

        /// <summary>
        /// Reserved, do not use
        /// </summary>
        [Description("Reserved")]
        Reserved3,

        /// <summary>
        /// Reserved, do not use
        /// </summary>
        [Description("Reserved")]
        Reserved4,

        /// <summary>
        /// Reserved, do not use
        /// </summary>
        [Description("Reserved")]
        Reserved5,

        /// <summary>
        /// Director comments
        /// </summary>
        [Description("Director comments")]
        Director,

        /// <summary>
        /// Large director comments
        /// </summary>
        [Description("Large director comments")]
        LargeDirector,

        /// <summary>
        /// Director comments for children
        /// </summary>
        [Description("Director comments for children")]
        ChildrenDirector,
    }
}