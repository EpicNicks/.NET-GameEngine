﻿using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

using NEngineEditor.Model.JsonSerialized;

namespace NEngineEditor.Windows;
/// <summary>
/// Interaction logic for ProjectOpenWindow.xaml
/// </summary>
public partial class ProjectOpenWindow : Window
{
    private static readonly JsonSerializerOptions NEW_PROJECT_JSON_OPTIONS = new() { WriteIndented = true };

    public ProjectOpenWindow()
    {
        InitializeComponent();
        BaseFilePathTextBox.TextChanged += BaseFilePathTextBox_TextChanged;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            BaseFilePathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void BaseFilePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Refresh();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void CreateNewProject_Click(object sender, RoutedEventArgs e)
    {
        //System.Windows.MessageBox.Show("Create new project functionality to be implemented.");
        SetupNewProject();
    }

    private void Refresh()
    {
        var basePath = BaseFilePathTextBox.Text;
        ProjectListBox.Items.Clear();

        if (Directory.Exists(basePath))
        {
            var projectDirs = Directory.GetDirectories(basePath)
                .Where(dir => File.Exists(Path.Combine(dir, "NEngineProject.json")));

            foreach (var dir in projectDirs)
            {
                ProjectListBox.Items.Add(new ListBoxItem { Content = Path.GetFileName(dir), Tag = dir });
            }
        }
    }

    private void SetupNewProject()
    {
        var basePath = BaseFilePathTextBox.Text;
        // revalidate
        if (Directory.Exists(basePath))
        {
            NewProjectDialog newProjectDialog = new NewProjectDialog();
            if (newProjectDialog.ShowDialog() == true)
            {
                if (newProjectDialog.ProjectName is null)
                {
                    System.Windows.MessageBox.Show("The Project Name was null somehow", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string projectName = newProjectDialog.ProjectName;
                string sanitizedProjectName = projectName.Replace(" ", "_");
                // Create the new project directory and NEngineProject.json file
                var projectPath = Path.Combine(BaseFilePathTextBox.Text, projectName);
                var assetsPath = Path.Combine(projectPath, "Assets");
                if (!Directory.Exists(projectPath))
                {
                    Directory.CreateDirectory(projectPath);
                    Directory.CreateDirectory(assetsPath);

                    NEngineProject projectData = new()
                    {
                        ProjectName = projectName,
                        EngineVersion = "0.0.1"
                        // placeholder since we aren't doing versioning yet anyway, not even sure how to version between the engine core and editor right now anyway
                        //  engine version selection would be selected or populated once it matters and the editor has a reference to it
                    };

                    // TODO: generate Main class with Main method which the editor needs to provide with the Scenes something like
                    //  Application.WindowName = <project property window name>
                    //  List<SceneData> = TraverseAndDeserializeScenesInProjectFolders()
                    //  foreach SceneData s => Application.AddScene(s.ToScene())
                    //  Application.Run()
                    Task[] fileTasks =
                    [
                        File.WriteAllTextAsync(Path.Combine(projectPath, $"{sanitizedProjectName}.csproj"), Properties.Resources.CsProjTemplate_csproj),
                        File.WriteAllTextAsync(Path.Combine(projectPath, "NEngineProject.json"), JsonSerializer.Serialize(projectData, NEW_PROJECT_JSON_OPTIONS)),
                    ];
                    Task.WaitAll(fileTasks);
                    // Add the new project to the list
                    ProjectListBox.Items.Add(new ListBoxItem { Content = projectName, Tag = projectPath });
                }
                else
                {
                    System.Windows.MessageBox.Show("A project with that name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void ProjectListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProjectListBox.SelectedItem is ListBoxItem selectedItem)
        {
            var projectPath = selectedItem.Tag as string;
            if (projectPath is not null)
            {
                OpenProjectWindow(projectPath);
            }
            else
            {
                System.Windows.MessageBox.Show("The Project Path was null somehow", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    private void OpenProjectWindow(string projectPath)
    {
        var projectWindow = new MainWindow(projectPath);
        projectWindow.Show();
    }
}
