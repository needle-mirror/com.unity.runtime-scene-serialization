using System;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization
{
    /// <summary>
    /// Used to decorate types which should not have property bags generated
    /// </summary>
    public class SkipGeneration : Attribute { }
}
