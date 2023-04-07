using H.Pipes;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChrisKaczor.Wpf.Application
{
    public static class SingleInstance
    {
        public static event EventHandler<string> MessageReceived = delegate { };

        private static PipeServer<string>? _pipeServer;
        private static SynchronizationContext? _syncContext;

        public static async Task<IDisposable?> GetSingleInstanceHandleAsync(string applicationName)
        {
            // Create the mutex and see if it was newly created
            var mutex = new Mutex(false, applicationName, out var createdNew);

            // Return the mutex if it was created
            if (createdNew) return mutex;

            // Create a client to send to the other instance
            await using var pipeClient = new PipeClient<string>(applicationName);

            try
            {
                // Connect to the other instance
                await pipeClient.ConnectAsync(new CancellationTokenSource(500).Token);

                // Send the command line to the other instance
                await pipeClient.WriteAsync(Environment.CommandLine);
            }
            catch (Exception)
            {
                // Ignored
            }

            return null;
        }

        public static async Task StartAsync(string applicationName)
        {
            // Store the synchronization context of the current thread
            _syncContext = SynchronizationContext.Current;

            _pipeServer = new PipeServer<string>(applicationName);

            _pipeServer.MessageReceived += PipeServer_MessageReceived;

            await _pipeServer.StartAsync();
        }

        public static async Task Stop()
        {
            if (_pipeServer == null) return;

            await _pipeServer.StopAsync();
        }

        private static void PipeServer_MessageReceived(object? sender, H.Pipes.Args.ConnectionMessageEventArgs<string?> e)
        {
            // Fire the event on the original thread
            _syncContext?.Send(_ => MessageReceived(null, e.Message!), null);
        }
    }
}