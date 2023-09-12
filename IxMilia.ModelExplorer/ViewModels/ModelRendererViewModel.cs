using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace IxMilia.ModelExplorer.ViewModels
{
    public class ModelRendererViewModel : ViewModelBase
    {
        private Model? _model;
        private float _viewScaleFactor = 1.0f;
        private Vector3 _cameraLocation = new Vector3(1.0f, -1.0f, 1.0f);
        private Vector3 _cameraTarget = Vector3.Zero;
        private Matrix4x4 _viewTransform;
        private string _status = string.Empty;

        public Model? Model
        {
            get => _model;
            set
            {
                ((IReactiveObject)this).RaisePropertyChanging(new PropertyChangingEventArgs(nameof(Model)));
                _model = value;
                ((IReactiveObject)this).RaisePropertyChanged(new PropertyChangedEventArgs(nameof(Model)));
            }
        }

        public Matrix4x4 ViewTransform
        {
            get => _viewTransform;
            private set
            {
                ((IReactiveObject)this).RaisePropertyChanging(new PropertyChangingEventArgs(nameof(ViewTransform)));
                _viewTransform = value;
                ((IReactiveObject)this).RaisePropertyChanged(new PropertyChangedEventArgs(nameof(ViewTransform)));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                ((IReactiveObject)this).RaisePropertyChanging(new PropertyChangingEventArgs(nameof(Status)));
                _status = value;
                ((IReactiveObject)this).RaisePropertyChanged(new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        internal Vector3 ViewportXAxis => Vector3.Cross(Vector3.UnitZ, _cameraLocation - _cameraTarget);

        internal Vector3 ViewportYAxis => Vector3.Cross(_cameraLocation - _cameraTarget, ViewportXAxis);

        public ModelRendererViewModel()
        {
            RecalculateViewTransform();
        }

        public void Zoom(float scale)
        {
            _viewScaleFactor *= scale;
            RecalculateViewTransform();
        }

        public void Pan(double cursorDx, double cursorDy)
        {
            var deltaX = ViewportXAxis * (float)(-cursorDx / _viewScaleFactor);
            var deltaY = ViewportYAxis * (float)(cursorDy / _viewScaleFactor);
            var delta = deltaX + deltaY;
            _cameraLocation += delta;
            _cameraTarget += delta;
            RecalculateViewTransform();
        }

        public void Rotate(double cursorDx, double cursorDy)
        {
            var rotationSpeed = 0.01;

            // do left-right around z-axis
            var angle = cursorDx * rotationSpeed;
            var rotationMatrix =
                Matrix4x4.CreateTranslation(-_cameraTarget)
                * Matrix4x4.CreateRotationZ((float)angle)
                * Matrix4x4.CreateTranslation(_cameraTarget);
            var newCameraLocation = Vector3.Transform(_cameraLocation, rotationMatrix);
            _cameraLocation = newCameraLocation;

            // up/down will rotate around the current view's x axis
            var angle2 = -cursorDy * rotationSpeed;
            var rotationMatrix2 =
                Matrix4x4.CreateTranslation(-_cameraTarget)
                * Matrix4x4.CreateFromAxisAngle(ViewportXAxis, (float)angle2)
                * Matrix4x4.CreateTranslation(_cameraTarget);
            var newCameraLocation2 = Vector3.Transform(_cameraLocation, rotationMatrix2);
            _cameraLocation = newCameraLocation2;

            RecalculateViewTransform();
        }

        private void RecalculateViewTransform()
        {
            ViewTransform = Matrix4x4.CreateLookAt(_cameraLocation, _cameraTarget, Vector3.UnitZ)
                * Matrix4x4.CreateScale(_viewScaleFactor);
        }
    }
}
