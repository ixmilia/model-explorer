using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace IxMilia.ModelExplorer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> MeasureCommand { get; }

    private static FilePickerFileType StlFilePickerType;

    private Control _control;
    public ModelRendererViewModel ModelRendererViewModel { get; }
    private UserInteractionManager _interactionManager;

    static MainViewModel()
    {
        StlFilePickerType = new FilePickerFileType("STL models")
        {
            Patterns = new[] { "*.stl" },
        };
    }

    public MainViewModel(Control control, UserInteractionManager interactionManager)
    {
        _control = control;
        _interactionManager = interactionManager;
        ModelRendererViewModel = new ModelRendererViewModel();
        OpenCommand = ReactiveCommand.CreateFromTask(Open);
        MeasureCommand = ReactiveCommand.CreateFromTask(Measure);
    }

    public async Task Open()
    {
        var topLevel = TopLevel.GetTopLevel(_control);
        if (topLevel is not null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Open model",
                AllowMultiple = false,
                FileTypeFilter = new FilePickerFileType[]
                {
                    StlFilePickerType,
                },
            });
            if (files?.Count > 0)
            {
                var file = files.First();
                var fileStream = await file.OpenReadAsync();
                var model = Model.FromNameAndStream(file.Name, fileStream);
                ModelRendererViewModel.Model = model;
            }
        }
    }

    public async Task Measure()
    {
        ModelRendererViewModel.Status = "Select first point";
        var firstPoint = await _interactionManager.GetVector3Async();
        ModelRendererViewModel.Status = "Select second point";
        var secondPoint = await _interactionManager.GetVector3Async();
        var delta = secondPoint - firstPoint;
        var distance = delta.Length();
        ModelRendererViewModel.Status = $"dx: {Math.Abs(delta.X)}, dy: {Math.Abs(delta.Y)}, dz: {Math.Abs(delta.Z)}, dist: {distance}";
    }
}
