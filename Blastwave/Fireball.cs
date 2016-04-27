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

        float atmPres;                  //in kPa
        Vector3d worldLocation;         //in m

        void SetConditionsAndInitiate(float yield, Vector3d worldLocation)
        {
            this.yield = yield;
            this.equivalentTNTMass = this.yield * JOULE_TO_KG_TNT_CONVERSION;
            this.maxRadius = CalcMaxRad(this.equivalentTNTMass);

            this.atmPres = (float)FlightGlobals.getStaticPressure(this.worldLocation);

            this.worldLocation = worldLocation;
        }

        float CalcMaxRad(float equivTNTMass)
        {
            return 54.864f * (float)Math.Pow(equivTNTMass * 1e-6f, 2f / 5f);
        }
    }
}
