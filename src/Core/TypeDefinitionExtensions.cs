﻿using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Reflection;

namespace Hiro
{
    /// <summary>
    /// A class that extends the <see cref="TypeDefinition"/>
    /// class with features similar to the features in the
    /// System.Reflection.Emit namespace.
    /// </summary>
    public static class TypeDefinitionExtensions
    {
        /// <summary>
        /// Returns the first constructor defined on the target type.
        /// </summary>
        /// <param name="targetType">The type to search for a default constructor</param>
        /// <returns>The default constructor.</returns>
        public static MethodDefinition GetDefaultConstructor(this TypeDefinition targetType)
        {
            foreach (var method in targetType.Methods)
                if (method.IsConstructor)
                    return method;

            return null;    
        }

        /// <summary>
        /// Adds a default constructor to the target type.
        /// </summary>
        /// <param name="targetType">The type that will contain the default constructor.</param>
        /// <returns>The default constructor.</returns>
        public static MethodDefinition AddDefaultConstructor(this TypeDefinition targetType)
        {
            var parentType = typeof(object);

            return AddDefaultConstructor(targetType, parentType);
        }

        /// <summary>
        /// Adds a default constructor to the target type.
        /// </summary>
        /// <param name="parentType">The base class that contains the default constructor that will be used for constructor chaining..</param>
        /// <param name="targetType">The type that will contain the default constructor.</param>
        /// <returns>The default constructor.</returns>
        public static MethodDefinition AddDefaultConstructor(this TypeDefinition targetType, Type parentType)
        {
            var module = targetType.Module;
            var voidType = module.Import(typeof(void));
            var methodAttributes = Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig
                                   | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName;


            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var objectConstructor = parentType.GetConstructor(flags, null, new Type[0], null);

            // Revert to the System.Object constructor
            // if the parent type does not have a default constructor
            if (objectConstructor == null)
                objectConstructor = typeof(object).GetConstructor(new Type[0]);

            var baseConstructor = module.Import(objectConstructor);

            // Define the default constructor
            var ctor = new MethodDefinition(".ctor", methodAttributes, voidType)
            {
                CallingConvention = MethodCallingConvention.StdCall,
                ImplAttributes = (Mono.Cecil.MethodImplAttributes.IL | Mono.Cecil.MethodImplAttributes.Managed)
            };

            var il = ctor.Body.GetILProcessor();

            // Call the constructor for System.Object, and exit
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Ret);

            targetType.Methods.Add(ctor);

            return ctor;
        }

