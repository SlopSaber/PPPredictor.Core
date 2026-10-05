using System;
using System.Threading;
using System.Threading.Tasks;

namespace PPPredictor.Core
{
    internal static class OwnedNumericWorker
    {
        internal const int MinimumCount = 128;

        internal static TResult Run<TResult>(Func<object, TResult> computation, object input)
        {
            Task<TResult> task;
            if (ExecutionContext.IsFlowSuppressed())
            {
                task = Task.Factory.StartNew(computation, input, CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    task = Task.Factory.StartNew(computation, input, CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                }
            }
            return task.GetAwaiter().GetResult();
        }

        internal static bool SameBits(double left, double right)
        {
            return BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right);
        }
    }
}
