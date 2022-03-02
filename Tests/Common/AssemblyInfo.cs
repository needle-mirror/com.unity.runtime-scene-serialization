using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.Tests.Playmode")]

#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.Tests.Editor")]
#endif
