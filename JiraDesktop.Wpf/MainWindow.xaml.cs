using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using JiraDesktop.Core.Models;

namespace JiraDesktop.Wpf;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;

    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        Loaded += async (_, _) => await _vm.InitializeAsync(this);
        LocationChanged += async (_, _) => await _vm.SaveWindowSettingsAsync(this);
        SizeChanged += async (_, _) => await _vm.SaveWindowSettingsAsync(this);
        Closing += async (_, _) => await _vm.SaveWindowSettingsAsync(this);
    }

    private void IssuesGrid_OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var direction = e.Column.SortDirection != ListSortDirection.Ascending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        e.Column.SortDirection = direction;
        var sortField = string.IsNullOrWhiteSpace(e.Column.SortMemberPath)
            ? e.Column.Header?.ToString() ?? "Updated"
            : e.Column.SortMemberPath;

        _vm.ApplyManualSort(sortField, direction);
    }

    private void IssuesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IssuesGrid.SelectedItem is not WorkItem workItem || !workItem.IsPulseActive)
            return;

        workItem.IsPulseActive = false;
        _vm.RefreshGrid();
    }
}
