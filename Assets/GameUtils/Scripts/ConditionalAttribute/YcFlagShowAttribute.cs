using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public class YcFlagShowAttribute : PropertyAttribute {

    public string ConditionalSourceField = "";
    public int[] ShowWhenEqualToValues;
    public bool IsReadOnly;

    /// <summary>
    /// Show the field when the given flag variable includes any of the specified values. Hide it otherwise.
    /// </summary>
    /// <param name="conditionalSourceField">Name of the variable to check</param>
    /// <param name="enumValues">The enum values to check</param>
    public YcFlagShowAttribute(string conditionalSourceField, params object[] enumValues) : this(conditionalSourceField, false, enumValues) { }

    /// <summary>
    /// Show the field when the given flag variable includes any of the specified values. Hide it otherwise.
    /// </summary>
    /// <param name="conditionalSourceField">Name of the variable to check</param>
    /// <param name="isReadOnly">Should the field be greyed out instead of hidden</param>
    /// <param name="enumValues">The enum values to check</param>
    public YcFlagShowAttribute(string conditionalSourceField, bool isReadOnly, params object[] enumValues) {
        this.ConditionalSourceField = conditionalSourceField;
        this.ShowWhenEqualToValues = Array.ConvertAll(enumValues, item => (int)item);
        this.IsReadOnly = isReadOnly;
    }
}
