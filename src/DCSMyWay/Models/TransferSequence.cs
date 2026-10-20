namespace DCSMyWay.Models;

public sealed record TransferSequence(List<CockpitStep> Steps, List<int> WaypointEnteredAfter);
