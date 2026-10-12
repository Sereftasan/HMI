namespace Kaban.Core.Enums
{
    public enum SecurityLevel
    {
        None = 0,        // Anyone can use
        Operator = 10,   // Basic operations
        Maintenance = 20,// Setup, manual overrides
        Admin = 30       // Recipe changes, config
    }
}
