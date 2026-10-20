namespace DCSMyWay.Models;

public sealed record DcsState(DcsPosition Position, bool CrosshairVisible, bool IsEnteringWaypoints, long? AircraftId);
