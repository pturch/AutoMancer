// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace GridHost;

// A WPF window with a 3-column DataGrid, a multi-select ListBox, a CheckBox, a ComboBox, a Slider and a ProgressBar, as live targets for the Stage 2.8 grid, selection, toggle, expand and range-value actions; WPF's UIA peers implement these patterns fully, unlike WinForms' ListView.
public static class Program
{
    // One row of the DataGrid; the property names become its column headers.
    private sealed record Fruit(string Name, string Colour, string Price);

    // Builds the window in code (no XAML) and runs it.
    [STAThread]
    public static void Main()
    {
        var grid = new DataGrid
        {
            ItemsSource = new[] { new Fruit("Apple", "Red", "1.20"), new Fruit("Banana", "Yellow", "0.50"), new Fruit("Cherry", "Dark red", "4.00"), new Fruit("Plum", "Purple", "2.10") },
            IsReadOnly = true, CanUserAddRows = false, Height = 130,
        };
        AutomationProperties.SetAutomationId(grid, "fruitGrid");

        var list = new ListBox { SelectionMode = SelectionMode.Extended, Height = 90, ItemsSource = new[] { "Alpha", "Beta", "Gamma", "Delta" } };
        AutomationProperties.SetAutomationId(list, "greekList");

        var check = new CheckBox { Content = "Enable logging", Margin = new Thickness(0, 10, 0, 0) };
        AutomationProperties.SetAutomationId(check, "loggingCheck");

        var combo = new ComboBox { ItemsSource = new[] { "Small", "Medium", "Large" }, SelectedIndex = 0, Margin = new Thickness(0, 10, 0, 0) };
        AutomationProperties.SetAutomationId(combo, "sizeCombo");

        var slider = new Slider { Minimum = 0, Maximum = 10, Value = 3, Margin = new Thickness(0, 10, 0, 0) };
        AutomationProperties.SetAutomationId(slider, "volumeSlider");

        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 40, Height = 16, Margin = new Thickness(0, 10, 0, 0) };
        AutomationProperties.SetAutomationId(progress, "loadProgress");

        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(grid);
        panel.Children.Add(list);
        panel.Children.Add(check);
        panel.Children.Add(combo);
        panel.Children.Add(slider);
        panel.Children.Add(progress);

        new Application().Run(new Window { Title = "GridHost", Width = 520, Height = 500, Content = panel });
    }
}
