namespace HspCopier.App.Views.Ball.Weather;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

public partial class FoggyAnimation : UserControl
{
    public FoggyAnimation()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var enter = (Storyboard)Resources["EnterStoryboard"];
        var idle = (Storyboard)Resources["IdleStoryboard"];
        enter.Completed += (_, _) => idle.Begin(this);
        enter.Begin(this);
    }
}
