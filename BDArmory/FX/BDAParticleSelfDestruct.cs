﻿using System.Collections;
using UnityEngine;

namespace BDArmory.FX
{
    public class BDAParticleSelfDestruct : MonoBehaviour
    {
        KSPParticleEmitter pEmitter;
        BDAGaplessParticleEmitter gpe;

        void Awake()
        {
            pEmitter = gameObject.GetComponent<KSPParticleEmitter>();
            EffectBehaviour.AddParticleEmitter(pEmitter);
            gpe = gameObject.GetComponent<BDAGaplessParticleEmitter>();
        }

        void Start()
        {
            if (pEmitter.pe.particleCount == 0)
            {
                Destroy(gameObject);
            }
            else
            {
                StartCoroutine(SelfDestructRoutine());
            }
        }

        IEnumerator SelfDestructRoutine()
        {
            pEmitter.emit = false;
            EffectBehaviour.RemoveParticleEmitter(pEmitter);
            if (gpe)
            {
                gpe.emit = false;
                EffectBehaviour.RemoveParticleEmitter(gpe.pEmitter);
            }
            yield return new WaitForSeconds(pEmitter.maxEnergy);
            Destroy(gameObject);
            yield break;
        }
    }
}