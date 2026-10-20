namespace DCSMyWay.Models;

public sealed record CameraState(DcsPosition Position, bool CrosshairVisible, long? AircraftId);
