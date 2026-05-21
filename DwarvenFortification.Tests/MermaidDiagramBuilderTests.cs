namespace DwarvenFortification.Tests;

// Mermaid diagram tests are disabled while DwarvenFortification.GOAP is being rewritten as a
// GOAP+HTN hybrid. GoapPlanDiagram and GoapPlanSettings are not currently part of the GOAP
// project (GoapPlanDiagram.cs is fully commented out), so these tests cannot compile against
// the new API. Re-enable once the diagram builder returns.
[TestFixture]
public sealed class MermaidDiagramBuilderTests
{
[Test, Ignore("Pending GOAP+HTN rewrite: GoapPlanDiagram is currently commented out in the GOAP project.")]
public void DiagramGenerationPendingReimplementation()
{
}
}
