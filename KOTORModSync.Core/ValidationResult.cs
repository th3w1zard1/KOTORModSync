using JetBrains.Annotations;

namespace KOTORModSync.Core
{
    public class ValidationResult
    {
        public ValidationResult(
            ComponentValidation validator,
            Instruction instruction,
            [CanBeNull] string message,
            bool isError
        )
        {
            Component = validator.ComponentToValidate;
            Instruction = instruction;
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
