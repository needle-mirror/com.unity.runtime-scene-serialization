using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.EditorInternal
{
    [InitializeOnLoad]
    static class CodeGenLog
    {
        static CodeGenLog()
        {
            CompilationPipeline.compilationStarted += _ =>
            {
                const string logfilePath = "Logs/RuntimeSerializationLogs.txt";
                if (File.Exists(logfilePath))
                    File.Delete(logfilePath);
            };
        }
    }
}
