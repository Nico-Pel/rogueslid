using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public class YcBoolShowAttribute : PropertyAttribute {

    public string ConditionalSourceField = "";
    public bool ShowWhenEqualToValue;
    public bool IsReadOnly;

    /// <summary>
    /// Show the field when the given bool variable is equal to the specified value. Hide it otherwise.
    /// </summary>
    /// <param name="conditionalSourceField">Name of the variable to check</param>
    /// <param name="showWhenEqualToValue">Value of the bool</param>
    public YcBoolShowAttribute(string conditionalSourceField, bool showWhenEqualToValue) : this(conditionalSourceField, false, showWhenEqualToValue) { }

    /// <summary>
    /// Show the field when the given bool variable is equal to the specified value. Hide it otherwise.
    /// </summary>
    /// <param name="conditionalSourceField">Name of the variable to check</param>
    /// <param name="isReadOnly">Should the field be greyed out instead of hidden</param>
    /// <param name="showWhenEqualToValue">Value of the bool</param>
    public YcBoolShowAttribute(string conditionalSourceField, bool isReadOnly, bool showWhenEqualToValue) {
        this.ConditionalSourceField = conditionalSourceField;
        this.ShowWhenEqualToValue = showWhenEqualToValue;
        this.IsReadOnly = isReadOnly;
    }
}