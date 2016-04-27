using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blastwave
{
    class BlastwaveIsoDamageModel
    {
        const float NO_DAMAGE_NORMALIZED_IMPULSE = 0.0689476f;          //kPa * s
        const float NO_DAMAGE_NORMALIZED_OVERPRES = 5.5f;               //kPa

        const float TOTAL_DESTRUCTION_NORMALIZED_IMPULSE = 0.4826333f;  //kPa * s
        const float TOTAL_DESTRUCTION_NORMALIZED_OVERPRES = 27.579f;    //kPa
        const float TOTAL_DESTRUCTION_DAMAGE_LEVEL = (TOTAL_DESTRUCTION_NORMALIZED_IMPULSE - NO_DAMAGE_NORMALIZED_IMPULSE) * (TOTAL_DESTRUCTION_NORMALIZED_OVERPRES - NO_DAMAGE_NORMALIZED_OVERPRES);
        const float TOTAL_DESTRUCTION_DAMAGE_LEVEL_INV = 1 / TOTAL_DESTRUCTION_DAMAGE_LEVEL;

        const float CRASH_TOLERANCE_SCALE_FACTOR = 0.1f;
        const float BUILDING_DAMAGE_SCALE_FACTOR = 0.25f;

        static BlastwaveIsoDamageModel instance;
        public static BlastwaveIsoDamageModel Instance
        {
            get
            {
                if (instance == null)
                    instance = new BlastwaveIsoDamageModel();
                return instance;
            }
        }

        System.Random damageRNG = new System.Random();

        public bool CalculateDamage(Part p, float impulse, float overPressure)
        {
            if(p == null)
                return false;

            float scaling = p.crashTolerance * CRASH_TOLERANCE_SCALE_FACTOR;

            float impulseOffset = impulse - (NO_DAMAGE_NORMALIZED_IMPULSE * scaling);
            float overPresOffset = overPressure - (NO_DAMAGE_NORMALIZED_OVERPRES * scaling);

            float damageFactor = impulseOffset * overPresOffset * TOTAL_DESTRUCTION_DAMAGE_LEVEL_INV / (scaling);

            if (damageFactor < 0)
                return false;
            else if (damageFactor > 1)
            {
                //Debug.Log("Part " + p.partInfo.title + " destroyed by a blastwave");
                p.explode();
                return true;
            }
            if(damageFactor < 0.4f)
            {
                if(p.attachJoint == null)
                    return false;
                //joints are weakened
                float jointWeakeningValue = damageFactor * 2.5f;

                p.breakingForce *= 1f - (0.25f * damageFactor);
                p.breakingTorque *= 1f - (0.25f * damageFactor);

                p.attachJoint.SetBreakingForces(p.breakingForce, p.breakingTorque);
            }
            else 
            {
                if(p.attachJoint != null)
                    p.attachJoint.SetBreakingForces(p.breakingForce * 0.75f, p.breakingTorque * 0.75f);
                if (damageFactor < 0.8f)
                {
                    float jointBreakValue = 2.5f * damageFactor - 1f;

                    if (jointBreakValue > damageRNG.NextDouble())
                    {
                        p.decouple(5);
                    }
                }
                else
                {
                    p.decouple(5);
                    float partExplodeValue = 5f * damageFactor - 4f;

                    if (partExplodeValue > damageRNG.NextDouble())
                    {
                        p.explode();
                        return true;
                        //Debug.Log("Part " + p.partInfo.title + " destroyed by a blastwave");
                    }
                }
            }
            return false;
        }

        public void CalculateDamage(DestructibleBuilding b, float impulse, float overPressure)
        {
            if (b == null)
                return;

            float scaling = (float)b.impactMomentumThreshold * BUILDING_DAMAGE_SCALE_FACTOR;

            float impulseOffset = impulse - (NO_DAMAGE_NORMALIZED_IMPULSE * scaling);
            float overPresOffset = overPressure - (NO_DAMAGE_NORMALIZED_OVERPRES * scaling);

            float damageFactor = impulseOffset * overPresOffset * TOTAL_DESTRUCTION_DAMAGE_LEVEL_INV;

            if (damageFactor < 0)
                return;
            else
            {
                b.AddDamage(impulseOffset * overPresOffset * 1000f);
            }
        }
    }
}