        /// <summary>
        /// Adds a new method to the <paramref name="typeDef">target type</paramref>.
        /// </summary>
        /// <param name="typeDef">The type that will hold the newly-created method.</param>
        /// <param name="attributes">The <see cref="Mono.Cecil.MethodAttributes"/> parameter that describes the characteristics of the method.</param>
        /// <param name="methodName">The name to be given to the new method.</param>
        /// <param name="returnType">The method return type.</param>        
        /// <param name="parameterTypes">The list of argument types that will be used to define the method signature.</param>
        /// <param name="genericParameterTypes">The list of generic argument types that will be used to define the method signature.</param>
        /// <returns>A <see cref="MethodDefinition"/> instance that represents the newly-created method.</returns>
        public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string methodName,
            Mono.Cecil.MethodAttributes attributes, Type returnType, Type[] parameterTypes, Type[] genericParameterTypes)
        {
            var method = new MethodDefinition(methodName, attributes, typeDef.Module.Import(typeof(object)));

            typeDef.Methods.Add(method);

            //Match the generic parameter types
            foreach (var genericParameterType in genericParameterTypes)
            {
                method.AddGenericParameter(genericParameterType);
            }

            // Match the parameter types
            method.AddParameters(parameterTypes);

            // Match the return type
            method.SetReturnType(returnType);

            return method;
        }

        /// <summary>
        /// Adds a rewritable property to the <paramref name="typeDef">target type</paramref>.
        /// </summary>
        /// <param name="typeDef">The target type that will hold the newly-created property.</param>
        /// <param name="propertyName">The name of the property itself.</param>
        /// <param name="propertyType">The <see cref="System.Type"/> instance that describes the property type.</param>
        public static void AddProperty(this TypeDefinition typeDef, string propertyName, Type propertyType)
        {
            var module = typeDef.Module;
            var typeRef = module.Import(propertyType);
            typeDef.AddProperty(propertyName, typeRef);
        }

        /// <summary>
        /// Adds a rewritable property to the <paramref name="typeDef">target type</paramref>.
        /// </summary>
        /// <param name="typeDef">The target type that will hold the newly-created property.</param>
        /// <param name="propertyName">The name of the property itself.</param>
        /// <param name="propertyType">The <see cref="TypeReference"/> instance that describes the property type.</param>
        public static void AddProperty(this TypeDefinition typeDef, string propertyName,
            TypeReference propertyType)
        {
            #region Add the backing field
            string fieldName = string.Format("__{0}_backingField", propertyName);
            FieldDefinition actualField = new FieldDefinition(fieldName,
                Mono.Cecil.FieldAttributes.Private, propertyType);


            typeDef.Fields.Add(actualField);
            #endregion


            FieldReference backingField = actualField;
            if (typeDef.GenericParameters.Count > 0)
                backingField = GetBackingField(fieldName, typeDef, propertyType);

            var getterName = string.Format("get_{0}", propertyName);
            var setterName = string.Format("set_{0}", propertyName);


            const Mono.Cecil.MethodAttributes attributes = Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig |
                                                Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.NewSlot |
                                                Mono.Cecil.MethodAttributes.Virtual;

            ModuleDefinition module = typeDef.Module;
            TypeReference voidType = module.Import(typeof(void));

            // Implement the getter and the setter
            MethodDefinition getter = AddPropertyGetter(propertyType, getterName, attributes, backingField);
            MethodDefinition setter = AddPropertySetter(propertyType, attributes, backingField, setterName, voidType);

            typeDef.AddProperty(propertyName, propertyType, getter, setter);
        }

        /// <summary>
        /// Adds a rewriteable property to the <paramref name="typeDef">target type</paramref>
        /// using an existing <paramref name="getter"/> and <paramref name="setter"/>.
        /// </summary>
        /// <param name="typeDef">The target type that will hold the newly-created property.</param>
        /// <param name="propertyName">The name of the property itself.</param>
        /// <param name="propertyType">The <see cref="TypeReference"/> instance that describes the property type.</param>
        /// <param name="getter">The property getter method.</param>
        /// <param name="setter">The property setter method.</param>
        public static void AddProperty(this TypeDefinition typeDef, string propertyName, TypeReference propertyType, MethodDefinition getter, MethodDefinition setter)
        {
            var newProperty = new PropertyDefinition(propertyName,
                Mono.Cecil.PropertyAttributes.None, propertyType)
            {
                GetMethod = getter,
                SetMethod = setter
            };

            typeDef.Methods.Add(getter);
            typeDef.Methods.Add(setter);
            typeDef.Properties.Add(newProperty);
        }

        /// <summary>
        /// Creates a property getter method implementation with the
        /// <paramref name="propertyType"/> as the return type.
        /// </summary>
        /// <param name="propertyType">Represents the <see cref="TypeReference">return type</see> for the getter method.</param>
        /// <param name="getterName">The getter method name.</param>
        /// <param name="attributes">The method attributes associated with the getter method.</param>
        /// <param name="backingField">The field that will store the instance that the getter method will retrieve.</param>
        /// <returns>A <see cref="MethodDefinition"/> representing the getter method itself.</returns>
        private static MethodDefinition AddPropertyGetter(TypeReference propertyType,
            string getterName, Mono.Cecil.MethodAttributes attributes, FieldReference backingField)
        {
            var getter = new MethodDefinition(getterName, attributes, propertyType)
            {
                IsPublic = true,
                ImplAttributes = (Mono.Cecil.MethodImplAttributes.Managed | Mono.Cecil.MethodImplAttributes.IL)
            };

            var IL = getter.GetILGenerator();
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, backingField);
            IL.Emit(OpCodes.Ret);

            return getter;
        }

        /// <summary>
        /// Creates a property setter method implementation with the
        /// <paramref name="propertyType"/> as the setter parameter.
        /// </summary>
        /// <param name="propertyType">Represents the <see cref="TypeReference">parameter type</see> for the setter method.</param>
        /// <param name="attributes">The method attributes associated with the setter method.</param>
        /// <param name="backingField">The field that will store the instance for the setter method.</param>
        /// <param name="setterName">The method name of the setter method.</param>
        /// <param name="voidType">The <see cref="TypeReference"/> that represents <see cref="Void"/>.</param>
        /// <returns>A <see cref="MethodDefinition"/> that represents the setter method itself.</returns>
        private static MethodDefinition AddPropertySetter(TypeReference propertyType, Mono.Cecil.MethodAttributes attributes, FieldReference backingField, string setterName, TypeReference voidType)
        {
            var setter = new MethodDefinition(setterName, attributes, voidType)
            {
                IsPublic = true,
                ImplAttributes = (Mono.Cecil.MethodImplAttributes.Managed | Mono.Cecil.MethodImplAttributes.IL)
            };

            setter.Parameters.Add(new ParameterDefinition(propertyType));

            var IL = setter.GetILGenerator();
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Stfld, backingField);
            IL.Emit(OpCodes.Ret);

            return setter;
        }

        /// <summary>
        /// Resolves the backing field for a generic type declaration.
        /// </summary>
        /// <param name="fieldName">The name of the field to reference.</param>
        /// <param name="typeDef">The type that holds the actual field.</param>
        /// <param name="propertyType">The <see cref="TypeReference"/> that describes the property type being referenced.</param>
        /// <returns>A <see cref="FieldReference"/> that points to the actual backing field.</returns>
        private static FieldReference GetBackingField(string fieldName, TypeDefinition typeDef, TypeReference propertyType)
        {
            // If the current type is a generic type, 
            // the current generic type must be resolved before
            // using the actual field
            var declaringType = new GenericInstanceType(typeDef);
            foreach (GenericParameter parameter in typeDef.GenericParameters)
            {
                declaringType.GenericArguments.Add(parameter);
            }

            return new FieldReference(fieldName, propertyType) { DeclaringType = declaringType };
        }

        /// <summary>
        /// Tests if a <see cref="TypeDefinition"/> implements directly a certain <see cref="TypeReference"/>.
        /// </summary>
        /// <param name="type">The <see cref="TypeDefinition"/> to test.</param>
        /// <param name="interfaceType">The <see cref="TypeReference"/> which reprents the interface.</param>
        /// <returns>True if type directly implements interfaceType, False otherwise.</returns>
        public static bool Implements(this TypeDefinition type, TypeReference interfaceType)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (interfaceType == null)
                throw new ArgumentNullException("interfaceType");

            foreach (var reference in type.Interfaces)
                if (interfaceType.IsEquivalentTo(reference))
                    return true;

            return false;
        }
    }
}
