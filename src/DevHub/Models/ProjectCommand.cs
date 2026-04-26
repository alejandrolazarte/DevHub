namespace DevHub.Models;

public enum ProjectType { Angular, React, Vue, DotNet, Node, Unknown }

public enum CommandSource { AutoDetected, PackageJson, Custom }

public record ProjectCommand(string Name, string Command, CommandSource Source);