using System;

[AttributeUsage(AttributeTargets.All)]
public class ToDoAttribute : Attribute
{
    public readonly string message;
    public readonly int importance;

    public ToDoAttribute(string message, int importance = 0)
    {
        this.message = message;
        this.importance = importance;
    }
}