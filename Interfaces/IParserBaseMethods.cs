using Parser.Models;
using System.Collections.Generic;

namespace Parser.Interfaces;

/// <summary>
/// Defines base methods for parser components that manage scopes and variables.
/// </summary>
public interface IParserBaseMethods {
    /// <summary>
    /// Checks if a variable with the specified name exists in the current scope.
    /// </summary>
    /// <param name="variable">The name of the variable to check.</param>
    /// <returns>true if the variable exists; otherwise, false.</returns>
    bool ExistsVariable(string variable);

    /// <summary>
    /// Gets the dictionary of scopes managed by the parser.
    /// </summary>
    /// <returns>A dictionary where keys are scope names and values are sets of variables within that scope.</returns>
    Dictionary<string, HashSet<Variable>> GetScopes();
}