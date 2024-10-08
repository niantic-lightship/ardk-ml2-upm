// Copyright 2022-2024 Niantic.

using System;
using System.Threading;
using System.Threading.Tasks;
using Niantic.Lightship.AR.Utilities.Logging;

namespace Niantic.Lightship.MagicLeap
{
    /// <summary>
    /// Base class for services that start and stop asynchronously.
    /// </summary>
    public abstract class AsyncService<T> : LazySingleton<T> where T : class, new()
    {
        private enum ServiceStatus
        {
            /// <summary>
            /// The service is not running.
            /// </summary>
            Stopped,

            /// <summary>
            /// The service is starting.
            /// </summary>
            Starting,

            /// <summary>
            /// The service is running.
            /// </summary>
            Running,

            /// <summary>
            /// The service is stopping.
            /// </summary>
            Stopping
        }

        private ServiceStatus _status = ServiceStatus.Stopped;
        private ServiceStatus Status
        {
            get => _status;
            set
            {
                Log.Info(ServiceName + " status from " + _status + " changed to " + value + ".");
                _status = value;
            }
        }

        /// <summary>
        /// Indicates whether the service is currently running.
        /// </summary>
        public bool IsRunning
        {
            get => _status == ServiceStatus.Running;
        }

        /// <summary>
        /// The name of this service.
        /// </summary>
        protected abstract string ServiceName { get; }

        // Cancellation tokens
        private CancellationTokenSource _startProcessCancellation;

        /// <summary>
        /// Attempts to start the service.
        /// </summary>
        public async void Start()
        {
            if (Status != ServiceStatus.Stopped)
            {
                Log.Warning("Cannot start " + ServiceName + ", because it is " + Status + ".");
                return;
            }

            // Create a new cancellation token source
            _startProcessCancellation = new CancellationTokenSource();
            var token = _startProcessCancellation.Token;

            try
            {
                // Start the service
                Status = ServiceStatus.Starting;
                var success = await OnStarting(token);

                // Evaluate the service status
                if (success)
                {
                    Status = ServiceStatus.Running;
                    OnStarted();
                }
                else
                {
                    Status = ServiceStatus.Stopped;
                    Log.Error("Could not start " + ServiceName + ".");
                }
            }
            catch (Exception e)
            {
                Status = ServiceStatus.Stopped;
                Log.Error("Could not start " + ServiceName + ": " + e);
            }
            finally
            {
                // Dispose of the cancellation token source
                _startProcessCancellation?.Dispose();
                _startProcessCancellation = null;
            }
        }

        /// <summary>
        /// Attempts to stop the service.
        /// </summary>
        public async void Stop()
        {
            // Inspect the current status
            var previousStatus = Status;
            switch (previousStatus)
            {
                // Don't need to stop if already stopped
                case ServiceStatus.Stopping:
                case ServiceStatus.Stopped:
                    Log.Warning("Cannot stop " + ServiceName + ", because it is " + Status + ".");
                    return;

                // Requires cancellation of the start process
                case ServiceStatus.Starting:
                {
                    // Cancel
                    _startProcessCancellation?.Cancel();

                    // Wait for the cancellation to finish
                    while (_status == ServiceStatus.Starting)
                    {
                        await Task.Delay(100);
                    }
                    break;
                }
            }

            // Stop the service
            Status = ServiceStatus.Stopping;
            var success = await OnStopping();

            // Evaluate the service status
            Status = success ? ServiceStatus.Stopped : previousStatus;

            // Only invoke the stopped event if the service was running
            if (success && previousStatus == ServiceStatus.Running)
            {
                OnStopped();
            }
        }

        /// <summary>
        /// Invoked when the service needs to start.
        /// </summary>
        protected virtual async Task<bool> OnStarting(CancellationToken cancellation)
        {
            await Task.Delay(0, cancellation);
            return true;
        }

        /// <summary>
        /// Invoked when the service has successfully started.
        /// Subscribe to events and perform other initialization here.
        /// </summary>
        protected virtual void OnStarted()
        {
        }

        /// <summary>
        /// Invoked when the service needs to stop.
        /// </summary>
        protected virtual async Task<bool> OnStopping()
        {
            await Task.Delay(0);
            return true;
        }

        /// <summary>
        /// Invoked when the service has successfully stopped.
        /// Unsubscribe from events and perform other cleanup here.
        /// </summary>
        protected virtual void OnStopped()
        {
        }
    }
}
