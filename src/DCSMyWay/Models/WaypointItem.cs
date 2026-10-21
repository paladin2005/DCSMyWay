using System.ComponentModel;

namespace DCSMyWay.Models;

public sealed class WaypointItem(Waypoint waypoint) : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;

  public Waypoint Waypoint
  {
    get;
    set
    {
      if (field == value) return;
      field = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Waypoint)));
    }
  } = waypoint;

  public bool ShowsTargetToggle
  {
    get;
    set
    {
      if (field == value) return;
      field = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowsTargetToggle)));
    }
  }

  public bool IsTransferred
  {
    get;
    set
    {
      if (field == value) return;
      field = value;
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTransferred)));
    }
  }
}
