namespace Kaban.Core.Interfaces
{
    public interface IPlcDriver
    {
        bool IsConnected { get; }

        /// <summary>
        /// True if the most recent read/write actually succeeded. False means the
        /// last operation failed (timeout, disconnect, bad address, etc). Callers
        /// should check this instead of assuming a returned 0 means "PLC value is
        /// zero" - a communication failure and a genuine zero used to be
        /// indistinguishable, which is exactly the kind of thing that can make a
        /// real fault look like "everything reads 0" on an HMI screen.
        /// </summary>
        bool IsHealthy { get; }

        bool Connect();
        void Disconnect();

        int Read(string address);
        int[] ReadMulti(string[] addresses);
        int Write(string address, int value);

        /// <summary>Read that reports success/failure instead of silently returning 0 on error.</summary>
        bool TryRead(string address, out int value);

        /// <summary>
        /// Batch read that reports overall success/failure instead of silently returning 0 on error.
        /// success is true only if every address in the batch was read successfully.
        /// </summary>
        bool TryReadMulti(string[] addresses, out int[] values);
    }

    /// <summary>
    /// Marks a driver whose calls never hold the caller for long: it keeps its
    /// own thread and bounds every wait (MX Component), or it never blocks at
    /// all (simulation). Every other driver is wrapped in BoundedDriver when it
    /// is registered, because the connection poll and the scanner call drivers
    /// from the UI thread.
    /// </summary>
    public interface INonBlockingDriver { }

    /// <summary>
    /// A driver that can say WHAT it is connected to - the CPU's model as the
    /// controller reports it ("FX5U-64MT/ES"). Read once when the connection
    /// opens; empty until then, or when the controller does not say.
    /// </summary>
    public interface IPlcIdentity
    {
        string Identity { get; }
    }
}

