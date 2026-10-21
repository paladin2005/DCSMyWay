namespace DCSMyWay.Models;

public sealed record CockpitState(
  string? Module,
  int? ActiveSteerpoint,
  bool IsEnteringWaypoints,
  int? TransferId,
  int PressesDone,
  string? TransferFailure = null,
  int? Seat = null);
