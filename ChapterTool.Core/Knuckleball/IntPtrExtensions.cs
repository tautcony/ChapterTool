// -----------------------------------------------------------------------
// <copyright file="IntPtrExtensions.cs" company="Knuckleball Project">
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
    /// The <see cref="IntPtrExtensions"/> class contains extension methods used
    /// for marshaling data between managed and unmanaged code.
    /// </summary>
    public static class IntPtrExtensions
    {
        /// <summary>
        /// Reads a 32-bit integer value beginning at the location pointed to
        /// in memory by the specified pointer value.
        /// </summary>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <returns>The 32-bit integer value pointed to by this <see cref="IntPtr"/>. Returns
        /// <see langword="null"/> if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static int? ReadInt(this IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.ReadInt32(value);
        }

        /// <summary>
        /// Reads a 64-bit integer value beginning at the location pointed to
        /// in memory by the specified pointer value.
        /// </summary>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <returns>The 64-bit integer value pointed to by this <see cref="IntPtr"/>. Returns
        /// <see langword="null"/> if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static long? ReadLong(this IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.ReadInt64(value);
        }

        /// <summary>
        /// Reads a 16-bit integer value beginning at the location pointed to
        /// in memory by the specified pointer value.
        /// </summary>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <returns>The 16-bit integer value pointed to by this <see cref="IntPtr"/>. Returns
        /// <see langword="null"/> if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static short? ReadShort(this IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.ReadInt16(value);
        }

        /// <summary>
        /// Reads an 8-bit integer value beginning at the location pointed to
        /// in memory by the specified pointer value.
        /// </summary>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <returns>The 8-bit integer value pointed to by this <see cref="IntPtr"/>. Returns
        /// <see langword="null"/> if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static byte? ReadByte(this IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.ReadByte(value);
        }

        /// <summary>
        /// Reads an 8-bit integer value beginning at the location pointed to
        /// in memory by the specified pointer value, and coerces that value into
        /// a boolean.
        /// </summary>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <returns><see langword="true"/> if the value pointed to by this <see cref="IntPtr"/>
        /// is non-zero; <see langword="false"/> if the value pointed to is zero.
        /// Returns <see langword="null"/> if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static bool? ReadBoolean(this IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.ReadByte(value) != 0;
        }

        /// <summary>
        /// Reads an enumerated value beginning at the location pointed to in
        /// memory by the specified pointer value.
        /// </summary>
        /// <typeparam name="T">A value derived from <see cref="System.Enum"/>.</typeparam>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <param name="defaultValue">The default value of the enumerated value to return
        /// if the memory location pointed to by this <see cref="IntPtr"/> is a null pointer
        /// (<see cref="IntPtr.Zero"/>).</param>
        /// <returns>The enumerated value pointed to by this <see cref="IntPtr"/>. Returns
        /// the specified default value if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static T ReadEnumValue<T>(this IntPtr value, T defaultValue)
            where T : struct
        {
            if (value == IntPtr.Zero)
            {
                return defaultValue;
            }

            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("Type T must be an enumerated value");
            }

            object rawValue;
            var underlyingType = Enum.GetUnderlyingType(typeof(T));
            if (underlyingType == typeof(byte))
            {
                rawValue = ReadByte(value).Value;
            }
            else if (underlyingType == typeof(long))
            {
                rawValue = ReadLong(value).Value;
            }
            else if (underlyingType == typeof(short))
            {
                rawValue = ReadShort(value).Value;
            }
            else
            {
                rawValue = value.ReadInt().Value;
            }

            return (T)Enum.ToObject(typeof(T), rawValue);
        }

        /// <summary>
        /// Reads a structure beginning at the location pointed to in memory by the
        /// specified pointer value.
        /// </summary>
        /// <typeparam name="T">The type of the structure to read.</typeparam>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <returns>An instance of the specified structure type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when this <see cref="IntPtr"/>
        /// is a null pointer (<see cref="IntPtr.Zero"/>).</exception>
        public static T ReadStructure<T>(this IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(value), "Structures cannot be read from a null pointer (IntPtr.Zero)");
            }

            return (T)Marshal.PtrToStructure(value, typeof(T));
        }

        /// <summary>
        /// Reads a block of memory beginning at the location pointed to by the specified
        /// pointer value, and copies the contents into a byte array of the specified length.
        /// </summary>
        /// <param name="value">The <see cref="IntPtr"/> value indicating the location
        /// in memory at which to begin reading data.</param>
        /// <param name="bufferLength">The number of bytes to read into the byte array.</param>
        /// <returns>The byte array containing copies of the values pointed to by this <see cref="IntPtr"/>. Returns
        /// <see langword="null"/> if this pointer is a null pointer (<see cref="IntPtr.Zero"/>).</returns>
        public static byte[] ReadBuffer(this IntPtr value, int bufferLength)
        {
            if (value == IntPtr.Zero)
            {
                return null;
            }

            var buffer = new byte[bufferLength];
            Marshal.Copy(value, buffer, 0, bufferLength);
            return buffer;
        }
    }
}
