using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiskSpace_Examiner_2016
{
    /** This class can also be found in Wiley Black's Software Library **/

    /// <summary>
    /// The DataSize structure represents a bytewise data length, such as the size
    /// of a file, a network packet, RAM, etc.  The structure provides tools to aid
    /// in the optimal presentation of this data length, such as printing a 1.3 GB file
    /// as "1.35 GB", "1.3 GB", "1 GB", etc., depending on options.  A Parse()
    /// utility is also providing for the same purpose.  Note that the default ToString()
    /// implementation always provides an exact representation such as "1928386 bytes"
    /// in case it is used for information storage or processing.  See the 
    /// ToFriendlyString() and Parse() methods for more information.
    /// </summary>
    public struct DataSize
    {
        const Int64 g_Kilobyte = 1024;
        const Int64 g_Megabyte = 1048576;
        const Int64 g_Gigabyte = 1073741824;
        const Int64 g_Terrabyte = 1099511627776;

        public static DataSize Kilobyte = new DataSize(g_Kilobyte);
        public static DataSize Megabyte = new DataSize(g_Megabyte);
        public static DataSize Gigabyte = new DataSize(g_Gigabyte);
        public static DataSize Terrabyte = new DataSize(g_Terrabyte);

        public long Size;

        public DataSize(long size) { this.Size = size; }

        public static DataSize operator +(DataSize a, DataSize b) { return new DataSize(a.Size + b.Size); }
        public static DataSize operator -(DataSize a, DataSize b) { return new DataSize(a.Size - b.Size); }
        public static implicit operator DataSize(long a) { return new DataSize(a); }
        public static implicit operator long(DataSize a) { return a.Size; }
        public override bool Equals(object obj)
        {
            if (!(obj is DataSize)) return false;
            return (this.Size == ((DataSize)obj).Size);
        }
        public static bool operator >(DataSize a, DataSize b) { return (a.Size > b.Size); }
        public static bool operator <(DataSize a, DataSize b) { return (a.Size < b.Size); }
        public static bool operator >=(DataSize a, DataSize b) { return (a.Size >= b.Size); }
        public static bool operator <=(DataSize a, DataSize b) { return (a.Size <= b.Size); }

        /// <summary>
        /// The ToString() method returns an exact representation of the
        /// data size, such as "1932964 bytes".
        /// </summary>
        /// <returns>A string representation of the data size.</returns>
        public override string ToString()
        {
            return Size.ToString() + " bytes";
        }

        /// <summary>
        /// ToFriendlyString() provides a human friendly presentation of
        /// the data size.  For example, FileLength.ToFriendlyString()
        /// might return "1.3 GB".
        /// </summary>        
        /// <returns>An inexact human readable string which can also be handled by Parse().</returns>
        public string ToFriendlyString() { return ToFriendlyString(g_Gigabyte, 1); }

        /// <summary>
        /// ToFriendlyString() provides a human friendly presentation of
        /// the data size.  For example, FileLength.ToFriendlyString(DataSize.Gigabyte)
        /// might return "1.3 GB".
        /// </summary>
        /// <param name="FractionThreshold">The smallest data size for which a fractional digit will be included.</param>
        /// <returns>An inexact human readable string which can also be handled by Parse().</returns>
        public string ToFriendlyString(long FractionThreshold) { return ToFriendlyString(FractionThreshold, 1); }

        /// <summary>
        /// ToFriendlyString() provides a human friendly presentation of
        /// the data size.  For example, FileLength.ToFriendlyString(DataSize.Gigabyte)
        /// might return "1.3 GB".
        /// </summary>
        /// <param name="FractionThreshold">The smallest data size for which a fractional digit will be included.</param>
        /// <param name="FractionalDigits">The number of fractional digits to include when FractionThreshold is exceeded.</param>
        /// <returns>An inexact human readable string which can also be handled by Parse().</returns>
        public string ToFriendlyString(long FractionThreshold, int FractionalDigits)
        {
            string Postfix;
            double Divisor;
            if (Size < g_Kilobyte) { Postfix = " bytes"; Divisor = 1.0; }
            else if (Size < g_Megabyte) { Postfix = " KB"; Divisor = g_Kilobyte; }
            else if (Size < g_Gigabyte) { Postfix = " MB"; Divisor = g_Megabyte; }
            else if (Size < g_Terrabyte) { Postfix = " GB"; Divisor = g_Gigabyte; }
            else { Postfix = " TB"; Divisor = g_Terrabyte; }

            if (Size < FractionThreshold) return ((long)Math.Round(Size / Divisor)).ToString() + Postfix;
            return (Size / Divisor).ToString("F0" + FractionalDigits.ToString()) + Postfix;
        }

        public enum Unit
        {
            Bytes,
            Kilobytes,
            Megabytes,
            Gigabytes,
            Terrabytes
        }

        /// <summary>
        /// <para>The DataSize.Parse() function accepts a string in any of the following formats:</para>
        /// <list type="bullet">
        /// <item>Exact numeric string such as "1392853".</item>
        /// <item>Exact string with the "bytes" postfix such as "1024 bytes" or "1024bytes".</item>
        /// <item>Possibly inexact string with a postfix of "KB", "MB", "GB", or "TB".  The numeric 
        ///     value can contain a fraction, as in "1.359 GB".</item>
        /// </list>
        /// </summary>
        /// <param name="str">The string to be parsed.</param>
        /// <returns>A DataSize object representing the value.  If the string cannot be parsed, an exception is thrown</returns>
        public static DataSize Parse(string str) { return Parse(str, Unit.Bytes); }

        /// <summary>
        /// <para>The DataSize.Parse() function accepts a string in any of the following formats:</para>
        /// <list type="bullet">        
        /// <item>Exact string with the "bytes" postfix such as "1024 bytes" or "1024bytes".</item>
        /// <item>Possibly exact numeric string such as "1.359" or "29394", which will take the units specified
        ///     by the DefaultUnit parameter.</item>
        /// <item>Possibly inexact string with a postfix of "KB", "MB", "GB", or "TB".  The numeric 
        ///     value can contain a fraction, as in "1.359 GB".</item>
        /// </list>
        /// </summary>
        /// <param name="str">The string to be parsed.</param>
        /// <param name="DefaultUnit">The default units to be applied when the string contains only a numeric value.</param>
        /// <returns>A DataSize object representing the value.  If the string cannot be parsed, an exception is thrown</returns>
        public static DataSize Parse(string str, Unit DefaultUnit)
        {
            str = str.Trim();
            string Working;
            double Factor;
            int iIndex;
            if ((iIndex = str.IndexOf("TB")) >= 0) { Factor = g_Terrabyte; Working = str.Substring(0, iIndex); }
            else if ((iIndex = str.IndexOf("GB")) >= 0) { Factor = g_Gigabyte; Working = str.Substring(0, iIndex); }
            else if ((iIndex = str.IndexOf("MB")) >= 0) { Factor = g_Megabyte; Working = str.Substring(0, iIndex); }
            else if (((iIndex = str.IndexOf("B")) >= 0) || ((iIndex = str.IndexOf("bytes")) >= 0)) { Factor = 1.0; Working = str.Substring(0, iIndex); }
            else
            {
                Working = str;
                switch (DefaultUnit)
                {
                    case Unit.Bytes: Factor = 1.0; break;
                    case Unit.Kilobytes: Factor = g_Kilobyte; break;
                    case Unit.Megabytes: Factor = g_Megabyte; break;
                    case Unit.Gigabytes: Factor = g_Gigabyte; break;
                    case Unit.Terrabytes: Factor = g_Terrabyte; break;
                    default: throw new NotSupportedException();
                }
            }

            double Size = double.Parse(Working.TrimEnd());
            return new DataSize((long)(Size * Factor));
        }

        public override int GetHashCode() { return Size.GetHashCode(); }
    }
}
