using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PPPredictor.Core.API
{
    internal static class ResponseJsonWorker
    {
        private interface IRequest
        {
            void Read();
        }

        private sealed class Request<T> : IRequest
        {
            private readonly string json;
            internal readonly TaskCompletionSource<T> Completion =
                new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal Request(string json)
            {
                this.json = json;
            }

            public void Read()
            {
                try
                {
                    JsonSerializer serializer = JsonSerializer.Create();
                    serializer.CheckAdditionalContent = true;
                    using (var reader = new JsonTextReader(new StringReader(json)))
                    {
                        Completion.TrySetResult(serializer.Deserialize<T>(reader));
                    }
                }
                catch (Exception ex)
                {
                    Completion.TrySetException(ex);
                }
            }
        }

        private static readonly object gate = new object();
        private static readonly Queue<IRequest> requests = new Queue<IRequest>();
        private static Task worker;

        internal static Task<T> DeserializeAsync<T>(string json)
        {
            if (json == null)
                throw new ArgumentNullException("value");

            // Global settings may contain caller-owned converters or callbacks.
            if (JsonConvert.DefaultSettings != null)
                return Task.FromResult(JsonConvert.DeserializeObject<T>(json));

            var request = new Request<T>(json);
            lock (gate)
            {
                requests.Enqueue(request);
                if (worker == null)
                    StartWorker();
            }
            return request.Completion.Task;
        }

        private static void StartWorker()
        {
            worker = Task.Factory.StartNew(Drain, CancellationToken.None,
                TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            worker.ContinueWith(WorkerCompleted, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private static void Drain()
        {
            while (true)
            {
                IRequest request;
                lock (gate)
                {
                    if (requests.Count == 0)
                        return;
                    request = requests.Dequeue();
                }
                request.Read();
            }
        }

        private static void WorkerCompleted(Task completed)
        {
            lock (gate)
            {
                if (!ReferenceEquals(worker, completed))
                    return;
                worker = null;
                if (requests.Count != 0)
                    StartWorker();
            }
        }
    }
}
