//-----------------------------------------------------------------------
// <copyright file="GuidConverter.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;

namespace Raven.NewClient.Client.Converters
{
    /// <summary>
    /// Convert strings from / to guids
    /// </summary>
    public class GuidConverter : ITypeConverter
    {
        /// <summary>
        /// Returns whether this converter can convert an object of the given type to the type of this converter, using the specified context.
        /// </summary>
        /// <returns>
        /// true if this converter can perform the conversion; otherwise, false.
        /// </returns>
        /// <param name="sourceType">A <see cref="T:System.Type"/> that represents the type you want to convert from.</param>
        public bool CanConvertFrom(Type sourceType)
        {
            return sourceType == typeof(Guid);
        }

        /// <summary>
        /// Converts the given object to the type of this converter, using the specified context and culture information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. </exception>
        public string ConvertFrom(string tag, object value, bool allowNull)
        {
            var val = (Guid)value;
            if (val == Guid.Empty)
                return tag + Guid.NewGuid();
            return tag + value.ToString();
        }

        /// <summary>
        /// Converts the given value object to the specified type, using the specified context and culture information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        public object ConvertTo(string value)
        {
            return new Guid(value);
        }
    }
}
