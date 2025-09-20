namespace TaskMaster.Models;

public class PanelResult
{
    public bool Success { get; set; }
    public string DecisionPath { get; set; } = string.Empty;
    public string PanelOutput { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class PanelRequest
{
    public string SpecPath { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public int Rounds { get; set; } = 2;
    public string Scope { get; set; } = "src/";
    public string RepoRoot { get; set; } = string.Empty;
}