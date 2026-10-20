using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DCSMyWay;

public sealed partial class MainPage : Page
{
  private int _clickCount;

  public MainPage()
  {
    InitializeComponent();

    RuntimeInfoText.Text = $".NET {Environment.Version}";
  }

  private void TestButton_Click(object sender, RoutedEventArgs e)
  {
    _clickCount++;
    ClickCountText.Text = $"Clicked {_clickCount} time(s)";
  }
}
