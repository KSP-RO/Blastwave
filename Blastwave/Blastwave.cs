using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blastwave
{
    public class Blastwave
    {
        const int BASELINE_EXPLOSION_COUNT = 32;
        const float KG_TNT_TO_JOULE_CONVERSION = 4.184e6f;
        const float JOULE_TO_KG_TNT_CONVERSION = 2.390e-7f;

        float yield;                    //in J
        float equivalentTNTMass;        //in kg
        float equivTNTMassInvCubeRoot;  //in kg^-(1/3)
        Vector3 worldLocation;          //in m
        double atmPres;                 //in kPa
        double ambientSoundSpeed;       //in m/s
        double ratioSpecHeat;           

        float prevBlastRadius;          //in m
        float prevPeakPressure;         //in kPa
        float prevPosImpulse;           //in kPa*s

        float currentBlastRadius;       //in m
        float currentPeakPressure;      //in kPa
        float currentPosImpulse;        //in kPa*s

        float blastWaveVel;             //in m/s
        float maxOverPres;              //in kPa

        Dictionary<Blastwave, float> potentialCoalesenceCandidates;     //Explosion object w/ distance to it in m

        //From Kinney & Grahm, 1985
        static FloatCurve overPressureCurve; //in kPa
        static FloatCurve posImpulseCurve;   //in kPa*s

        static float peakOverPressure;
        static float peakPosImpulse;

        static Queue<Blastwave> inactiveBlastwaveObjects;
        static List<Blastwave> activeBlastwaveObjects;
        static bool ready = false;
        static Vector3d floatingOrigin;

        #region Constructor And Explosion Initiation
        private Blastwave()
        {
            potentialCoalesenceCandidates = new Dictionary<Blastwave, float>();
        }

        public static void Setup()
        {
            SetOverPressureFloatCurve();
            SetImpulseFloatCurve();
            CreateBaselineExplosionObjects();
            floatingOrigin = FloatingOrigin.Offset;
            ready = true;
        }

        public static void CreateBlastwave(float yield, float maxOverPres, Vector3d worldLocation)
        {
            if(!ready)
            {
                Setup();
            }
            Blastwave newExplosion;
            if (inactiveBlastwaveObjects.Count > 0)
                newExplosion = inactiveBlastwaveObjects.Dequeue();
            else
                newExplosion = new Blastwave();

            newExplosion.SetConditionsAndInitiate(yield, maxOverPres, worldLocation);
        }

        void SetConditionsAndInitiate(float yield, float maxOverPres, Vector3d worldLocation)
        {
            this.yield = yield;
            this.equivalentTNTMass = this.yield * JOULE_TO_KG_TNT_CONVERSION;
            this.equivTNTMassInvCubeRoot = (float)Math.Pow(this.equivalentTNTMass, -(1f / 3f));

            this.worldLocation = worldLocation;
            this.atmPres = FlightGlobals.getStaticPressure(this.worldLocation);
            this.ambientSoundSpeed = FlightGlobals.currentMainBody.GetSpeedOfSound(this.atmPres, FlightGlobals.currentMainBody.GetDensity(this.atmPres, FlightGlobals.currentMainBody.GetTemperature(FlightGlobals.currentMainBody.GetAltitude(worldLocation))));
            this.ratioSpecHeat = FlightGlobals.currentMainBody.atmosphereAdiabaticIndex;

            if (atmPres <= 0 || ambientSoundSpeed <= 0)
                return;

            this.maxOverPres = maxOverPres;

            this.currentPeakPressure = (maxOverPres + 1f) * (float)atmPres;
            this.currentPosImpulse = peakPosImpulse;
            this.currentBlastRadius = 0;
            this.blastWaveVel = 0;

            this.prevPeakPressure = this.currentPeakPressure;
            this.prevPosImpulse = this.currentPosImpulse;
            this.prevBlastRadius = 0;

            for (int i = 0; i < activeBlastwaveObjects.Count; i++)
            {
                Blastwave otherExplosion = activeBlastwaveObjects[i];
                float dist = (otherExplosion.worldLocation - this.worldLocation).magnitude;
                otherExplosion.potentialCoalesenceCandidates.Add(this, dist);
                this.potentialCoalesenceCandidates.Add(otherExplosion, dist);
            }


            activeBlastwaveObjects.Add(this);
        }
        #endregion

        #region Simulate
        public static void SimulateActiveBlastwaves()
        {
            if (ready)
            {
                UpdateWorldLocations();
                UpdateActiveBlastwave();
                UpdateCoalesence();
                CheckFinishedBlastwave();
            }
        }

        static void UpdateWorldLocations()
        {
            for (int i = 0; i < activeBlastwaveObjects.Count; ++i)
            {
                Blastwave exp = activeBlastwaveObjects[i];
                if (floatingOrigin != FloatingOrigin.Offset)
                {
                    exp.worldLocation += (FloatingOrigin.Offset - floatingOrigin);
                }
                exp.worldLocation -= Krakensbane.GetFrameVelocity();
            }
            floatingOrigin = FloatingOrigin.Offset;
        }

        static void UpdateActiveBlastwave()
        {
            for (int i = 0; i < activeBlastwaveObjects.Count; ++i)
            {
                Blastwave exp = activeBlastwaveObjects[i];
                exp.Simulate();
            }
        }

        static void UpdateCoalesence()
        {
            for(int i = 0; i < activeBlastwaveObjects.Count; ++i)
            {
                Blastwave exp = activeBlastwaveObjects[i];
                if (exp.yield <= 0.001f)
                    continue;

                for(int j = 0; j < activeBlastwaveObjects.Count; ++j)
                {
                    if(i == j)
                        continue;
                    Blastwave otherExp = activeBlastwaveObjects[j];

                    float dist = exp.potentialCoalesenceCandidates[otherExp];

                    float mergeRadius = dist * 10f;

                    if(exp.currentBlastRadius > mergeRadius && otherExp.currentBlastRadius > mergeRadius)
                    {
                        float percentDiffBlastRadius = 2 * (exp.currentBlastRadius - otherExp.currentBlastRadius) / (exp.currentBlastRadius + otherExp.currentBlastRadius);
                        percentDiffBlastRadius = Math.Abs(percentDiffBlastRadius);

                        if (percentDiffBlastRadius > 0.05f)
                            continue;

                        //Debug.Log("Merging explosions of yield " + exp.equivalentTNTMass + " and " + otherExp.equivalentTNTMass + " due to proximity\n");
                        float yieldChange = otherExp.yield;
                        exp.UpdateYield(yieldChange, otherExp.worldLocation);
                        otherExp.UpdateYield(-yieldChange, otherExp.worldLocation);
                    }
                }
            }
        }

        static void CheckFinishedBlastwave()
        {
            for (int i = 0; i < activeBlastwaveObjects.Count; ++i)
            {
                Blastwave exp = activeBlastwaveObjects[i];

                if (exp.CheckSimCompleted())
                {
                    activeBlastwaveObjects.RemoveAt(i);
                    --i;

                    if (inactiveBlastwaveObjects.Count < BASELINE_EXPLOSION_COUNT)
                        inactiveBlastwaveObjects.Enqueue(exp);

                    exp.potentialCoalesenceCandidates.Clear();
                    for (int j = 0; j < activeBlastwaveObjects.Count; ++j)
                    {
                        activeBlastwaveObjects[j].potentialCoalesenceCandidates.Remove(exp);
                    }
                }
            }
        }

        void Simulate()
        {
            prevBlastRadius = currentBlastRadius;
            prevPeakPressure = currentPeakPressure;
            prevPosImpulse = currentPosImpulse;

            //first estimation
            blastWaveVel = CalculateBlastWaveVel();

            currentBlastRadius = prevBlastRadius + blastWaveVel * TimeWarp.fixedDeltaTime;
            CalculateOverpressureAndImpulse();
            //second estimation
            blastWaveVel = (blastWaveVel + CalculateBlastWaveVel()) * 0.5f;

            currentBlastRadius = prevBlastRadius + blastWaveVel * TimeWarp.fixedDeltaTime;
            //Debug.Log("curBlastRad, prevBlastRad: " + currentBlastRadius + " " + prevBlastRadius
            //    + "\nwaveVel: " + blastWaveVel + " kg TNT: " + equivalentTNTMass);

            CalculateOverpressureAndImpulse();
            ApplyEffectsToEnvironment();
        }

        bool CheckSimCompleted()
        {
            return yield <= 0.001f || currentBlastRadius > 25000f || blastWaveVel < ambientSoundSpeed * 1.001f;
        }

        void CalculateOverpressureAndImpulse()
        {
            float currentScaledDistance = currentBlastRadius * equivTNTMassInvCubeRoot;
            if(float.IsNaN(currentScaledDistance))
            {
                Debug.Log("scaledDist NaN! Yield: " + equivalentTNTMass + " currentBlastRad " + currentBlastRadius);
            }
            currentPeakPressure = (overPressureCurve.Evaluate(currentScaledDistance)) * (float)atmPres;
            currentPosImpulse = posImpulseCurve.Evaluate(currentScaledDistance) * (float)atmPres;

            if(currentPeakPressure > maxOverPres)
                currentPeakPressure = maxOverPres;
            currentPeakPressure += (float)atmPres;
        }

        float CalculateBlastWaveVel()
        {
            double blastMachNumber = (ratioSpecHeat + 1f) / (2f * ratioSpecHeat);
            blastMachNumber *= (currentPeakPressure / atmPres - 1f);
            blastMachNumber += 1f;
            if (blastMachNumber < 1f)
                blastMachNumber = 1f;
            blastMachNumber = Math.Sqrt(blastMachNumber);

            return (float)(blastMachNumber * ambientSoundSpeed);
        }

        void ApplyEffectsToEnvironment()
        {
            for(int i = 0; i < FlightGlobals.Vessels.Count; ++i)
            {
                Vessel v = FlightGlobals.Vessels[i];
                float dist = (v.vesselTransform.position - worldLocation).magnitude;
                if (dist > currentBlastRadius + 3 * blastWaveVel || dist < prevBlastRadius - 3 * blastWaveVel)
                    continue;

                if (v.situation == Vessel.Situations.LANDED)
                {
                    v.vesselRanges.landed.pack = Math.Max(dist * 2f, v.vesselRanges.landed.pack);
                    v.vesselRanges.landed.unpack = Math.Max(dist * 1.5f, v.vesselRanges.landed.unpack);
                }
                if (v.situation == Vessel.Situations.PRELAUNCH)
                {
                    v.vesselRanges.prelaunch.pack = Math.Max(dist * 2f, v.vesselRanges.prelaunch.pack);
                    v.vesselRanges.prelaunch.unpack = Math.Max(dist * 1.5f, v.vesselRanges.prelaunch.unpack);
                }
                if (v.situation == Vessel.Situations.SPLASHED)
                {
                    v.vesselRanges.splashed.pack = Math.Max(dist * 2f, v.vesselRanges.splashed.pack);
                    v.vesselRanges.splashed.unpack = Math.Max(dist * 1.5f, v.vesselRanges.splashed.unpack);
                }

                CalculateDamageAndForcesOnVessel(v);
            }
            foreach (DestructibleBuilding b in BlastwaveFlightController.Instance.buildings)
            {
                float dist = (b.transform.position - worldLocation).magnitude;
                if (dist < currentBlastRadius && dist > prevBlastRadius)
                    CalculateDamageAndForcesOnBuilding(b, dist);

            }
        }

        void CalculateDamageAndForcesOnVessel(Vessel v)
        {
            float peakPressureDiff = (currentPeakPressure - prevPeakPressure);
            float impulseDiff = (currentPosImpulse - prevPosImpulse);
            float radiusDiff = currentBlastRadius - prevBlastRadius;

            if (radiusDiff <= 0 || float.IsNaN(peakPressureDiff) || float.IsNaN(impulseDiff))
            {
                Debug.Log("Vessel damage error; radDiff: " + radiusDiff + " impulseDiff: " + impulseDiff + " peakDiff: " + peakPressureDiff);
                return;
            }

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                Vector3 directionVector = (p.partTransform.position - worldLocation);
                float dist = directionVector.magnitude;
                if (dist > currentBlastRadius || dist < prevBlastRadius || dist <= 0 || float.IsNaN(dist))
                    continue;

                directionVector /= dist;


                float overpressureApplied = peakPressureDiff * (dist - prevBlastRadius) / radiusDiff + prevPeakPressure - (float)atmPres;
                float impulseApplied = impulseDiff * (dist - prevBlastRadius) / radiusDiff + prevPosImpulse;

                float projBlastArea = p.DragCubes.GetCubeAreaDir(p.partTransform.InverseTransformDirection(directionVector));
                Vector3 impulse = projBlastArea * impulseApplied * directionVector;

                if (float.IsNaN(impulse.sqrMagnitude))
                {
                    Debug.Log("NaN in blastwave; staticPres: " + atmPres + " blastVel: " + blastWaveVel + "\noverPresApplied: " + overpressureApplied + " impulseApplied: " + impulseApplied + "\nyield: " + equivalentTNTMass + " invCubeRootYield: " + equivTNTMassInvCubeRoot);
                    return;
                }
                Debug.Log("Overpressure: " + overpressureApplied + " impulse: " + impulseApplied + " impulse: " + impulse);

                if(!BlastwaveIsoDamageModel.Instance.CalculateDamage(p, impulseApplied, overpressureApplied))
                    p.Rigidbody.AddForce(impulse, ForceMode.Impulse);
            }
        }

        void CalculateDamageAndForcesOnBuilding(DestructibleBuilding b, float dist)
        {
            float peakPressureDiff = (currentPeakPressure - prevPeakPressure);
            float impulseDiff = (currentPosImpulse - prevPosImpulse);
            float radiusDiff = currentBlastRadius - prevBlastRadius;

            if (radiusDiff <= 0 || float.IsNaN(peakPressureDiff) || float.IsNaN(impulseDiff))
            {
                Debug.Log("Vessel damage error; radDiff: " + radiusDiff + " impulseDiff: " + impulseDiff + " peakDiff: " + peakPressureDiff);
                return;
            }
            float overpressureApplied = peakPressureDiff * (dist - prevBlastRadius) / radiusDiff + prevPeakPressure - (float)atmPres;
            float impulseApplied = impulseDiff * (dist - prevBlastRadius) / radiusDiff + prevPosImpulse;

            BlastwaveIsoDamageModel.Instance.CalculateDamage(b, impulseApplied, overpressureApplied);
        }

        void UpdateYield(float additionalYield, Vector3d additionalYieldLocation)
        {
            this.worldLocation = additionalYieldLocation * additionalYield + this.worldLocation * this.yield;

            this.yield += additionalYield;

            if(this.yield >= 0)
                this.worldLocation /= this.yield;

            this.equivalentTNTMass = this.yield * JOULE_TO_KG_TNT_CONVERSION;
            this.equivTNTMassInvCubeRoot = (float)Math.Pow(this.equivalentTNTMass, -(1f / 3f));
        }

        #endregion

        #region Static Setup
        static void SetOverPressureFloatCurve()
        {
            overPressureCurve = new FloatCurve();
            float scaledDistance = 0.00001f;
            while(scaledDistance <= 1000)
            {
                float sqrScaledDistance = scaledDistance * scaledDistance;
                float overPressure, dydxOverPressure;
                float tmp = (0.548697f * sqrScaledDistance + 1f) * (9.76563f * sqrScaledDistance + 1f) * (434.028f * sqrScaledDistance + 1f);
                tmp = 1f / (float)Math.Sqrt(tmp);

                overPressure = (0.0493827f * sqrScaledDistance + 1) * 808f * tmp;

                dydxOverPressure = (404f * (0.0493827f * sqrScaledDistance + 1) *
                    (868.056f * scaledDistance * (0.548697f * sqrScaledDistance + 1f) * (9.76563f * sqrScaledDistance + 1f) +
                    1.09739f * scaledDistance * (9.76563f * sqrScaledDistance + 1f) * (434.028f * sqrScaledDistance + 1f) +
                    19.5313f * scaledDistance * (0.548697f * sqrScaledDistance + 1f) * (434.028f * sqrScaledDistance + 1f)));
                dydxOverPressure *= tmp * tmp * tmp;

                dydxOverPressure = 79.8025f * scaledDistance * tmp - dydxOverPressure;

                overPressureCurve.Add(scaledDistance, overPressure, dydxOverPressure, dydxOverPressure);

                if (scaledDistance < 0.0001f)
                    scaledDistance += 0.00001f;
                else if (scaledDistance < 0.001f)
                    scaledDistance += 0.0001f;
                else if (scaledDistance < 0.01f)
                    scaledDistance += 0.001f;
                else if (scaledDistance < 0.1f)
                    scaledDistance += 0.01f;
                else if (scaledDistance < 1f)
                    scaledDistance += 0.1f;
                else if (scaledDistance < 10f)
                    scaledDistance += 1f;
                else if (scaledDistance < 100f)
                    scaledDistance += 10f;
                else
                    scaledDistance += 100f;
            }
            peakOverPressure = overPressureCurve.Evaluate(0);
        }

        static void SetImpulseFloatCurve()
        {
            posImpulseCurve = new FloatCurve();
            float scaledDistance = 0.00001f;
            while (scaledDistance <= 1000)
            {
                float sqrScaledDistance = scaledDistance * scaledDistance;
                float cubeScaledDistance = scaledDistance * scaledDistance * scaledDistance;
                float quadScaledDistance = sqrScaledDistance * sqrScaledDistance;
                float impulse, dydxImpulse;

                impulse = 0.268537f * cubeScaledDistance + 1;
                impulse = (float)Math.Pow(impulse, 1f / 3f);
                impulse *= sqrScaledDistance;

                dydxImpulse = impulse * scaledDistance;

                impulse = (float)Math.Sqrt(357.346f * quadScaledDistance + 1) / impulse;
                impulse *= 0.067f;
                impulse *= 0.001f;  //convert to s

                dydxImpulse *= (float)Math.Sqrt(357.346f * quadScaledDistance + 1);
                dydxImpulse *= (cubeScaledDistance + 3.72388f);
                dydxImpulse = (-23.9422f * cubeScaledDistance * quadScaledDistance - 0.201f * cubeScaledDistance - 0.498999f) / dydxImpulse;
                dydxImpulse *= 0.001f;  //convert to s

                posImpulseCurve.Add(scaledDistance, impulse, dydxImpulse, dydxImpulse);

                if (scaledDistance < 0.0001f)
                    scaledDistance += 0.00001f;
                else if (scaledDistance < 0.001f)
                    scaledDistance += 0.0001f;
                else if (scaledDistance < 0.01f)
                    scaledDistance += 0.001f;
                else if (scaledDistance < 0.1f)
                    scaledDistance += 0.01f;
                else if (scaledDistance < 1f)
                    scaledDistance += 0.1f;
                else if (scaledDistance < 10f)
                    scaledDistance += 1f;
                else if (scaledDistance < 100f)
                    scaledDistance += 10f;
                else
                    scaledDistance += 100f;
            }
            peakPosImpulse = posImpulseCurve.Evaluate(0);
        }

        static void CreateBaselineExplosionObjects()
        {
            inactiveBlastwaveObjects = new Queue<Blastwave>(BASELINE_EXPLOSION_COUNT);
            for (int i = 0; i < BASELINE_EXPLOSION_COUNT; ++i)
                inactiveBlastwaveObjects.Enqueue(new Blastwave());

            activeBlastwaveObjects = new List<Blastwave>();
        }

        #endregion
    }
}
