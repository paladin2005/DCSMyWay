namespace DCSMyWay.Models;

public sealed record ButtonPress(int Device, int Code, int Delay, double Activate = 1, bool AddDepress = true);
