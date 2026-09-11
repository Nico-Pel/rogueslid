using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public class YcPredicateShowAttribute : PropertyAttribute {

    public string Predicate = "";
    public bool IsReadOnly;

    /// <summary>
    /// Show the field if the given predicate is true. Hide it otherwise.<br />
    /// - Use '@' in front of any variable<br />
    /// - Strings must be between simple quotes and not double quotes<br />
    /// - Be careful, null comparison is not handled yet<br />
    /// </summary>
    /// <param name="predicate">The string predicate to analyse.</param>
    /// <param name="isReadOnly">Should the field be greyed out instead of hidden</param>
    public YcPredicateShowAttribute(string predicate, bool isReadOnly = false) {
        this.Predicate = predicate;
        this.IsReadOnly = isReadOnly;
    }
}