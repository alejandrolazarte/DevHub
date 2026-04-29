namespace DevHub.Models;

public enum ConsoleLineKind { Input, Output, Error, System }

public record ConsoleLine(string Text, ConsoleLineKind Kind, DateTimeOffset Timestamp)
{
    public static ConsoleLine FromInput(string text)  => new(text, ConsoleLineKind.Input,  DateTimeOffset.Now);
    public static ConsoleLine FromOutput(string text) => new(text, ConsoleLineKind.Output, DateTimeOffset.Now);
    public static ConsoleLine FromError(string text)  => new(text, ConsoleLineKind.Error,  DateTimeOffset.Now);
    public static ConsoleLine FromSystem(string text) => new(text, ConsoleLineKind.System, DateTimeOffset.Now);
}
