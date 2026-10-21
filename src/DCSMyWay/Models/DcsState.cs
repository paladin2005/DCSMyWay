namespace DCSMyWay.Models;

public sealed record DcsState(
  DcsPosition Position,
  bool CrosshairVisible,
  bool IsEnteringWaypoints,
  long? AircraftId,
  int? ActiveSteerpoint,
  int? TransferId,
  int PressesDone,
  string? TransferFailure = null,
  int? Seat = null);
