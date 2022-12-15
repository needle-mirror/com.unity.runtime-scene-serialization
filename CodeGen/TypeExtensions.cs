using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    /// <summary>
    /// Extension methods for Type objects
    /// </summary>
    static class TypeExtensions
    {
        /// <summary>
        /// Gets all fields of the Type or any of its base Types
        /// </summary>
        /// <param name="type">Type we are going to get fields on</param>
        /// <param name="fields">A list to which all fields of this type will be added</param>
        public static void GetFieldsRecursively(this TypeDefinition type, List<FieldDefinition> fields)
        {
            while (true)
            {
                foreach (var field in type.Fields)
                {
                    fields.Add(field);
                }

                var baseType = type.BaseType;
                if (baseType != null)
                {
                    type = baseType.Resolve();
                    if (type == null)
                    {
                        Console.WriteLine($"Error: Could not resolve {baseType}");
                        break;
                    }

                    continue;
                }

                break;
            }
        }

        /// <summary>
        /// Gets all properties of the Type or any of its base Types
        /// </summary>
        /// <param name="type">Type we are going to get properties on</param>
        /// <param name="properties">A list to which all properties of this type will be added</param>
        public static void GetPropertiesRecursively(this TypeDefinition type, List<PropertyDefinition> properties)
        {
            while (true)
            {
                foreach (var property in type.Properties)
                {
                    properties.Add(property);
                }

                var baseType = type.BaseType;
                if (baseType != null)
                {
                    type = baseType.Resolve();
                    if (type == null)
                    {
                        Console.WriteLine($"Error: Could not resolve {baseType}");
                        break;
                    }

                    continue;
                }

                break;
            }
        }
    }
}
