namespace MovieNightPicker.Core.Models;

/// <summary>A person (cast or crew) referenced by the discovery filters.</summary>
public sealed record Person(
    int Id,
    string Name,
    string? ProfilePath,
    string? KnownForDepartment);
