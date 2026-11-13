using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomFootTrails
{
    /// <summary>A collection of world-space Particle Systems fired at foot contact.</summary>
    [DisallowMultipleComponent]
    public class FootTrailEffect : MonoBehaviour
    {
        [Serializable]
        public struct Layer
        {
            public ParticleSystem particles;
            [Min(0)] public int count;
        }

        [SerializeField] private List<Layer> layers = new List<Layer>();

        public void EmitOnce()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                ParticleSystem ps = layers[i].particles;
                if (ps == null || layers[i].count <= 0) continue;
                if (!ps.isPlaying) ps.Play();
                ps.Emit(layers[i].count);
            }
        }

        public void StopAndClear()
        {
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].particles != null)
                    layers[i].particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Used by the editor preset generator; also available for procedural setup.
        public void Configure(ParticleSystem first, int firstCount,
            ParticleSystem second, int secondCount)
        {
            layers.Clear();
            layers.Add(new Layer { particles = first, count = firstCount });
            layers.Add(new Layer { particles = second, count = secondCount });
        }
    }
}
