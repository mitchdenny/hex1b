using Hex1b;
using Hex1b.Input;
using Hex1b.Automation;
using TaskFlow;

namespace TaskFlow.Tests;

/// <summary>
/// Integration tests for the TaskFlow application using Hex1b's testing infrastructure.
/// </summary>
public class TaskFlowAppTests
{
    private static (Hex1bTerminal terminal, Hex1bApp app, AppState state) CreateApp(int width = 120, int height = 30)
    {
        var terminal = new Hex1bTerminal(width, height);
        var state = SampleData.CreateSampleState();
        var app = new Hex1bApp(
            ctx => Task.FromResult(TaskFlowApp.Build(ctx, state)),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );
        return (terminal, app, state);
    }

    [Fact]
    public async Task App_InitialRender_ShowsProjectList()
    {
        // Arrange
        var (terminal, app, _) = CreateApp();
        
        // Act
        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Projects"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal);
        
        cts.Cancel();
        await runTask;
        
        // Assert
        Assert.Contains("Projects", terminal.GetScreenText());
        Assert.Contains("Website Redesign", terminal.GetScreenText());
    }

    [Fact]
    public async Task App_InitialRender_ShowsMenuBar()
    {
        // Arrange
        var (terminal, app, _) = CreateApp();
        
        // Act
        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal);
        
        cts.Cancel();
        await runTask;
        
