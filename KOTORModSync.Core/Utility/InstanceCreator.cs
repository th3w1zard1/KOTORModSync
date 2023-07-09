// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KOTORModSync.Core.Utility
{
    public static class InstanceCreator
    {
        private static readonly Dictionary<Type, ConstructorInfo[]> s_cachedConstructors
            = new Dictionary<Type, ConstructorInfo[]>();

        public static T CreateInstance<T>( params object[] constructorParameters )
        {
            Type type = typeof( T );

            if ( type.IsGenericType
                && type.GetGenericTypeDefinition().GetConstructor( Type.EmptyTypes ) != null )
            {
                Type[] genericArguments = type.GetGenericArguments();
                Type genericTypeDefinition = type.GetGenericTypeDefinition();
                Type genericType = genericTypeDefinition.MakeGenericType( genericArguments );

                return (T)Activator.CreateInstance( genericType );
            }

            if ( type.GetConstructor( Type.EmptyTypes ) != null )
            {
                return (T)Activator.CreateInstance( type );
            }

            if ( !s_cachedConstructors.TryGetValue( type, out ConstructorInfo[] constructors ) )
            {
                constructors = type.GetConstructors();
                s_cachedConstructors[type] = constructors;
            }

            int parameterCount = constructorParameters.Length;
            foreach ( ConstructorInfo constructor in
                     from constructor in constructors
                     let parameters = constructor.GetParameters()
                     where parameters.Length == parameterCount
                     let match = AreParametersCompatible( parameters, constructorParameters )
                     where match
                     select constructor )
            {
                return (T)constructor.Invoke( constructorParameters );
            }

            Logger.Log( $"No suitable constructor found for type '{type.Name}' with the provided parameters." );
            return default;
        }

        private static bool AreParametersCompatible( ParameterInfo[] parameters, object[] constructorParameters )
        {
            for ( int i = 0; i < parameters.Length; i++ )
            {
                ParameterInfo parameter = parameters[i];
                object constructorParameter = constructorParameters[i];

                if ( parameter.ParameterType.IsAssignableFrom( constructorParameter?.GetType() ) )
                {
                    continue;
                }

                if ( parameter.HasDefaultValue
                    && constructorParameter is null )
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
