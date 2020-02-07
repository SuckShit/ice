using System;
using System.Collections.Generic;

namespace Ice
{
    /// <summary> This attribute class is used by the generated code to assign a type ID to C# classes and
    /// interfaces mapped from Slice interfaces, classes and exceptions. </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    public sealed class TypeIdAttribute : Attribute
    {
        /// <summary>Retrieve the type ID.</summary>
        /// <value>The type ID string.</value>
        public string Value { get; }

        /// <summary>Construct a TypeIdAttribute.</summary>
        /// <param name="value">The type ID.</param>>
        public TypeIdAttribute(string value) => Value = value;
    }

    /// <summary> This class adds extension methods to System.Type.</summary>
    public static class TypeExtensions
    {
        /// <summary>Retrieve the Ice type ID from a type with the attribute Ice.TypeId</summary>
        /// <param name="type">The class or interface generated by the Slice compiler.</param>
        /// <returns>The type ID, or null if type does not carry the Ice.TypeId attribute.</returns>
        public static string? GetIceTypeId(this Type type)
        {
            object[] attributes = type.GetCustomAttributes(typeof(TypeIdAttribute), false);
            if (attributes.Length == 1 && attributes[0] is TypeIdAttribute typeId)
            {
                return typeId.Value;
            }
            else
            {
                return null; // unexpected when called by the generated code
            }
        }

        /// <summary>Retrieve the Ice type ID from a type and from all its base types.
        /// When type is an interface, it returns the type ID for that interface, plus all its base interfaces, plus
        /// "::Ice::Object", and these type IDs are returned in alphabetical order.
        /// When type is a class, it returns the type ID of that class plus the type ID of the base classes. These
        /// type IDs are sorted from most derived to least derived.</summary>
        /// <param name="type">The class or interface generated by the Slice compiler.</param>
        /// <returns>An array of Ice type IDs.</returns>
        public static string[] GetAllIceTypeIds(this Type type)
        {
            if (type.IsInterface)
            {
                var result = new List<string>();
                if (GetIceTypeId(type) is string firstTypeId)
                {
                    result.Add(firstTypeId);
                    Type[] interfaces = type.GetInterfaces();
                    foreach (Type p in interfaces)
                    {
                        if (GetIceTypeId(p) is string typeId)
                        {
                            result.Add(typeId);
                        }
                    }
                    result.Add("::Ice::Object");
                    result.Sort(StringComparer.Ordinal);
                }
                return result.ToArray();
            }
            else if (type.IsClass)
            {
                var result = new List<string>();
                for (Type? p = type; p != null; p = p.BaseType)
                {
                    if (GetIceTypeId(p) is string typeId)
                    {
                        result.Add(typeId);
                    }
                    else
                    {
                        break; // for
                    }
                }
                return result.ToArray();
            }
            else
            {
                return Array.Empty<string>();
            }
        }
    }
}
