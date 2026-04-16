namespace Leontes.Application.Configuration;

public sealed class PersonaOptions
{
    public const string SectionName = "Persona";

    public string InstructionsFile { get; set; } = "persona.md";
    public double ConfidenceThreshold { get; set; } = 0.7;
    public ProactivityLevel ProactivityLevel { get; set; } = ProactivityLevel.Balanced;
    public Dictionary<string, StageSettings> StageSettings { get; set; } = new();
}

public enum ProactivityLevel
{
    Minimal,
    Balanced,
    Proactive
}

public sealed class StageSettings
{
    public string ModelTier { get; set; } = "Large";
    public float Temperature { get; set; } = 0.5f;
}
