using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;

namespace IxMilia.ModelExplorer.ViewModels;

public class MainViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    public ReactiveCommand<Unit, Unit> MeasureDistanceCommand { get; }
    public ReactiveCommand<Unit, Unit> MeasureAngleCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetViewCommand { get; }

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
        MeasureDistanceCommand = ReactiveCommand.CreateFromTask(MeasureDistance);
        MeasureAngleCommand = ReactiveCommand.CreateFromTask(MeasureAngle);
        ResetViewCommand = ReactiveCommand.Create(ResetView);
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

    public async Task MeasureDistance()
    {
        ModelRendererViewModel.Status = "Select first point";
        var firstPoint = await _interactionManager.GetVector3Async();
        ModelRendererViewModel.Status = "Select second point";
        var secondPoint = await _interactionManager.GetVector3Async();
        var delta = secondPoint - firstPoint;
        var distance = delta.Length();
        ModelRendererViewModel.Status = $"dx: {Math.Abs(delta.X)}, dy: {Math.Abs(delta.Y)}, dz: {Math.Abs(delta.Z)}, dist: {distance}";
    }

    public async Task MeasureAngle()
    {
        ModelRendererViewModel.Status = "Select angle fulcrum point";
        var fulcrum = await _interactionManager.GetVector3Async();
        ModelRendererViewModel.Status = "Select endpoint 1";
        var endpoint1 = await _interactionManager.GetVector3Async();
        ModelRendererViewModel.Status = "Select endpoint 2";
        var endpoint2 = await _interactionManager.GetVector3Async();
        var v1 = endpoint1 - fulcrum;
        var v2 = endpoint2 - fulcrum;
        var dot = Vector3.Dot(v1, v2);
        var denominator = v1.Length() * v2.Length();
        if (denominator == 0.0)
        {
            ModelRendererViewModel.Status = "Vector cannot be zero";
            return;
        }

        var cosTheta = dot / denominator;
        var angleBetweenRadians = Math.Acos(cosTheta);
        var angleBetweenDegrees = angleBetweenRadians * 180.0 / Math.PI;
        ModelRendererViewModel.Status = $"Angle: {angleBetweenDegrees} degrees";
    }

    public void ResetView()
    {
        ModelRendererViewModel.ResetViewTransform();
    }
}
