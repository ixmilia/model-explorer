using System;
using System.Diagnostics;
using System.Numerics;
using System.Xml.Schema;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using IxMilia.ModelExplorer.ViewModels;

namespace IxMilia.ModelExplorer.Views
{
    public partial class ModelRenderer : Control
    {
        public static readonly StyledProperty<Color> BackgroundColorProperty = AvaloniaProperty.Register<ModelRenderer, Color>(nameof(Color));
        public static readonly StyledProperty<double> FpsProperty = AvaloniaProperty.Register<ModelRenderer, double>(nameof(Fps));
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

        public double Fps
        {
            get => GetValue(FpsProperty);
            set => SetValue(FpsProperty, value);
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
                        RecalculateVertices();
                        InvalidateVisual();
                    }
                };
            }
        }

        public override void Render(DrawingContext context)
        {
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
            context.DrawLine(_xAxisPen, axisCenter.ToPoint(), (xaxisDirection * axisSize + axisCenter).ToPoint());
            context.DrawLine(_yAxisPen, axisCenter.ToPoint(), (yaxisDirection * axisSize + axisCenter).ToPoint());
            context.DrawLine(_zAxisPen, axisCenter.ToPoint(), (zaxisDirection * axisSize + axisCenter).ToPoint());

            sw.Stop();
            var elapsed = sw.ElapsedMilliseconds;
            if (elapsed == 0.0)
            {
                Fps = double.PositiveInfinity;
            }
            else
            {
                Fps = 1.0 / elapsed;
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var scaleAdjustment = e.Delta.Y > 0 ? 1.25f : 0.8f;
            _viewModel?.Zoom(scaleAdjustment);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var point = e.GetCurrentPoint(this);
            var delta = point.Position - _lastCursorPosition;
            _lastCursorPosition = point.Position;

            if (_isPanning)
            {
                _viewModel?.Pan(delta.X, delta.Y);
            }

            if (_isRotating)
            {
                _viewModel?.Rotate(-delta.X, delta.Y);
            }

            var closest = GetClosestVertexInRange(point.Position);
            if (closest.HasValue)
            {
                var (closestVertex, _closestPoint, _distance) = closest.GetValueOrDefault();
                _highlightVertex = closestVertex;
            }
            else
            {
                _highlightVertex = null;
            }

            InvalidateVisual();
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
                var closest = GetClosestVertexInRange(cursorLocation);
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

            var transform = GetCorrectedTransform();
            var swapTransformedModel = new Model((Vector3[])model.Vertices.Clone(), model.Triangles);
            for (int i = 0; i < swapTransformedModel.Vertices.Length; i++)
            {
                var v = swapTransformedModel.Vertices[i];
                var vTransformed = Vector3.Transform(v, transform);
                swapTransformedModel.Vertices[i] = vTransformed;
            }

            // do the swap
            _transformedModel = swapTransformedModel;
        }

        private (Vector3, Point, double)? GetClosestVertexInRange(Point cursorLocation)
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

            var closestVertexIndex = 0;
            var closestDistance = double.MaxValue;
            for (int i = 0; i < transformedModel.Vertices.Length; i++)
            {
                var candidateVertex = transformedModel.Vertices[i].ToPoint();
                var candidateDistance = DistanceSquared(cursorLocation, candidateVertex);
                if (candidateDistance < closestDistance)
                {
                    closestDistance = candidateDistance;
                    closestVertexIndex = i;
                }
            }

            var minimumDistance = 5.0;
            if (closestDistance < minimumDistance * minimumDistance)
            {
                return (model.Vertices[closestVertexIndex], transformedModel.Vertices[closestVertexIndex].ToPoint(), closestDistance);
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
