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
    /// A driver that reads and writes a RUN OF DEVICES in one call (22 October: the PLC
    /// Connection page's Read and Write - a whole PLC's devices into a backup and back):
    /// MX Component's ReadDeviceBlock2 / WriteDeviceBlock2, the simulator's memory. A word
    /// device gives a word per device; a bit device 16 bits a word, the first device the
    /// lowest bit ("M0" x 2 = M0 - M31). A module's buffer memory ("U1\G6000") is words.
    /// </summary>
    public interface IPlcBlockAccess
    {
        /// <summary><paramref name="count"/> words from <paramref name="first"/>; false (and null) when the controller refused.</summary>
        bool TryReadBlock(string first, int count, out short[] values);

        /// <summary>The words written from <paramref name="first"/>: 0 when done, else the controller's (or -1) refusal.</summary>
        int WriteBlock(string first, short[] values);

        /// <summary>
        /// True when the controller refuses a device beyond the range its parameters give it
        /// (a real CPU): a backup then finds each device's size by asking. The simulator
        /// answers any device - its backup takes the CPU's default sizes.
        /// </summary>
        bool RefusesOutOfRange { get; }
    }

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

