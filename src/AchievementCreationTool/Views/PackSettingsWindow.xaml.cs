using System.Windows;
using AchievementCreationTool.Model;

namespace AchievementCreationTool.Views;

public partial class PackSettingsWindow : Window
{
    public PackSettingsWindow(Pack pack)
    {
        InitializeComponent();
        DataContext = pack;
        HelpWindow.HookF1(this, "start");
    }

    void OnHelp(object sender, RoutedEventArgs e) => HelpWindow.ShowTopic(this, (sender as FrameworkElement)?.Tag as string ?? "start");

    void OnDone(object sender, RoutedEventArgs e) => Close();
}
