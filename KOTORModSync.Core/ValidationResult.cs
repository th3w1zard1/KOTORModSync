// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
    public class ValidationResult
    {
        public ValidationResult(
            [NotNull] ComponentValidation validator,
            [NotNull] Instruction instruction,
            [CanBeNull] string message,
            bool isError
        )
        {
            if ( validator == null )
                throw new ArgumentNullException( nameof(validator) );

            Component = validator.ComponentToValidate;
            Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
            InstructionIndex = Component.Instructions.IndexOf( instruction );
            Message = message;
            IsError = isError;

            _ = Logger.LogAsync(
                $"{( IsError ? "[Error]" : "[Warning]" )}"
                + $" Component: '{Component.Name}',"
                + $" Instruction #{InstructionIndex + 1},"
                + $" Action '{instruction.Action}'"
            );
            _ = Logger.LogAsync( $"{( IsError ? "[Error]" : "[Warning]" )} {Message}" );
        }

        public int InstructionIndex { get; }
        public Instruction Instruction { get; }
        public Component Component { get; }
        public string Message { get; }
        public bool IsError { get; }
    }
}
