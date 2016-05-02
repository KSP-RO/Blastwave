using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blastwave
{
    class FreeReactant
    {
        const double TIME_EXPIRED = 1f;

        public PartResourceDefinition resource;
        public Vector3d worldPosition;
        public Vector3d worldVelocity;
        double volume;
        public double resourceMass;
        public double effectiveRadius;

        double timeAlive;
        public bool expired;

        public FreeReactant(PartResource res, Vector3d worldPosition, Vector3d worldVelocity)
        {
            this.resource = res.info;
            this.resourceMass = res.amount * resource.density;

            //assume amount is in L, aka 0.001 m^3

            this.volume = res.amount * 0.001;
            this.effectiveRadius = volume * 0.75 / Math.PI;
            this.effectiveRadius = Math.Pow(effectiveRadius, (1.0 / 3.0));

            this.worldPosition = worldPosition;
            this.worldVelocity = worldVelocity;

            timeAlive = 0.0;
            expired = false;

            //Debug.Log("[Blastwave]: Created a free reactant of type " + res.resourceName + " with " + this.resourceMass + " t of mass and " + this.effectiveRadius + " size bubble");
        }

        public void FixedUpdate(double timeStep)
        {
            worldPosition += worldVelocity * timeStep;

            timeAlive += timeStep;
            if (timeAlive >= TIME_EXPIRED || resourceMass <= 0)
                expired = true;
        }
    }
}
