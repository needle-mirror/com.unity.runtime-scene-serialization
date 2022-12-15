#if !NET_DOTS
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Reflection;
using Unity.RuntimeSceneSerialization.Internal;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.RuntimeSceneSerialization.CodeGen
{
    static class ReflectedExternalProperty
    {
        public static TypeDefinition Generate(Context context, TypeReference containerType, TypeDefinition externalContainer, IMemberDefinition member, TypeReference externalMember, MethodReference getTypeMethod, string nameOverride)
        {
            if (null == member)
                throw new ArgumentException(nameof(member));

            var effectiveContainerType = externalContainer ?? containerType;
            var memberType = context.Module.ImportReference(Utility.GetMemberType(member).ResolveGenericParameter(containerType));
            var propertyBaseType = context.ImportReference(typeof(ReflectedExternalMemberProperty<,>)).MakeGenericInstanceType(effectiveContainerType, externalMember ?? memberType);
            var memberName = string.IsNullOrEmpty(nameOverride) ? member.Name : nameOverride;

            var type = new TypeDefinition
            (
                string.Empty,
                Utility.GetSanitizedName(memberName, string.Empty),
                TypeAttributes.Class | TypeAttributes.NestedPrivate,
                propertyBaseType
            )
            {
                Scope = containerType.Scope
            };

            string externalContainerTypeName = null;
            if (externalContainer != null)
                externalContainerTypeName = containerType.GetAssemblyQualifiedName().Replace('/', '+');

            var ctorMethod = CreateReflectedMemberPropertyCtorMethod(context, containerType, propertyBaseType, member, memberName, getTypeMethod, externalContainerTypeName);
            type.Methods.Add(ctorMethod);

            return type;
        }

        static MethodDefinition CreateReflectedMemberPropertyCtorMethod(Context context, TypeReference containerType, TypeReference baseType, IMemberDefinition member, string memberName, MethodReference getTypeMethod, string externalContainer)
        {
            // NOTE: We create our own method reference since this assembly may not reference Unity.Properties on it's own. Thus any attempt
            // to Resolve() a TypeReference from Properties will return null. So instead we create MethodReferences for methods we
            // know will exist ourselves and let the new assembly, which will now include a reference to Properties, resolve at runtime
            var basePropertyConstructor = new MethodReference(".ctor", context.ImportReference(typeof(void)), baseType)
            {
                HasThis = true,
                ExplicitThis = false,
                CallingConvention = MethodCallingConvention.Default
            };

            var parameters = basePropertyConstructor.Parameters;
            switch (member)
            {
                case FieldDefinition _:
                    parameters.Add(new ParameterDefinition(context.ImportReference(typeof(FieldInfo))));
                    break;
                case PropertyDefinition _:
                    parameters.Add(new ParameterDefinition(context.ImportReference(typeof(PropertyInfo))));
                    break;
                default:
                    throw new ArgumentException($"No constructor exists for ReflectedMemberProperty({member.GetType()})");
            }

            var method = new MethodDefinition
            (
                ".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                context.ImportReference(typeof(void))
            );

            var il = method.Body.GetILProcessor();

            il.Emit(OpCodes.Ldarg_0); // this

            var hasExternalContainer = !string.IsNullOrEmpty(externalContainer);
            var stringType = context.ImportReference(typeof(string));

            // name
            parameters.Add(new ParameterDefinition(stringType));

            // externalContainerType
            parameters.Add(new ParameterDefinition(stringType));

            if (hasExternalContainer)
            {
                // Type.GetType("AssemblyQualifiedName")
                il.Emit(OpCodes.Ldstr, member.GetResolvedDeclaringType(containerType).GetAssemblyQualifiedName());
                il.Emit(OpCodes.Call, getTypeMethod);
            }
            else
            {
                // typeof({TContainer})
                il.Emit(OpCodes.Ldtoken, context.ImportReference(member.GetResolvedDeclaringType(containerType)));
                il.Emit(OpCodes.Call, context.TypeGetTypeFromTypeHandleMethodReference.Value);
            }

            // {FieldName}
            il.Emit(OpCodes.Ldstr, member.Name);

            var flags = BindingFlags.Instance;

            if (member.IsPublic())
            {
                flags |= BindingFlags.Public;
            }
            else
            {
                flags |= BindingFlags.NonPublic;
            }

            // {bindingFlags}
            il.Emit(OpCodes.Ldc_I4_S, (sbyte) flags);

            switch (member)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                case FieldDefinition _:
                    // GetField
                    il.Emit(OpCodes.Callvirt, context.TypeGetFieldMethodReference.Value);
                    break;
                case PropertyDefinition _:
                    // GetProperty
                    il.Emit(OpCodes.Callvirt, context.TypeGetPropertyMethodReference.Value);
                    break;
            }

            // {name}
            il.Emit(OpCodes.Ldstr, memberName);

            // {externalContainerType}
            if (hasExternalContainer)
                il.Emit(OpCodes.Ldstr, externalContainer);
            else
                il.Emit(OpCodes.Ldnull);

            // : base
            il.Emit(OpCodes.Call, context.Module.ImportReference(basePropertyConstructor));
            il.Emit(OpCodes.Ret);

            return method;
        }
    }
}
#endif
