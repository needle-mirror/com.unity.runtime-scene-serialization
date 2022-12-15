#if INCLUDE_AUDIO
using System.Collections.Generic;
using UnityEngine;

namespace Unity.RuntimeSceneSerialization.Internal
{
    static class AudioSourcePropertyBagDefinition
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            ReflectedPropertyBagUtils.SetIgnoredProperties(typeof(AudioSource), new HashSet<string>
            {
#if UNITY_EDITOR
                nameof(AudioSource.gamepadSpeakerOutputType),
#endif

                nameof(AudioSource.velocityUpdateMode),
                nameof(AudioSource.time),
                nameof(AudioSource.timeSamples),
                nameof(AudioSource.ignoreListenerVolume),
                nameof(AudioSource.ignoreListenerPause),
                nameof(AudioSource.spatialBlend),
                nameof(AudioSource.reverbZoneMix),
                nameof(AudioSource.spread)
            });

            ReflectedPropertyBagUtils.SetIncludedProperties(typeof(AudioSource), new HashSet<string>
            {
                nameof(AudioSource.pitch)
                // TODO: Custom curves
            });
        }

#pragma warning disable 618
        // Reference property getters and setters needed for serialization that may get stripped on AOT
        public static void Unused(AudioSource audioSource)
        {
            audioSource.volume = audioSource.volume;
            audioSource.clip = audioSource.clip;
            audioSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            audioSource.loop = audioSource.loop;
            audioSource.playOnAwake = audioSource.playOnAwake;
            audioSource.panStereo = audioSource.panStereo;
            audioSource.spatialize = audioSource.spatialize;
            audioSource.spatializePostEffects = audioSource.spatializePostEffects;
            audioSource.bypassEffects = audioSource.bypassEffects;
            audioSource.bypassListenerEffects = audioSource.bypassListenerEffects;
            audioSource.bypassReverbZones = audioSource.bypassReverbZones;
            audioSource.dopplerLevel = audioSource.dopplerLevel;
            audioSource.priority = audioSource.priority;
            audioSource.mute = audioSource.mute;
            audioSource.minDistance = audioSource.minDistance;
            audioSource.maxDistance = audioSource.maxDistance;
            audioSource.rolloffMode = audioSource.rolloffMode;
        }
#pragma warning restore 618
    }
}
#endif
