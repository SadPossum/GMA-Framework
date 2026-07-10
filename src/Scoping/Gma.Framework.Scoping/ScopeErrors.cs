namespace Gma.Framework.Scoping;

using Gma.Framework.Results;

public static class ScopeErrors
{
    public static readonly Error ScopeRequired = new(
        "Scope.Required",
        "A scope id is required.");

    public static readonly Error ScopeInvalid = new(
        "Scope.Invalid",
        "The scope id is invalid.");
}
