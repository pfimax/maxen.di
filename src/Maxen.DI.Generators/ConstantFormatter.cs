using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Maxen.DI.Generators
{
    /// <summary>Turns a constant read from an attribute back into C# source text.</summary>
    internal static class ConstantFormatter
    {
        public static string Format(TypedConstant constant)
        {
            if (constant.IsNull) return "null";

            switch (constant.Kind)
            {
                case TypedConstantKind.Type:
                    return "typeof(" + ((ITypeSymbol)constant.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")";

                case TypedConstantKind.Enum:
                    // An enum constant is stored as its number, so cast it back to the enum type.
                    var enumType = constant.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    return "(" + enumType + ")" + System.Convert.ToString(constant.Value, CultureInfo.InvariantCulture);

                default:
                    return SymbolDisplay.FormatPrimitive(constant.Value!, quoteStrings: true, useHexadecimalNumbers: false);
            }
        }
    }
}