        // Assert
        var screen = terminal.GetScreenText();
        Assert.Contains("File", screen);
        Assert.Contains("Edit", screen);
        Assert.Contains("View", screen);
        Assert.Contains("Help", screen);
    }

    [Fact]
    public async Task App_InitialRender_ShowsStatusBar()
    {
        // Arrange
        var (terminal, app, state) = CreateApp();
        
        // Act
        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("tasks completed"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal);
        
        cts.Cancel();
        await runTask;
        
        // Assert
        Assert.Contains("tasks completed", terminal.GetScreenText());
    }

    [Fact]
    public async Task App_InitialRender_ShowsFirstProjectTasks()
    {
        // Arrange
        var (terminal, app, _) = CreateApp();
        
        // Act
        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Design new homepage"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal);
        
        cts.Cancel();
        await runTask;
        
        // Assert
        var screen = terminal.GetScreenText();
        Assert.Contains("Design new homepage", screen);
        Assert.Contains("Implement responsive navigation", screen);
    }

    [Fact]
    public async Task TaskList_SearchFilter_FiltersResults()
    {
        // Arrange
        var (terminal, app, state) = CreateApp();
        
        // Act
        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        // Wait for initial render and type in search box
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Website Redesign"), TimeSpan.FromSeconds(2))
            .Tab()  // Navigate to search
            .Type("navigation")
            .Wait(100)
            .Build()
            .ApplyAsync(terminal);
        
        cts.Cancel();
        await runTask;
        
        // Assert - search query should be set
        Assert.Equal("navigation", state.SearchQuery);
    }

    [Fact]
    public async Task ProjectSidebar_SelectProject_ChangesTaskList()
    {
        // Arrange
        var (terminal, app, state) = CreateApp();
        
        // Act - Select second project
        state.SelectProject(1);
        
        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mobile App"), TimeSpan.FromSeconds(2))
            .Build()
            .ApplyAsync(terminal);
        
        cts.Cancel();
        await runTask;
        
        // Assert
        Assert.Equal("Mobile App v2.0", state.SelectedProject?.Name);
        Assert.Contains("Mobile App", terminal.GetScreenText());
    }

    [Fact]
    public void State_AddTask_CreatesNewTask()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        var initialCount = state.SelectedProject!.Tasks.Count;
        
        // Act
        state.AddTask("New test task");
        
        // Assert
        Assert.Equal(initialCount + 1, state.SelectedProject.Tasks.Count);
        Assert.Contains(state.SelectedProject.Tasks, t => t.Title == "New test task");
    }

    [Fact]
    public void State_ToggleTaskCompletion_UpdatesStatus()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        var task = state.SelectedProject!.Tasks.First(t => !t.IsCompleted);
        
        // Act
        state.ToggleTaskCompletion(task);
        
        // Assert
        Assert.True(task.IsCompleted);
        Assert.Equal(TaskStatus.Done, task.Status);
    }

    [Fact]
    public void State_DeleteTask_RemovesFromList()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        var task = state.SelectedProject!.Tasks.First();
        var initialCount = state.SelectedProject.Tasks.Count;
        
        // Act
        state.DeleteTask(task);
        
        // Assert
        Assert.Equal(initialCount - 1, state.SelectedProject.Tasks.Count);
        Assert.DoesNotContain(task, state.SelectedProject.Tasks);
    }

    [Fact]
    public void State_AddProject_CreatesAndSelects()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        var initialCount = state.Projects.Count;
        
        // Act
        state.AddProject("New Project");
        
        // Assert
        Assert.Equal(initialCount + 1, state.Projects.Count);
        Assert.Equal("New Project", state.SelectedProject?.Name);
    }

    [Fact]
    public void State_FilteredTasks_RespectsShowCompleted()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        var allTasks = state.FilteredTasks.Count();
        
        // Act
        state.ShowCompletedTasks = false;
        var filteredTasks = state.FilteredTasks.Count();
        
        // Assert
        Assert.True(filteredTasks <= allTasks);
        Assert.DoesNotContain(state.FilteredTasks, t => t.IsCompleted);
    }

    [Fact]
    public void State_FilteredTasks_RespectsSearchQuery()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        
        // Act
        state.SearchQuery = "navigation";
        var results = state.FilteredTasks.ToList();
        
        // Assert
        Assert.All(results, t => 
            Assert.True(
                t.Title.Contains("navigation", StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains("navigation", StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    [Fact]
    public void State_FilteredTasks_RespectsPriorityFilter()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        
        // Act
        state.FilterPriority = Priority.High;
        var results = state.FilteredTasks.ToList();
        
        // Assert
        Assert.All(results, t => Assert.Equal(Priority.High, t.Priority));
    }

    [Fact]
    public void State_LogActivity_AddsEntry()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        var initialCount = state.ActivityLog.Count;
        
        // Act
        state.LogActivity("Test Action", "Test Details", "🧪");
        
        // Assert
        Assert.Equal(initialCount + 1, state.ActivityLog.Count);
        Assert.Equal("Test Action", state.ActivityLog[0].Action);
        Assert.Equal("Test Details", state.ActivityLog[0].Details);
        Assert.Equal("🧪", state.ActivityLog[0].Icon);
    }

    [Fact]
    public void State_Statistics_CalculatesCorrectly()
    {
        // Arrange
        var state = SampleData.CreateSampleState();
        
        // Act
        var totalTasks = state.TotalTasksAcrossProjects;
        var completedTasks = state.CompletedTasksAcrossProjects;
        
        // Assert
        Assert.True(totalTasks > 0);
        Assert.True(completedTasks >= 0);
        Assert.True(completedTasks <= totalTasks);
    }

    [Fact]
    public void Project_ProgressPercentage_CalculatesCorrectly()
    {
        // Arrange
        var project = new Project
        {
            Name = "Test",
            Tasks =
            [
                new TaskItem { IsCompleted = true },
                new TaskItem { IsCompleted = true },
                new TaskItem { IsCompleted = false },
                new TaskItem { IsCompleted = false }
            ]
        };
        
        // Assert
        Assert.Equal(50, project.ProgressPercentage);
        Assert.Equal(2, project.CompletedTasks);
        Assert.Equal(2, project.RemainingTasks);
    }

    [Fact]
    public void Project_EmptyProject_ZeroProgress()
    {
        // Arrange
        var project = new Project { Name = "Empty" };
        
        // Assert
        Assert.Equal(0, project.ProgressPercentage);
        Assert.Equal(0, project.TotalTasks);
    }
}
