using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JiraDashboardApp.Core.Models;

namespace JiraDashboardApp.Wpf;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private ListSortDirection _keySortDirection = ListSortDirection.Descending;

    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        Loaded += async (_, _) => await _vm.InitializeAsync(this);

        Deactivated += async (_, _) =>
        {
            if (_vm.AutoHideDrawerOnFocusLoss && _vm.IsDrawerOpen)
            {
                _vm.IsDrawerOpen = false;
                await _vm.SaveSettingsAsync(this);
            }
        };

        LocationChanged += async (_, _) => await _vm.SaveSettingsAsync(this);
        SizeChanged += async (_, _) => await _vm.SaveSettingsAsync(this);
    }

    private void IssuesGrid_OnSorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column.Header?.ToString() == "Key")
        {
            e.Handled = true;
            _keySortDirection = _keySortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            e.Column.SortDirection = _keySortDirection;
            _vm.ApplyManualSort("KeyNumeric", _keySortDirection);
            return;
        }

        e.Handled = true;
        var dir = e.Column.SortDirection != ListSortDirection.Ascending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        e.Column.SortDirection = dir;
        var sortField = string.IsNullOrWhiteSpace(e.Column.SortMemberPath)
            ? e.Column.Header?.ToString() ?? ""
            : e.Column.SortMemberPath;

        _vm.ApplyManualSort(sortField, dir);
    }

    private void IssuesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IssuesGrid.SelectedItem is WorkItem wi)
            _vm.OpenWorkItemInBrowser(wi);
    }

    private void IssuesGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IssuesGrid.SelectedItem is WorkItem wi && wi.IsPulseActive)
        {
            wi.IsPulseActive = false;
            _vm.RefreshGrid();
        }
    }
}