using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CoiDataExporter.Mod
{
    /// <summary>
    /// Small, defensive reflection adapter around game prototype objects.
    /// COI's internal types can change between game releases; keeping this boundary
    /// in one class makes upgrades local and keeps the exported contract stable.
    /// </summary>
    internal sealed class ReflectionReader
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public object Read(object target, params string[] path)
        {
            var current = target;
            if (current == null || path == null)
            {
                return null;
            }

            foreach (var memberName in path)
            {
                current = ReadMember(current, memberName);
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        public object ReadMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            var type = target.GetType();

            try
            {
                var property = type.GetProperty(memberName, InstanceFlags);
                if (property != null && property.GetIndexParameters().Length == 0 && property.CanRead)
                {
                    return property.GetValue(target, null);
                }

                var field = type.GetField(memberName, InstanceFlags);
                if (field != null)
                {
                    return field.GetValue(target);
                }
            }
            catch
            {
                // A prototype can expose a property that throws when unavailable in a
                // particular game state. Treat that property as missing.
            }

            return null;
        }

        public object Unwrap(object value)
        {
            var current = value;

            for (var i = 0; i < 8 && current != null; i++)
            {
                if (current is string)
                {
                    return current;
                }

                var next = ReadMember(current, "Value");
                if (next == null || ReferenceEquals(next, current))
                {
                    break;
                }

                // Avoid unwrapping arbitrary domain objects that happen to expose a
                // Value property but are not option/value wrappers.
                var typeName = current.GetType().Name;
                if (!typeName.Contains("Option") &&
                    !typeName.Contains("Quantity") &&
                    !typeName.Contains("Duration") &&
                    typeName.IndexOf("id", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !typeName.Contains("Value") &&
                    !IsNumeric(next))
                {
                    break;
                }

                current = next;
            }

            return current;
        }

        public string Text(object value)
        {
            var unwrapped = Unwrap(value);
            return unwrapped == null ? string.Empty : Convert.ToString(unwrapped, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public string Id(object prototype)
        {
            var rawId = Read(prototype, "Id");
            return Text(rawId);
        }

        public double Number(object value, double defaultValue = 0d)
        {
            var unwrapped = Unwrap(value);
            if (unwrapped == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToDouble(unwrapped, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        public int Integer(object value, int defaultValue = 0)
        {
            return (int)Math.Round(Number(value, defaultValue), MidpointRounding.AwayFromZero);
        }

        public bool Boolean(object value, bool defaultValue = false)
        {
            var unwrapped = Unwrap(value);
            if (unwrapped == null)
            {
                return defaultValue;
            }

            if (unwrapped is bool boolean)
            {
                return boolean;
            }

            bool parsed;
            return bool.TryParse(Text(unwrapped), out parsed) ? parsed : defaultValue;
        }

        public IEnumerable<object> Items(object value)
        {
            var unwrapped = Unwrap(value);
            if (unwrapped is string || !(unwrapped is IEnumerable enumerable))
            {
                return Enumerable.Empty<object>();
            }

            var result = new List<object>();
            foreach (var item in enumerable)
            {
                result.Add(item);
            }

            return result;
        }

        public static bool IsNumeric(object value)
        {
            if (value == null)
            {
                return false;
            }

            var type = value.GetType();
            type = Nullable.GetUnderlyingType(type) ?? type;

            return type == typeof(byte) ||
                   type == typeof(short) ||
                   type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal);
        }
    }
}
