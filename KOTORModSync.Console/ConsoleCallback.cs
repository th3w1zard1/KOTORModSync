// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.ConsoleApp
{
    // Wrapper class that implements the callback interfaces using Console.ReadLine
    public class ConsoleCallback :
        CallbackObjects.IConfirmationDialogCallback,
        CallbackObjects.IOptionsDialogCallback
    {
        [CanBeNull]
        public Task<bool?> ShowConfirmationDialog( [NotNull] string message )
        {
            ArgumentNullException.ThrowIfNull( message );

            Console.WriteLine( message );
            ConsoleKeyInfo key = Console.ReadKey();

            bool? result = key.Key switch
            {
                ConsoleKey.Y => true,
                ConsoleKey.N => false,
                _ => null
            };

            return Task.FromResult( result );
        }


        [ItemCanBeNull]
        [NotNull]
        public Task<string> ShowOptionsDialog( [NotNull] List<string> options )
        {
            ArgumentNullException.ThrowIfNull( options );

            Console.WriteLine( "Select an option:" );
            for ( int i = 0; i < options.Count; i++ )
            {
                Console.WriteLine( $"{i + 1}. {options[i]}" );
            }

            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey( intercept: true );
            } while ( !char.IsDigit( key.KeyChar ) );

            int selectedIndex = int.Parse( key.KeyChar.ToString() );
            string selectedOption = options[selectedIndex - 1];

            return Task.FromResult( selectedOption );
        }
    }
}
