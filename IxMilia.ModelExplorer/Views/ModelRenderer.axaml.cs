using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using IxMilia.ModelExplorer.ViewModels;
using ReactiveUI;

namespace IxMilia.ModelExplorer.Views
{
    public partial class ModelRenderer : Control
    {
        public static readonly StyledProperty<Color> BackgroundColorProperty = AvaloniaProperty.Register<ModelRenderer, Color>(nameof(Color));
        public static readonly StyledProperty<Pen> LinePenProperty = AvaloniaProperty.Register<ModelRenderer, Pen>(nameof(LinePen));
        public static readonly StyledProperty<Pen> VertexPenProperty = AvaloniaProperty.Register<ModelRenderer, Pen>(nameof(VertexPen));

        static ModelRenderer()
        {
            AffectsRender<ModelRenderer>(BackgroundColorProperty);
            AffectsRender<ModelRenderer>(LinePenProperty);
            AffectsRender<ModelRenderer>(VertexPenProperty);
        }

        private ModelRendererViewModel? _viewModel;
        private Brush? _backgroundBrush;
        private FormattedText _xAxisLabel;
        private FormattedText _yAxisLabel;
        private FormattedText _zAxisLabel;
        private Pen _xAxisPen;
        private Pen _yAxisPen;
        private Pen _zAxisPen;
        private Model? _transformedModel;
        private Vector3? _highlightVertex;
        private bool _isPanning;
        private bool _isRotating;
        private Point _lastCursorPosition;

        internal UserInteractionManager InteractionManager { get; set; }

        public Color BackgroundColor
        {
            get => GetValue(BackgroundColorProperty);
            set
            {
                _backgroundBrush = new SolidColorBrush(value);
                SetValue(BackgroundColorProperty, value);
            }
        }

        public Pen LinePen
        {
            get => GetValue(LinePenProperty);
            set => SetValue(LinePenProperty, value);
        }

        public Pen VertexPen
        {
            get => GetValue(VertexPenProperty);
            set => SetValue(VertexPenProperty, value);
        }

        public ModelRenderer()
        {
            var textBrush = new SolidColorBrush(Colors.White);
            _xAxisLabel = new FormattedText("X", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.0, textBrush);
            _yAxisLabel = new FormattedText("Y", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.0, textBrush);
            _zAxisLabel = new FormattedText("Z", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10.0, textBrush);
            _xAxisPen = new Pen(Colors.Red.ToUInt32(), thickness: 1.0);
            _yAxisPen = new Pen(Colors.Green.ToUInt32(), thickness: 1.0);
            _zAxisPen = new Pen(Colors.Blue.ToUInt32(), thickness: 1.0);
            DataContextChanged += (_sender, _e) => Bind();
            Bind();
            InitializeComponent();
        }

