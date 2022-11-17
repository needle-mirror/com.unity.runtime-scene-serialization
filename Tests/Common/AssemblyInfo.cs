using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.Tests")]

#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.Tests.Editor")]
#endif
