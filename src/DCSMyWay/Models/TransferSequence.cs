namespace DCSMyWay.Models;

public sealed record TransferSequence(List<ButtonPress> Presses, List<int> WaypointEnteredAfter);
