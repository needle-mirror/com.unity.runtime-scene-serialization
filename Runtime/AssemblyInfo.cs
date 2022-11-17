using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.Tests")]

#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.CodeGen")]
[assembly: InternalsVisibleTo("Unity.RuntimeSceneSerialization.Editor")]
#endif
