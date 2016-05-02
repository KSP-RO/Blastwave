using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blastwave
{
    class Fireball
    {
        const float KG_TNT_TO_JOULE_CONVERSION = 4.184e6f;
        const float JOULE_TO_KG_TNT_CONVERSION = 2.390e-7f;

        float yield;                    //in J
        float equivalentTNTMass;        //in kg
        float maxRadius;                //in m
        float lifeTime;                 //in s
        float time;                     //in s

        float atmPres;                  //in kPa
        Vector3d worldLocation;         //in m



        void SetConditionsAndInitiate(float yield, Vector3d worldLocation)
        {
            this.yield = yield;
            this.equivalentTNTMass = this.yield * JOULE_TO_KG_TNT_CONVERSION;
            this.maxRadius = CalcMaxRadius(this.equivalentTNTMass);
            this.lifeTime = CalculateLifetime(this.equivalentTNTMass);

            this.time = 0;

            this.atmPres = (float)FlightGlobals.getStaticPressure(this.worldLocation);

            this.worldLocation = worldLocation;
        }

        float CalcMaxRadius(float equivTNTMass)
        {
            return 54.864f * (float)Math.Pow(equivTNTMass * 1e-6f, 2f / 5f);
        }

        float CalculateLifetime(float equivTNTMass)
        {
            return 0.147069f * (float)Math.Pow(equivTNTMass, 1f / 3f);
        }

        void ApplyHeat()
        {
            double timeFactor = 1f;

            if(time * 2f > lifeTime)
                timeFactor -= ((time / lifeTime) - 0.5f) * 2f;

            for(int i = 0; i < FlightGlobals.Vessels.Count; ++i)
            {
                Vessel v = FlightGlobals.Vessels[i];
                if ((v.vesselTransform.position - worldLocation).sqrMagnitude > 2 * maxRadius * maxRadius)
                    continue;

                for(int j = 0; j < v.Parts.Count; ++j)
                {
                    Part p = v.Parts[j];
                    if (p.ShieldedFromAirstream)
                        continue;

                    double radFactor = (p.partTransform.position - worldLocation).magnitude / maxRadius;

                    if (radFactor < 1)
                        radFactor = 1;
                    else if (radFactor > 1.5f)
                        continue;
                    else
                        radFactor = 3f - 2f * radFactor;

                    double heatFlux = 2000 * radFactor * timeFactor;        //2000 kW/m^2 within the radius

                    p.AddSkinThermalFlux(heatFlux * p.skinExposedArea);
                }
            }
        }
    }
}
