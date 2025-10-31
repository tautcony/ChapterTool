// --------------------------------------------------------------------------------------------------------------------
// <copyright file="VmgmInfo.cs" company="JT-Soft (https://github.com/UniqProject/SharpDvdInfo)">
//   This file is part of the SharpDvdInfo source code - It may be used under the terms of the GNU General Public License.
// </copyright>
// <summary>
//   Defines the DVD VMGM info
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpDvdInfo.Model
{
    /// <summary>
    /// The VMGM info
    /// </summary>
    public class VmgmInfo
    {
        /// <summary>
        /// The Major Version
        /// </summary>
        public int MajorVersion { get; set; }

        /// <summary>
        /// The Minor Version
        /// </summary>
        public int MinorVersion { get; set; }

        /// <summary>
        /// Number of titlesets
        /// </summary>
        public int NumTitleSets { get; set; }

        /// <summary>
        /// Number of titles
        /// </summary>
        public int NumTitles { get; set; }
    }
}
