namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Serialized types that implement IFormatVersion will have the opportunity to check if serialized data is the
    /// correct format and throw an exception if the serialized version does not match the expected version
    /// </summary>
    public interface IFormatVersion
    {
        /// <summary>
        /// Called during deserialization. Implementors may choose to throw an exception if the format versions do not match.
        /// </summary>
        void CheckFormatVersion();
    }
}