        private void Bind()
        {
            if (DataContext is ModelRendererViewModel modelRendererViewModel)
            {
                _viewModel = modelRendererViewModel;
                _viewModel.PropertyChanged += (_sender, e) =>
                {
                    if (e.PropertyName == nameof(ModelRendererViewModel.Model) ||
                        e.PropertyName == nameof(ModelRendererViewModel.ViewTransform))
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                RecalculateVertices();
                                Dispatcher.UIThread.Post(() => InvalidateVisual());
                            }
                            catch (OperationCanceledException)
                            {
                            }
                        });
                    }
                };
            }
        }

        public override void Render(DrawingContext context)
        {
            var token = GetNewToken();

            var sw = new Stopwatch();
            sw.Start();

            context.FillRectangle(_backgroundBrush, new Rect(0, 0, Bounds.Width, Bounds.Height));
            var transform = GetCorrectedTransform();

            // model
            var transformedModel = _transformedModel;
            if (transformedModel != null)
            {
                for (int i = 0; i < transformedModel.Triangles.Length; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var triangle = transformedModel.Triangles[i];
                    var v1 = transformedModel.Vertices[triangle.V1].ToPoint();
                    var v2 = transformedModel.Vertices[triangle.V2].ToPoint();
                    var v3 = transformedModel.Vertices[triangle.V3].ToPoint();

                    context.DrawLine(LinePen, v1, v2);
                    context.DrawLine(LinePen, v2, v3);
                    context.DrawLine(LinePen, v3, v1);
                }
            }

            // highlighted vertex
            var localHighlightVertex = _highlightVertex;
            if (localHighlightVertex.HasValue)
            {
                var vertexSize = 5.0;
                var localHighlightVertexValue = localHighlightVertex.GetValueOrDefault().Transform(transform);
                context.DrawLine(
                    VertexPen,
                    new Point(localHighlightVertexValue.X - vertexSize, localHighlightVertexValue.Y - vertexSize),
                    new Point(localHighlightVertexValue.X + vertexSize, localHighlightVertexValue.Y + vertexSize));
                context.DrawLine(
                    VertexPen,
                    new Point(localHighlightVertexValue.X - vertexSize, localHighlightVertexValue.Y + vertexSize),
                    new Point(localHighlightVertexValue.X + vertexSize, localHighlightVertexValue.Y - vertexSize));
            }

            // axes
            var origin = Vector3.Transform(Vector3.Zero, transform);
            var xaxisDirection = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, transform) - origin);
            var yaxisDirection = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, transform) - origin);
            var zaxisDirection = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, transform) - origin);
            var axisSize = 50.0f;
            var axisCenter = new Vector3(axisSize, (float)Bounds.Height - axisSize, 0.0f);
            var xAxisEndPoint = (xaxisDirection * axisSize + axisCenter).ToPoint();
            var yAxisEndPoint = (yaxisDirection * axisSize + axisCenter).ToPoint();
            var zAxisEndPoint = (zaxisDirection * axisSize + axisCenter).ToPoint();
            context.DrawText(_xAxisLabel, xAxisEndPoint);
            context.DrawText(_yAxisLabel, yAxisEndPoint);
            context.DrawText(_zAxisLabel, zAxisEndPoint);
            context.DrawLine(_xAxisPen, axisCenter.ToPoint(), xAxisEndPoint);
            context.DrawLine(_yAxisPen, axisCenter.ToPoint(), yAxisEndPoint);
            context.DrawLine(_zAxisPen, axisCenter.ToPoint(), zAxisEndPoint);

            sw.Stop();
            var elapsed = sw.ElapsedMilliseconds;
            var fps = elapsed == 0.0 ? double.PositiveInfinity : 1000.0 / elapsed;
            if (_viewModel is not null)
            {
                Dispatcher.UIThread.Post(() => _viewModel.Fps = fps);
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var scaleAdjustment = e.Delta.Y > 0 ? 1.25f : 0.8f;
            _viewModel?.Zoom(scaleAdjustment);
        }

        private CancellationTokenSource _source = new CancellationTokenSource();
        private object _gate = new object();

        private CancellationToken GetNewToken()
        {
            //lock(_gate)
            //{
            //    _source.Cancel();
            //    _source.Dispose();
            //    _source = new CancellationTokenSource();
            //    return _source.Token;
            //}

            return CancellationToken.None;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var point = e.GetCurrentPoint(this);

            var token = GetNewToken();
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var delta = point.Position - _lastCursorPosition;
                    _lastCursorPosition = point.Position;

                    if (_isPanning)
                    {
                        Dispatcher.UIThread.Post(() => _viewModel?.Pan(delta.X, delta.Y));
                    }

                    if (_isRotating)
                    {
                        Dispatcher.UIThread.Post(() => _viewModel?.Rotate(-delta.X, delta.Y));
                    }

                    var closest = GetClosestVertexInRange(point.Position, token);
                    if (closest.HasValue)
                    {
                        var (closestVertex, _closestPoint, _distance) = closest.GetValueOrDefault();
                        _highlightVertex = closestVertex;
                    }
                    else
                    {
                        _highlightVertex = null;
                    }

                    Dispatcher.UIThread.Post(() => InvalidateVisual());
                }
                catch (OperationCanceledException)
                {

                }
            });
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastCursorPosition = point.Position;
            }

            if (point.Properties.IsRightButtonPressed)
            {
                _isRotating = true;
                _lastCursorPosition = point.Position;
            }

            if (point.Properties.IsLeftButtonPressed)
            {
                var cursorLocation = point.Position;
                var closest = GetClosestVertexInRange(cursorLocation, CancellationToken.None);
                if (closest.HasValue)
                {
                    var (closestVertex, _closestPoint, _distanceSquared) = closest.GetValueOrDefault();
                    InteractionManager?.PushVector3(closestVertex);
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsMiddleButtonPressed)
            {
                _isPanning = false;
            }

            if (!point.Properties.IsRightButtonPressed)
            {
                _isRotating = false;
            }
        }

        private Matrix4x4 GetCorrectedTransform()
        {
            var transform = _viewModel.ViewTransform
                * Matrix4x4.CreateScale(1.0f, -1.0f, 1.0f)
                * Matrix4x4.CreateTranslation(new Vector3((float)Bounds.Width / 2, (float)Bounds.Height / 2, 0));
            return transform;
        }

        private void RecalculateVertices()
        {
            if (_viewModel is null)
            {
                return;
            }

            var model = _viewModel.Model;
            if (model is null)
            {
                return;
            }

            var token = GetNewToken();
            var transform = GetCorrectedTransform();
            var swapTransformedModel = new Model((Vector3[])model.Vertices.Clone(), model.Triangles);
            Parallel.ForEach(Enumerable.Range(0, swapTransformedModel.Vertices.Length), new ParallelOptions() { CancellationToken = token }, i =>
            {
                var v = swapTransformedModel.Vertices[i];
                var vTransformed = Vector3.Transform(v, transform);
                swapTransformedModel.Vertices[i] = vTransformed;
            });

            // do the swap
            if (!token.IsCancellationRequested)
            {
                _transformedModel = swapTransformedModel;
            }
        }

        private (Vector3, Point, double)? GetClosestVertexInRange(Point cursorLocation, CancellationToken cancellationToken)
        {
            if (_viewModel?.Model is null)
            {
                return null;
            }

            var model = _viewModel.Model;
            var transformedModel = _transformedModel;

            if (model is null || transformedModel is null)
            {
                return null;
            }

            var minimumDistance = 10.0;
            var closestVertexIndex = 0;
            var closestDistance = double.MaxValue;

            var (closestIndex, closestDistanceSquared) = transformedModel.Vertices.AsParallel()
                .WithCancellation(cancellationToken)
                .Select((v, i) => (i, DistanceSquared(cursorLocation, v.ToPoint())))
                .MinBy(p => p.Item2);
            if (closestDistanceSquared < minimumDistance * minimumDistance)
            {
                return (model.Vertices[closestIndex], transformedModel.Vertices[closestIndex].ToPoint(), closestDistanceSquared);
            }

            return null;
        }

        private static double DistanceSquared(Point a, Point b)
        {
            var delta = a - b;
            return delta.X * delta.X + delta.Y * delta.Y;
        }
    }
}
