using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace IxMilia.ModelExplorer
{
    public class UserInteractionManager
    {
        private TaskCompletionSource<Vector3>? _pointTaskCompletionSource;

        public void PushVector3(Vector3 v)
        {
            _pointTaskCompletionSource?.TrySetResult(v);
        }

        public Task<Vector3> GetVector3Async()
        {
            var pointTaskCompletionSource = new TaskCompletionSource<Vector3>();
            _pointTaskCompletionSource = pointTaskCompletionSource;
            return pointTaskCompletionSource.Task;
        }
    }
}
