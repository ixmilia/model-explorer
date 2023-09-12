using System;
using System.Diagnostics;
using System.Numerics;
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
        private Point[] _transformedVertices = Array.Empty<Point>();
        private Point[] _swapTransformedVertices = Array.Empty<Point>();
        private Vector3? _highlightVertex;
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

            var localTransformedVertices = _transformedVertices;
            for (int i = 0; i < localTransformedVertices.Length; i += 3)
            {
                var v1 = localTransformedVertices[i];
                var v2 = localTransformedVertices[i + 1];
                var v3 = localTransformedVertices[i + 2];

                context.DrawLine(LinePen, v1, v2);
                context.DrawLine(LinePen, v2, v3);
                context.DrawLine(LinePen, v3, v1);
            }

            var localHighlightVertex = _highlightVertex;
            if (localHighlightVertex.HasValue)
            {
                var vertexSize = 5.0;
                var localHighlightVertexValue = localHighlightVertex.GetValueOrDefault().Transform(GetCorrectedTransform());
                context.DrawLine(
                    VertexPen,
                    new Point(localHighlightVertexValue.X - vertexSize, localHighlightVertexValue.Y - vertexSize),
                    new Point(localHighlightVertexValue.X + vertexSize, localHighlightVertexValue.Y + vertexSize));
                context.DrawLine(
                    VertexPen,
                    new Point(localHighlightVertexValue.X - vertexSize, localHighlightVertexValue.Y + vertexSize),
                    new Point(localHighlightVertexValue.X + vertexSize, localHighlightVertexValue.Y - vertexSize));
            }

            sw.Stop();
            var elapsed = sw.ElapsedMilliseconds;
            Fps = elapsed == 0.0 ? double.PositiveInfinity : 1.0 / elapsed;
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
            if (_isRotating)
            {
                var delta = point.Position - _lastCursorPosition;
                _lastCursorPosition = point.Position;
                _viewModel?.Rotate(-delta.X, delta.Y);
            }
            else
            {
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
            }

            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetCurrentPoint(this);
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

            var viewTransform = _viewModel.ViewTransform;
            var _localTransformedVertices = _transformedVertices;
            var _localSwapTransformedVertices = _swapTransformedVertices;
            if (_transformedVertices.Length != model.Vertices.Length)
            {
                // resize
                _localTransformedVertices = new Point[model.Vertices.Length];
                _localSwapTransformedVertices = new Point[model.Vertices.Length];
            }

            var transformedVertexIndex = 0;
            var transform = GetCorrectedTransform();
            for (int i = 0; i < model.Triangles.Length; i++)
            {
                var triangle = model.Triangles[i];
                var v1 = model.Vertices[triangle.V1];
                var v2 = model.Vertices[triangle.V2];
                var v3 = model.Vertices[triangle.V3];
                var v1Transformed = v1.Transform(transform);
                var v2Transformed = v2.Transform(transform);
                var v3Transformed = v3.Transform(transform);
                _localSwapTransformedVertices[transformedVertexIndex++] = v1Transformed;
                _localSwapTransformedVertices[transformedVertexIndex++] = v2Transformed;
                _localSwapTransformedVertices[transformedVertexIndex++] = v3Transformed;
            }

            // do the swap
            _transformedVertices = _localSwapTransformedVertices;
            _swapTransformedVertices = _localTransformedVertices;
        }

        private (Vector3, Point, double)? GetClosestVertexInRange(Point cursorLocation)
        {
            if (_viewModel?.Model is null)
            {
                return null;
            }

            var vertices = _viewModel.Model.Vertices;
            var minimumDistance = 5.0;
            var transformedVertices = _transformedVertices;
            if (transformedVertices.Length > 0)
            {
                var closestVertex = vertices[0];
                var closestScreenPoint = _transformedVertices[0];
                var lastDistance = double.MaxValue;
                for (int i = 0; i < transformedVertices.Length; i++)
                {
                    var vertex = transformedVertices[i];
                    var distanceSquared = DistanceSquared(cursorLocation, vertex);
                    if (distanceSquared < lastDistance)
                    {
                        lastDistance = distanceSquared;
                        closestVertex = vertices[i];
                        closestScreenPoint = transformedVertices[i];
                    }
                }

                if (lastDistance < minimumDistance * minimumDistance)
                {
                    return (closestVertex, closestScreenPoint, lastDistance);
                }
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
